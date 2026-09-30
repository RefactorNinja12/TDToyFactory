using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>Paints a MapLayout onto this TileMapLayer, and its deposits onto ResourceLayer.</summary>
public partial class MapView : TileMapLayer
{
	// Source ids in Assets/TileSets/RoomTiles.tres
	private const int FloorSource = 0;
	private const int WallSource = 1;

	/// <summary>Layer drawn above the floor. Uses Assets/TileSets/ResourceTiles.tres,
	/// whose source ids are ResourceType - 1 (Brick = 0, Plastic = 1, Battery = 2).</summary>
	[Export] public TileMapLayer ResourceLayer;

	/// <summary>
	/// The floor picture (big boards) spans this many tiles and repeats; each tile shows its part of it,
	/// so boards run on across tiles. Must match tools/art/restyle.py FLOOR_TILES_X/Y.
	/// </summary>
	private const int FloorTilesX = 16, FloorTilesY = 8;

	public void Render(MapLayout map)
	{
		Clear();
		ResourceLayer?.Clear();
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				var cell = new Vector2I(x, y);
				int source = map[x, y] switch
				{
					TileType.Floor or TileType.Obstacle => FloorSource, // toys lie on the floor (ObstacleView)
					TileType.Wall => WallSource,
					_ => -1,
				};
				if (source >= 0)
					SetCell(cell, source, source == FloorSource ? new Vector2I(x % FloorTilesX, y % FloorTilesY) : Vector2I.Zero);

				var resource = map.GetResource(x, y);
				if (resource != ResourceType.None)
					ResourceLayer?.SetCell(cell, (int)resource - 1, Vector2I.Zero);
			}
		}
	}
}
