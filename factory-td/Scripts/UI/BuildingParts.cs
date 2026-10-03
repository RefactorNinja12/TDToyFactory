using System;
using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

public enum Motion
{
	Spin,   // turns round: Speed turns per second (negative = the other way)
	Swing,  // rocks back and forth by Amount radians
	Bob,    // goes out along (DX, DY) and back
	Slide,  // moves from the anchor to anchor + (DX, DY), then starts over
	Orbit,  // goes round an ellipse with radii (DX, DY), facing the way it drives
	Blink,  // on for the first half of each beat, dim for the rest
	Puff,   // rises by (DX, DY), growing and fading (steam, bubbles)
}

/// <summary>
/// A moving piece drawn over a building's still sprite while it works. X/Y = anchor in the building sprite's
/// pixels from its centre (before the building turns); DX/DY = how far it travels. Speed = cycles per second.
/// Phase (0..1) lets several copies of one part run out of step.
/// </summary>
public sealed record Part(string Sprite, float X, float Y, Motion Motion, float Speed = 1,
	float DX = 0, float DY = 0, float Amount = 0, float Phase = 0);

/// <summary>Where a part is at a moment: offset from the building centre in sprite pixels, turn, size, opacity.</summary>
public readonly record struct Pose(float X, float Y, float Angle, float Scale, float Alpha);

/// <summary>The moving parts of each building and how they move. Pure: the view only draws the poses.</summary>
public static class BuildingParts
{
	// Drawn by tools/art/buildings/*.py; anchors are sprite pixels from the centre, east = the output side.
	private static readonly Dictionary<BuildingType, Part[]> Table = new()
	{
		[BuildingType.BrickExtractor] = new[]
		{
			new Part("brick_ride", 5, 0, Motion.Slide, Speed: 0.8f, DX: 17),
			new Part("digger_arm", -3, 4, Motion.Swing, Speed: 0.8f, Amount: 0.35f),
		},
		[BuildingType.PlasticExtractor] = new[]
		{
			new Part("gumball_beads", 2, -2, Motion.Spin, Speed: 0.5f),
			new Part("gumball_crank", 18, 8, Motion.Spin, Speed: 1f),
		},
		[BuildingType.BatteryExtractor] = new[]
		{
			new Part("charge_light", -14, 6, Motion.Blink, Speed: 1.5f),
			new Part("charge_light", -4, 6, Motion.Blink, Speed: 1.5f, Phase: 0.33f),
			new Part("charge_light", 6, 6, Motion.Blink, Speed: 1.5f, Phase: 0.66f),
			new Part("magnet", -5, -12, Motion.Bob, Speed: 0.6f, DY: 10),
		},
		[BuildingType.CheeseMelter] = new[]
		{
			new Part("fondue_bubble", -6, 1, Motion.Puff, Speed: 1.1f, DY: -4),
			new Part("fondue_bubble", 3, -6, Motion.Puff, Speed: 1.1f, DY: -4, Phase: 0.4f),
			new Part("fondue_bubble", 4, 4, Motion.Puff, Speed: 1.1f, DY: -4, Phase: 0.7f),
			new Part("fondue_spoon", -1, -2, Motion.Spin, Speed: 0.4f),
		},
		[BuildingType.Assembler] = new[]
		{
			new Part("windup_key", -23, 0, Motion.Spin, Speed: 0.35f),
			new Part("gear_big", 16, -18, Motion.Spin, Speed: -0.4f),
			new Part("gear_small", 17, 16, Motion.Spin, Speed: 0.57f),
		},
		[BuildingType.Kitchen] = new[]
		{
			new Part("pot_lid", -12, -10, Motion.Swing, Speed: 2.5f, Amount: 0.2f),
			new Part("steam", -12, -16, Motion.Puff, Speed: 0.7f, DY: -12),
			new Part("steam", -10, -16, Motion.Puff, Speed: 0.7f, DY: -12, Phase: 0.5f),
		},
		[BuildingType.SoldierFactory] = new[]
		{
			new Part("sprue", -20, 24, Motion.Slide, Speed: 0.35f, DX: 60),
			new Part("press_head", -6, -20, Motion.Bob, Speed: 0.7f, DY: 12),
			new Part("panel_light", 41, -37, Motion.Blink, Speed: 1.4f),
			new Part("panel_light", 49, -37, Motion.Blink, Speed: 1.4f, Phase: 0.5f),
			new Part("panel_light", 41, -29, Motion.Blink, Speed: 0.7f, Phase: 0.25f),
		},
		[BuildingType.GolemWorkshop] = new[]
		{
			new Part("crane_hook", 9, -22, Motion.Bob, Speed: 0.5f, DY: 10),
			new Part("mallet", -20, 30, Motion.Swing, Speed: 1.6f, Amount: 0.7f),
		},
		[BuildingType.CarFactory] = new[]
		{
			new Part("race_car_red", 0, 0, Motion.Orbit, Speed: 0.35f, DX: 44, DY: 36),
			new Part("race_car_yellow", 0, 0, Motion.Orbit, Speed: 0.35f, DX: 44, DY: 36, Phase: 0.5f),
			new Part("race_flag", 42, 34, Motion.Swing, Speed: 1.2f, Amount: 0.5f),
		},
		[BuildingType.Toolbox] = new[]
		{
			new Part("saw", -18, 37, Motion.Bob, Speed: 2f, DX: 6),
			new Part("mallet", 26, 36, Motion.Swing, Speed: 1.6f, Amount: 0.7f, Phase: 0.3f),
		},
		[BuildingType.Farmhouse] = new[]
		{
			new Part("pinwheel", 40, -48, Motion.Spin, Speed: 0.8f),
		},
	};

	private static readonly Part[] None = Array.Empty<Part>();

	public static IReadOnlyList<Part> For(BuildingType type) => Table.TryGetValue(type, out var parts) ? parts : None;

	public static IEnumerable<BuildingType> Animated => Table.Keys;

	/// <summary>Where <paramref name="part"/> is after <paramref name="seconds"/> of work.</summary>
	public static Pose PoseAt(Part part, float seconds)
	{
		float cycle = seconds * part.Speed + part.Phase;
		float f = cycle - MathF.Floor(cycle);     // 0..1 through the current cycle
		float wave = MathF.Sin(f * MathF.Tau);
		float x = part.X, y = part.Y, angle = 0, scale = 1, alpha = 1;
		switch (part.Motion)
		{
			case Motion.Spin:
				angle = cycle * MathF.Tau;
				break;
			case Motion.Swing:
				angle = part.Amount * wave;
				break;
			case Motion.Bob:
				float out_ = 0.5f - 0.5f * MathF.Cos(f * MathF.Tau);
				x += part.DX * out_;
				y += part.DY * out_;
				break;
			case Motion.Slide:
				x += part.DX * f;
				y += part.DY * f;
				break;
			case Motion.Orbit:
				x += part.DX * MathF.Cos(f * MathF.Tau);
				y += part.DY * MathF.Sin(f * MathF.Tau);
				angle = MathF.Atan2(part.DY * MathF.Cos(f * MathF.Tau), -part.DX * MathF.Sin(f * MathF.Tau));
				break;
			case Motion.Blink:
				alpha = f < 0.5f ? 1 : 0.25f;
				break;
			case Motion.Puff:
				x += part.DX * f;
				y += part.DY * f;
				scale = 0.6f + 0.6f * f;
				alpha = 1 - f;
				break;
		}
		return new Pose(x, y, angle, scale, alpha);
	}

	/// <summary>The rest pose (when the building stands still): where the motion starts.</summary>
	public static Pose Rest(Part part) => PoseAt(part with { Phase = 0 }, 0);
}

/// <summary>
/// Each building's animation time: runs while it works and slows to a stop when it doesn't (no jumps),
/// with a start offset from its position so a row of the same building doesn't move in lock step.
/// </summary>
public sealed class AnimationClocks
{
	/// <summary>Seconds to get up to speed or to come to a stop.</summary>
	public const float Ease = 0.3f;

	private readonly Dictionary<Building, (float Time, float Speed)> _clocks = new();

	public float Advance(Building building, bool working, float delta)
	{
		if (!_clocks.TryGetValue(building, out var clock))
			clock = ((building.X * 0.37f + building.Y * 0.61f) % 1f, 0);
		float speed = Math.Clamp(clock.Speed + (working ? delta : -delta) / Ease, 0, 1);
		clock = (clock.Time + delta * speed, speed);
		_clocks[building] = clock;
		return clock.Time;
	}

	/// <summary>Forgets buildings that are gone.</summary>
	public void Keep(IReadOnlyCollection<Building> standing)
	{
		if (_clocks.Count <= standing.Count)
			return;
		var set = new HashSet<Building>(standing);
		foreach (var gone in new List<Building>(_clocks.Keys))
			if (!set.Contains(gone))
				_clocks.Remove(gone);
	}
}
