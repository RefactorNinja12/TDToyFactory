using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the big toys (nursery) or giant plants (garden, each in a bed of soil) lying on the map (MapLayout.Obstacles). Seen slightly from the front, so each picture
/// covers its footprint plus one tile above it; drawn over units (a unit walking behind a toy is partly
/// hidden) and under the fog.
/// </summary>
public partial class ObstacleView : Node2D
{
	private const float T = BuildingVisuals.TileSize;

	private World _world;

	public void Bind(World world)
	{
		_world = world;
		ZIndex = 1;
		TextureFilter = TextureFilterEnum.Nearest;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_world == null)
			return;
		// In the garden every plant grows out of a bed of dug soil: all beds first, so no plant lies under one.
		if (_world.Map.Theme == MapTheme.Garden)
			foreach (var plant in _world.Map.Obstacles)
				DrawTextureRect(BuildingVisuals.PlantBedTexture, Mirrored(plant, BedRect(plant)), false);
		foreach (var toy in _world.Map.Obstacles)
		{
			// From BuildingVisuals' cache, which holds on to it: a texture only loaded here would be freed by the
			// garbage collector after drawing, and the toy would turn into a white box.
			var texture = BuildingVisuals.GetObstacleTexture(toy.Kind);
			// Footprint plus one tile of the toy's front/top showing above it.
			var rect = new Rect2(toy.X * T, (toy.Y - 1) * T, toy.Width * T, (toy.Height + 1) * T);
			DrawTextureRect(texture, Mirrored(toy, rect), false);
		}
	}

	/// <summary>A plant's bed: its footprint and half a tile round it, so the soil shows at the plant's edge.</summary>
	private static Rect2 BedRect(Obstacle plant) =>
		new Rect2(plant.X * T, plant.Y * T, plant.Width * T, plant.Height * T).Grow(T / 2);

	/// <summary>Mirrored toys (right room) are flipped, so both rooms look the same way round.</summary>
	private Rect2 Mirrored(Obstacle toy, Rect2 rect) => toy.X > _world.Map.Width / 2
		? new Rect2(rect.Position.X + rect.Size.X, rect.Position.Y, -rect.Size.X, rect.Size.Y)
		: rect;
}
