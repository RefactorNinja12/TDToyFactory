using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws what traps do over their sprites: the trapdoor's hole while it is open and a blinking red border while
/// it warns, and the pendulum's weight where it is along the rail. Over the buildings, under the units.
/// </summary>
public partial class TrapView : Node2D
{
	private const int BlinkTicks = 4;

	private World _world;
	private int _localPlayer;
	private Texture2D _hole, _warn, _bob; // held here: textures only loaded inside _Draw get freed (white boxes)

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
		TextureFilter = TextureFilterEnum.Nearest;
		_hole = BuildingVisuals.GetPartTexture("trapdoor_open");
		_warn = BuildingVisuals.GetPartTexture("trapdoor_warn");
		_bob = BuildingVisuals.GetPartTexture("pendulum_bob");
	}

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (_world == null)
			return;
		const float ts = BuildingVisuals.TileSize;
		foreach (var building in _world.Buildings)
		{
			if (building is not Trap { IsBuilt: true } trap || !Knowledge.ShowBuilding(_world, _localPlayer, trap))
				continue;
			var center = BuildingVisuals.FootprintCenter(trap);
			switch (trap)
			{
				case Trapdoor { IsOpen: true }:
					DrawTexture(_hole, center - _hole.GetSize() / 2);
					break;
				case Trapdoor { IsWarning: true } when _world.TickCount / BlinkTicks % 2 == 0:
					DrawTexture(_warn, center - _warn.GetSize() / 2);
					break;
				case Pendulum pendulum:
					var (bx, by) = pendulum.Bob;
					var at = new Vector2(bx, by) * ts / UnitStats.SubTile;
					DrawTexture(_bob, at - _bob.GetSize() / 2);
					break;
			}
		}
	}
}
