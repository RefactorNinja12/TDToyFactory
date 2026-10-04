using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the big toys (nursery) or giant plants (garden) lying on the map (MapLayout.Obstacles). Seen slightly from the front, so each picture
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
		foreach (var toy in _world.Map.Obstacles)
		{
			// From BuildingVisuals' cache, which holds on to it: a texture only loaded here would be freed by the
			// garbage collector after drawing, and the toy would turn into a white box.
			var texture = BuildingVisuals.GetObstacleTexture(toy.Kind);
			// Footprint plus one tile of the toy's front/top showing above it.
			var rect = new Rect2(toy.X * T, (toy.Y - 1) * T, toy.Width * T, (toy.Height + 1) * T);
			// Mirrored toys (right room) are flipped, so both rooms look the same way round.
			bool flip = toy.X > _world.Map.Width / 2;
			if (flip)
				rect = new Rect2(rect.Position.X + rect.Size.X, rect.Position.Y, -rect.Size.X, rect.Size.Y);
			DrawTextureRect(texture, rect, false);
		}
	}
}
