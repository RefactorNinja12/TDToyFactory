using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the claw cranes' rails out over the field (one piece per tile, on stilts, over whatever is below) and
/// the claw on its trolley at its interpolated place along the rail: open on the way out, closed with the item
/// hanging under it on the way back.
/// </summary>
public partial class CraneView : Node2D
{
	private const float CarriedSize = 22f;

	private World _world;
	private int _localPlayer;

	/// <summary>How far between two ticks (0..1), for smooth claw movement.</summary>
	public float Alpha { get; set; }

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
		TextureFilter = TextureFilterEnum.Nearest;
		ZIndex = 1; // up on stilts: over buildings and units, under the fog
	}

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (_world == null)
			return;
		const float ts = BuildingVisuals.TileSize;
		var rail = BuildingVisuals.GetPartTexture("crane_rail");
		var open = BuildingVisuals.GetPartTexture("claw_open");
		var closed = BuildingVisuals.GetPartTexture("claw_closed");
		foreach (var building in _world.Buildings)
		{
			if (building is not ClawCrane crane || !crane.IsBuilt || !Knowledge.ShowBuilding(_world, _localPlayer, crane))
				continue;
			var forward = new Vector2(crane.Facing.DX(), crane.Facing.DY());
			var home = BuildingVisuals.FootprintCenter(crane);
			float turn = BuildingVisuals.Rotation(crane.Facing);
			for (int d = 1; d <= CraneStats.Reach; d++)
			{
				DrawSetTransform(home + forward * d * ts, turn);
				DrawTexture(rail, -rail.GetSize() / 2);
			}
			float along = Mathf.Lerp(crane.PrevClaw, crane.Claw, Alpha) * ts / UnitStats.SubTile;
			var at = home + forward * along;
			bool holding = crane.Carrying != ItemType.None;
			if (holding)
			{
				DrawSetTransform(at, 0);
				var item = BuildingVisuals.GetItemTexture(crane.Carrying);
				DrawTextureRect(item, new Rect2(-CarriedSize / 2, -CarriedSize / 2 + 4, CarriedSize, CarriedSize), false);
			}
			var claw = holding || crane.State == ClawState.Grab ? closed : open;
			DrawSetTransform(at, turn);
			DrawTexture(claw, -claw.GetSize() / 2);
		}
		DrawSetTransform(Vector2.Zero, 0);
	}
}
