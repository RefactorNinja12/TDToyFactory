using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>What lies flat on the floor: bare soil on the lawn, rugs on the nursery's boards.</summary>
public enum DecorKind : byte
{
	Dirt,        // garden: a patch of bare soil (Tiles/dirt.png, three variants side by side)
	RoundRug,    // nursery: a round rug with rings
	StripedRug,  // nursery: a striped rug with fringes
	PlayMat,     // nursery: a play mat with roads on it
}

/// <summary>One piece of floor decor, in tiles: its kind, which picture of that kind, and the rectangle it covers.</summary>
public readonly record struct Decor(DecorKind Kind, int Variant, int X, int Y, int Width, int Height);

/// <summary>
/// Floor variation: a few patches of bare soil on the garden's lawn, rugs on the nursery's floor. Only looks:
/// drawn flat over the floor, under deposits, buildings and everything else. Placed in the left room on plain
/// floor (no deposit, wall, toy or hall), apart from each other, and mirrored into the right room, so both
/// players get the same. Pure arithmetic from the map, so it always looks the same.
/// </summary>
public static class FloorDecor
{
	public const int DirtVariants = 3;

	private static readonly (DecorKind Kind, int Width, int Height)[] Rugs =
	{
		(DecorKind.PlayMat, 7, 5), (DecorKind.StripedRug, 6, 4), (DecorKind.RoundRug, 5, 5),
	};

	private static readonly (int Width, int Height)[] DirtSizes = { (3, 3), (2, 2), (3, 2), (2, 3) };

	public const int DirtPatches = 9;

	public static List<Decor> For(MapLayout map)
	{
		var wanted = new List<(DecorKind Kind, int Variant, int Width, int Height)>();
		if (map.Theme == MapTheme.Garden)
			for (int i = 0; i < DirtPatches; i++)
			{
				var (w, h) = DirtSizes[i % DirtSizes.Length];
				wanted.Add((DecorKind.Dirt, i % DirtVariants, w, h));
			}
		else
			foreach (var (kind, w, h) in Rugs)
				wanted.Add((kind, 0, w, h));

		var placed = new List<Decor>();
		var (x0, y0, x1, y1) = map.ZoneBounds(Zone.LeftRoom);
		int roomW = x1 - x0 + 1, roomH = y1 - y0 + 1;
		for (int i = 0; i < wanted.Count; i++)
		{
			var (kind, variant, w, h) = wanted[i];
			for (int attempt = 0; attempt < 400; attempt++)
			{
				int hash = Lawn.Hash(i, attempt, 4);
				var decor = new Decor(kind, variant, x0 + hash % roomW, y0 + hash / 97 % roomH, w, h);
				if (Fits(map, decor) && !placed.Exists(d => Near(d, decor)))
				{
					placed.Add(decor);
					break;
				}
			}
		}
		int count = placed.Count;
		for (int i = 0; i < count; i++)
			placed.Add(placed[i] with { X = map.Width - placed[i].X - placed[i].Width });
		return placed;
	}

	/// <summary>Every tile under it is plain floor in the left room.</summary>
	private static bool Fits(MapLayout map, Decor d)
	{
		for (int y = d.Y; y < d.Y + d.Height; y++)
			for (int x = d.X; x < d.X + d.Width; x++)
				if (!map.InBounds(x, y) || map[x, y] != TileType.Floor || map.GetZone(x, y) != Zone.LeftRoom
					|| map.GetResource(x, y) != ResourceType.None)
					return false;
		return true;
	}

	/// <summary>Overlapping or touching (at least one tile of floor between two pieces).</summary>
	private static bool Near(Decor a, Decor b) =>
		a.X - 1 < b.X + b.Width && b.X - 1 < a.X + a.Width && a.Y - 1 < b.Y + b.Height && b.Y - 1 < a.Y + a.Height;
}
