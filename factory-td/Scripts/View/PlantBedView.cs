using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// In the garden every giant plant grows out of a bed of dug soil with stones (Obstacles/plantbed.png), over its
/// footprint and half a tile round it, so the soil shows at the plant's edge. Flat on the ground: drawn under the
/// shadows (the plant's own shadow falls over its bed) and the plants (ObstacleView). Nothing on other maps.
/// </summary>
public partial class PlantBedView : Node2D
{
	private const float T = BuildingVisuals.TileSize;

	private MapLayout _map;

	public void Bind(MapLayout map)
	{
		_map = map;
		TextureFilter = TextureFilterEnum.Nearest;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_map is not { Theme: MapTheme.Garden })
			return;
		foreach (var plant in _map.Obstacles)
			DrawTextureRect(BuildingVisuals.PlantBedTexture, ObstacleView.Mirrored(plant, BedRect(plant), _map.Width), false);
	}

	/// <summary>A plant's bed: its footprint and half a tile round it.</summary>
	private static Rect2 BedRect(Obstacle plant) =>
		new Rect2(plant.X * T, plant.Y * T, plant.Width * T, plant.Height * T).Grow(T / 2);
}
