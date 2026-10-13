using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>One clover (or clover flower) lying on the lawn: its centre in pixels and its picture on the sheet.</summary>
public readonly record struct Clover(int X, int Y, int Sprite);

/// <summary>
/// Clover on the garden's lawn, like a real lawn: patches of it, thick in the middle and thinning out at the edges,
/// with plain grass between. A smooth noise over the map gives each tile a density; each tile then gets that many
/// clovers at spots of its own, which may lie over the tile's edge, so a patch has no square corners. Only on lawn
/// in the rooms (not the gravel path in the hall, the flower beds or the deposits). Pure arithmetic, no randomness,
/// so the same map always looks the same.
/// </summary>
public static class Lawn
{
	/// <summary>Pictures on Tiles/clover.png (tools/art/garden.py): leaves first, then the flowers.</summary>
	public const int Leaves = 6, Flowers = 2, SpriteSize = 24;

	/// <summary>The noise's grid, in tiles: about how far apart the patches are.</summary>
	public const int PatchTiles = 7;

	private const int TileSize = 64;

	/// <summary>How thick the clover is on a tile: 0 (none) to 3 (a thick mat).</summary>
	public static int Density(int x, int y)
	{
		int gx = x / PatchTiles, gy = y / PatchTiles, fx = x % PatchTiles, fy = y % PatchTiles;
		int top = Byte(gx, gy, 0) * (PatchTiles - fx) + Byte(gx + 1, gy, 0) * fx;
		int bottom = Byte(gx, gy + 1, 0) * (PatchTiles - fx) + Byte(gx + 1, gy + 1, 0) * fx;
		int value = (top * (PatchTiles - fy) + bottom * fy) / (PatchTiles * PatchTiles); // 0..255, smooth
		value += Byte(x, y, 1) % 40 - 20;                                                 // a ragged edge
		return value > 205 ? 3 : value > 180 ? 2 : value > 160 ? 1 : 0;
	}

	/// <summary>Clovers per tile at each density.</summary>
	public static int Count(int density) => density switch { 3 => 9, 2 => 5, 1 => 2, _ => 0 };

	/// <summary>Every clover on the map's lawn (none on other maps), back to front.</summary>
	public static List<Clover> Clovers(MapLayout map)
	{
		var clovers = new List<Clover>();
		if (map.Theme != MapTheme.Garden)
			return clovers;
		for (int y = 0; y < map.Height; y++)
			for (int x = 0; x < map.Width; x++)
			{
				if (!IsLawn(map, x, y))
					continue;
				int count = Count(Density(x, y));
				for (int i = 0; i < count; i++)
				{
					int h = Hash(x * 16 + i, y, 2);
					// anywhere on the tile and up to a few pixels over its edge
					int px = x * TileSize - 6 + h % (TileSize + 12);
					int py = y * TileSize - 6 + h / 97 % (TileSize + 12);
					bool flower = Hash(x * 16 + i, y, 3) % 9 == 0;
					int sprite = flower ? Leaves + h / 13 % Flowers : h / 7 % Leaves;
					clovers.Add(new Clover(px, py, sprite));
				}
			}
		clovers.Sort((a, b) => a.Y.CompareTo(b.Y));
		return clovers;
	}

	/// <summary>Lawn: floor (or a plant lying on it) in a room, without a deposit.</summary>
	public static bool IsLawn(MapLayout map, int x, int y) =>
		map[x, y] is TileType.Floor or TileType.Obstacle && map.GetZone(x, y) != Zone.Hall
		&& map.GetResource(x, y) == ResourceType.None;

	private static int Byte(int x, int y, int salt) => Hash(x, y, salt) >> 8;

	/// <summary>A well-mixed number 0..65535 for a spot (and a purpose, so the uses don't line up).</summary>
	internal static int Hash(int x, int y, int salt)
	{
		unchecked
		{
			uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
			h ^= h >> 13;
			h *= 0x5bd1e995;
			h ^= h >> 15;
			return (int)(h & 0xFFFF);
		}
	}
}
