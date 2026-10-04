using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Paints a MapLayout onto this TileMapLayer, and its deposits onto ResourceLayer. The tile set follows the map's
/// theme: the nursery's floorboards (with rugs) and walls, or the garden's lawn (with clover and bare soil), hedges
/// (the walls) and cobbled path (in the hall).
/// </summary>
public partial class MapView : TileMapLayer
{
	// Source ids in Assets/TileSets/RoomTiles.tres and GardenTiles.tres (the garden adds the path).
	private const int FloorSource = 0;
	private const int WallSource = 1;
	private const int PathSource = 2;
	private const int PathTiles = 4; // the gravel path and hedge pictures repeat every 4x4 tiles

	/// <summary>Layer drawn above the floor. Uses Assets/TileSets/ResourceTiles.tres,
	/// whose source ids are ResourceType - 1 (Brick = 0, Plastic = 1, Battery = 2).</summary>
	[Export] public TileMapLayer ResourceLayer;

	/// <summary>
	/// The floor picture (big boards) spans this many tiles and repeats; each tile shows its part of it,
	/// so boards run on across tiles. Must match tools/art/restyle.py FLOOR_TILES_X/Y.
	/// </summary>
	private const int FloorTilesX = 16, FloorTilesY = 8;

	private CloverView _clover;
	private FloorDecorView _decor;

	public void Render(MapLayout map)
	{
		if (_clover == null)
		{
			// Children: over the floor tiles, under the deposits. The decor over the clover.
			AddChild(_clover = new CloverView { Name = "Clover" });
			AddChild(_decor = new FloorDecorView { Name = "FloorDecor" });
		}
		_clover.Bind(map);
		_decor.Bind(map);
		bool garden = map.Theme == MapTheme.Garden;
		TileSet = GD.Load<TileSet>(garden ? "res://Assets/TileSets/GardenTiles.tres" : "res://Assets/TileSets/RoomTiles.tres");
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
				if (garden && source == FloorSource && map.GetZone(x, y) == Zone.Hall)
					SetCell(cell, PathSource, new Vector2I(x % PathTiles, y % PathTiles));
				else if (garden && source == WallSource)
					SetCell(cell, WallSource, new Vector2I(x % PathTiles, y % PathTiles));
				else if (source >= 0)
					SetCell(cell, source, source == FloorSource ? new Vector2I(x % FloorTilesX, y % FloorTilesY) : Vector2I.Zero);

				var resource = map.GetResource(x, y);
				if (resource != ResourceType.None)
					ResourceLayer?.SetCell(cell, (int)resource - 1, Vector2I.Zero);
			}
		}
	}
}
