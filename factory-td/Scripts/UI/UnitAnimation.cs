using System;
using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>Which way an upright unit is drawn: towards the camera (south), away (north), right or left.</summary>
public enum Facing
{
	Toward,
	Away,
	Right,
	Left,
}

public enum PoseKind
{
	Idle,
	Walk, // Step 0..3
	Act,  // Step 0..1: working (hammering, picking) or attacking
}

/// <summary>One picture of a unit: which way it looks, what it does, and which step of that.</summary>
public readonly record struct UnitFrame(Facing Facing, PoseKind Kind, int Step);

/// <summary>
/// Turns what units do in the simulation into animation frames. Call <see cref="Observe"/> once per frame:
/// - facing follows where the unit looks (it walks or aims that way); near a diagonal it keeps the old
///   facing (hysteresis), so a unit walking diagonally doesn't flicker between two pictures;
/// - walk steps follow the distance actually walked, so the feet match the speed (no sliding);
/// - it acts while it attacks (just after a shot or punch) or works (a builder at its site, a farmer
///   harvesting); it idles once it has stood still for a moment.
/// </summary>
public sealed class UnitAnimation
{
	/// <summary>Sub-tile distance per walk step: four steps (one stride pair) per tile.</summary>
	public const int StepLength = UnitStats.SubTile / 4;

	/// <summary>Ticks without moving before a unit counts as standing.</summary>
	public const int StillTicks = 4;

	/// <summary>Ticks the attack pose shows after each shot or punch (split over the two action steps).</summary>
	public const int AttackPoseTicks = 6;

	/// <summary>Ticks per action step while working (the hammer's rhythm).</summary>
	public const int WorkStepTicks = 5;

	/// <summary>One axis must be this much longer than the other to change facing near a diagonal.</summary>
	private const int Hysteresis = 5; // in quarters: 5/4 = 1.25

	private readonly Dictionary<int, State> _states = new();
	private long _tick = -1;

	private sealed class State
	{
		public Facing Facing = Facing.Toward;
		public int LastX, LastY;
		public long Walked;
		public long MovedAt = long.MinValue / 2;
	}

	public void Observe(World world)
	{
		if (world.TickCount == _tick)
			return;
		_tick = world.TickCount;
		foreach (var unit in world.Units)
		{
			if (!_states.TryGetValue(unit.Id, out var state))
			{
				// New (usually just out of a factory and on its way): counts as moving for a moment.
				_states[unit.Id] = state = new State { LastX = unit.X, LastY = unit.Y, MovedAt = _tick };
				state.Facing = FacingFor(unit.MoveX, unit.MoveY, Facing.Toward);
				continue;
			}
			int dx = unit.X - state.LastX, dy = unit.Y - state.LastY;
			if (dx != 0 || dy != 0)
			{
				state.Walked += IntMath.Sqrt(dx * dx + dy * dy);
				state.MovedAt = _tick;
				state.LastX = unit.X;
				state.LastY = unit.Y;
			}
			state.Facing = FacingFor(unit.MoveX, unit.MoveY, state.Facing);
		}
		if (_states.Count > world.Units.Count)
		{
			var alive = world.Units.Select(u => u.Id).ToHashSet();
			foreach (int gone in _states.Keys.Where(id => !alive.Contains(id)).ToList())
				_states.Remove(gone);
		}
	}

	public UnitFrame FrameOf(Unit unit)
	{
		if (!_states.TryGetValue(unit.Id, out var state))
			return new UnitFrame(Facing.Toward, PoseKind.Idle, 0);
		int attackTicks = UnitStats.AttackTicks(unit.Type);
		int sinceAttack = attackTicks - unit.AttackCooldown;
		if (unit.AttackCooldown > 0 && sinceAttack < AttackPoseTicks)
			return new UnitFrame(state.Facing, PoseKind.Act, sinceAttack * 2 / AttackPoseTicks);
		bool moving = _tick - state.MovedAt <= StillTicks;
		if (!moving && IsWorking(unit))
			return new UnitFrame(state.Facing, PoseKind.Act, (int)(_tick / WorkStepTicks % 2));
		if (moving)
			return new UnitFrame(state.Facing, PoseKind.Walk, (int)(state.Walked / StepLength % 4));
		return new UnitFrame(state.Facing, PoseKind.Idle, 0);
	}

	/// <summary>A builder at an unfinished site, or a farmer in the middle of a harvest.</summary>
	public static bool IsWorking(Unit unit) => unit.Type switch
	{
		UnitType.Builder => unit.Job is { IsBuilt: false } && (unit.MoveX != 0 || unit.MoveY != 0),
		UnitType.Farmer => unit.WorkTimer > 0,
		_ => false,
	};

	/// <summary>The facing for looking along (dx, dy); near a diagonal the previous facing stays if it still fits.</summary>
	public static Facing FacingFor(int dx, int dy, Facing previous)
	{
		if (dx == 0 && dy == 0)
			return previous;
		long ax = Math.Abs((long)dx), ay = Math.Abs((long)dy);
		var side = dx > 0 ? Facing.Right : Facing.Left;
		var vertical = dy > 0 ? Facing.Toward : Facing.Away;
		if (ax * 4 > ay * Hysteresis)
			return side;
		if (ay * 4 > ax * Hysteresis)
			return vertical;
		// Near the diagonal: keep looking the same way if that is one of the two candidates.
		return previous == side || previous == vertical ? previous : (ax >= ay ? side : vertical);
	}
}
