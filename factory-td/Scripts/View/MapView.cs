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

	/// <summary>Floor tiles come in variants side by side in the atlas; picked per tile so the floor doesn't repeat.</summary>
	private const int FloorVariants = 4;

	private static int FloorVariant(int x, int y) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663)) % FloorVariants);

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
					TileType.Floor => FloorSource,
					TileType.Wall => WallSource,
					_ => -1,
				};
				if (source >= 0)
					SetCell(cell, source, source == FloorSource ? new Vector2I(FloorVariant(x, y), 0) : Vector2I.Zero);

				var resource = map.GetResource(x, y);
				if (resource != ResourceType.None)
					ResourceLayer?.SetCell(cell, (int)resource - 1, Vector2I.Zero);
			}
		}
	}
}
