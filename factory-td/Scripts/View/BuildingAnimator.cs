using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the moving parts of buildings (UI/BuildingParts) over their still sprites: posed by each
/// building's clock, which runs while the building works (UI/Activity) and eases to a stop when it
/// doesn't. Parts turn with the building. Hidden (fog) and off-screen buildings are skipped.
/// </summary>
public partial class BuildingAnimator : Node2D
{
	private readonly Activity _activity = new();
	private readonly AnimationClocks _clocks = new();
	private World _world;
	private int _localPlayer;
	private float _delta;

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
		TextureFilter = TextureFilterEnum.Nearest;
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;
		_delta = (float)delta;
		_activity.Observe(_world);
		_clocks.Keep(_world.Buildings);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_world == null)
			return;
		var screen = GetViewportRect();
		var toWorld = GetCanvasTransform().AffineInverse();
		var visible = new Rect2(toWorld * screen.Position, toWorld.BasisXform(screen.Size)).Abs().Grow(BuildingVisuals.TileSize * 2);
		foreach (var building in _world.Buildings)
		{
			var parts = BuildingParts.For(building.Type);
			if (parts.Count == 0 || !building.IsBuilt)
				continue;
			var center = BuildingVisuals.FootprintCenter(building);
			if (!visible.HasPoint(center) || !Knowledge.ShowBuilding(_world, _localPlayer, building))
				continue;
			float time = _clocks.Advance(building, _activity.IsWorking(building), _delta);
			float wall = Time.GetTicksMsec() / 1000f + building.X * 0.37f + building.Y * 0.61f;
			// Side-view buildings (the ship) stay upright and only mirror when facing west.
			var place = BuildingParts.KeepsUpright(building.Type)
				? new Transform2D(0, new Vector2(building.Facing == Direction.West ? -1 : 1, 1), 0, center)
				: new Transform2D(BuildingVisuals.Rotation(building.Facing), center);
			foreach (var part in parts)
			{
				var pose = BuildingParts.PoseAt(part, BuildingParts.TimeFor(part, time, wall));
				var texture = BuildingVisuals.GetPartTexture(part.Sprite);
				DrawSetTransformMatrix(place * new Transform2D(pose.Angle, new Vector2(pose.Scale, pose.Scale), 0, new Vector2(pose.X, pose.Y)));
				DrawTexture(texture, -texture.GetSize() / 2, new Color(1, 1, 1, pose.Alpha));
			}
		}
		DrawSetTransform(Vector2.Zero, 0);
	}
}
