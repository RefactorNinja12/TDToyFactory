using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the big toys lying on the map (MapLayout.Obstacles). Seen slightly from the front, so each picture
/// covers its footprint plus one tile above it; drawn over units (a unit walking behind a toy is partly
/// hidden) and under the fog.
/// </summary>
public partial class ObstacleView : Node2D
{
	private const float T = BuildingVisuals.TileSize;

	private World _world;
	private Texture2D _teddy, _blocks, _doll, _sock;

	public void Bind(World world)
	{
		_world = world;
		_teddy = GD.Load<Texture2D>("res://Assets/Sprites/Obstacles/teddy.png");
		_blocks = GD.Load<Texture2D>("res://Assets/Sprites/Obstacles/blocks.png");
		_doll = GD.Load<Texture2D>("res://Assets/Sprites/Obstacles/doll.png");
		_sock = GD.Load<Texture2D>("res://Assets/Sprites/Obstacles/sock.png");
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
			var texture = toy.Kind switch
			{
				ObstacleKind.TeddyBear => _teddy,
				ObstacleKind.AbcBlocks => _blocks,
				ObstacleKind.Sock => _sock,
				_ => _doll,
			};
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
