using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Animates treadmills on top of their (still) building sprite: the belt's slats roll backwards, and while a
/// builder mouse is training it is drawn running on the belt towards the cheese, bobbing in step.
/// Positions are in the sprite's own pixels (128x128, belt at x 18..98, y 40..92 in
/// Assets/Sprites/Buildings/treadmill.png) and turn with the building like the sprite does.
/// </summary>
public partial class TreadmillView : Node2D
{
	private const float BeltLeft = 24 - 64, BeltRight = 92 - 64;   // between the rollers
	private const float BeltTop = 41 - 64, BeltBottom = 91 - 64;
	private const float SlatSpacing = 10f;
	private const float RunSpeed = 70f;                             // belt pixels per second while a mouse runs
	private const float StepsPerSecond = 10f;                       // running steps on the belt

	private static readonly Color Slat = new(0.29f, 0.29f, 0.35f);
	private static readonly Color SlatEdge = new(0.43f, 0.43f, 0.5f);

	private World _world;
	private int _localPlayer;
	private Texture2D _mouse;
	private float _beltOffset;

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
		_mouse = BuildingVisuals.GetUnitSheet(UnitType.Builder);
		TextureFilter = TextureFilterEnum.Nearest;
	}

	public override void _Process(double delta)
	{
		_beltOffset = (_beltOffset + (float)delta * RunSpeed) % SlatSpacing;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_world == null)
			return;
		float time = Time.GetTicksMsec() / 1000f;
		foreach (var building in _world.Buildings)
		{
			if (building is not Treadmill mill || !mill.IsBuilt || !Knowledge.ShowBuilding(_world, _localPlayer, mill))
				continue;
			DrawSetTransform(BuildingVisuals.FootprintCenter(mill), BuildingVisuals.Rotation(mill.Facing));

			// Slats: still while nobody trains, rolling back under the runner while someone does.
			float offset = mill.HasTrainee ? _beltOffset : 0f;
			for (float x = BeltRight - offset; x > BeltLeft; x -= SlatSpacing)
			{
				DrawLine(new Vector2(x, BeltTop), new Vector2(x, BeltBottom), Slat, 2f);
				DrawLine(new Vector2(x + 2, BeltTop), new Vector2(x + 2, BeltBottom), SlatEdge, 1f);
			}

			if (mill.HasTrainee && _mouse != null)
			{
				// Running towards the cheese with its side walk frames, upright (not turned with the building):
				// the belt spot follows the building's turn, the mouse only looks left or right.
				int step = (int)(time * StepsPerSecond + mill.X) % 4;
				var frame = new UnitFrame(mill.Facing == Direction.West ? Facing.Left : Facing.Right, PoseKind.Walk, step);
				var (column, row, mirror) = UnitSheets.Cell(frame);
				float cell = UnitSheets.CellSize(UnitType.Builder);
				var spot = GetTransformFor(mill) * new Vector2(-14f, 14f); // the feet on the belt
				DrawSetTransform(spot, 0);
				var source = new Rect2(column * cell, row * cell, cell, cell);
				float feet = UnitSheets.FeetY(UnitType.Builder);
				var target = mirror ? new Rect2(cell / 2, -feet, -cell, cell) : new Rect2(-cell / 2, -feet, cell, cell);
				DrawTextureRectRegion(_mouse, target, source, UnitView.PlayerTints[mill.Owner % UnitView.PlayerTints.Length]);
			}
		}
		DrawSetTransform(Vector2.Zero, 0);
	}

	private static Transform2D GetTransformFor(Building building) =>
		new(BuildingVisuals.Rotation(building.Facing), BuildingVisuals.FootprintCenter(building));
}
