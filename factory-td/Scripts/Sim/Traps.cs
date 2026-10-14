namespace FactoryTD.Sim;

/// <summary>Balance for traps. Times in ticks, distances in sub-tile units.</summary>
public static class TrapStats
{
	// ---- Trapdoor (2x2): closed, a warning blink, then open; enemies on it fall in ----
	public const int TrapdoorPeriod = 160;
	public const int TrapdoorWarnTicks = 30;
	public const int TrapdoorOpenTicks = 20;
	public const int TrapdoorWear = 10;        // per unit caught: 12 catches with 120 health

	// ---- Pendulum (4x1): the weight swings end to end, pauses, swings back ----
	public const int PendulumSwingTicks = 30;
	public const int PendulumPauseTicks = 10;
	public const int PendulumPeriod = 2 * (PendulumSwingTicks + PendulumPauseTicks);
	public const int PendulumHitRadius = UnitStats.SubTile * 3 / 5;
	public const int PushDistance = UnitStats.SubTile * 3;
	public const int PushTicks = 8;
	public const int PendulumDamage = 3;
	public const int PendulumWear = 5;         // per unit hit: 20 hits with 100 health

	public static bool IsTrap(BuildingType type) => type is BuildingType.Trapdoor or BuildingType.Pendulum;

	/// <summary>Golems are heavy: pushed half as far.</summary>
	public static int PushTicksFor(UnitType type) => type == UnitType.BrickGolem ? PushTicks / 2 : PushTicks;
}

/// <summary>
/// A trap: runs on a fixed timer from the moment it is finished. Enemies don't notice traps (they don't
/// target them and walk over them like floor); each catch wears the trap down until it breaks.
/// </summary>
public abstract class Trap : Building
{
	protected Trap(BuildingType type, int x, int y, Direction facing, int owner)
		: base(type, x, y, facing, owner)
	{
	}

	/// <summary>Ticks since it was finished.</summary>
	public int Phase { get; private set; }

	public override bool OutputsToward(Direction direction) => false;

	public override void Tick(World world)
	{
		Phase++;
		Spring(world);
	}

	protected abstract void Spring(World world);

	protected bool Covers(Unit unit) =>
		unit.TileX >= X && unit.TileX < X + Width && unit.TileY >= Y && unit.TileY < Y + Height;

	protected override void HashState(ref StateHash hash) => hash.Add(Phase);
}

public sealed class Trapdoor : Trap
{
	public Trapdoor(int x, int y, Direction facing, int owner)
		: base(BuildingType.Trapdoor, x, y, facing, owner)
	{
	}

	private int CycleTick => Phase % TrapStats.TrapdoorPeriod;

	public bool IsOpen => IsBuilt && CycleTick >= TrapStats.TrapdoorPeriod - TrapStats.TrapdoorOpenTicks;

	public bool IsWarning => IsBuilt && !IsOpen && CycleTick >= TrapStats.TrapdoorPeriod - TrapStats.TrapdoorOpenTicks - TrapStats.TrapdoorWarnTicks;

	protected override void Spring(World world)
	{
		if (!IsOpen)
			return;
		foreach (var unit in world.Units)
		{
			if (unit.Owner == Owner || unit.Health <= 0 || unit.AirTicks > 0 || !Covers(unit))
				continue; // a thrown unit flies over; it falls in only where it lands
			unit.Health = 0; // fell in
			world.WearTrap(this, TrapStats.TrapdoorWear);
		}
	}
}

public sealed class Pendulum : Trap
{
	public Pendulum(int x, int y, Direction facing, int owner)
		: base(BuildingType.Pendulum, x, y, facing, owner)
	{
	}

	private bool AlongX => Width > Height;

	/// <summary>How far the weight is along the arm, 0 (first tile) to 1000 (last tile).</summary>
	public int Swing
	{
		get
		{
			int t = Phase % TrapStats.PendulumPeriod;
			const int swing = TrapStats.PendulumSwingTicks, pause = TrapStats.PendulumPauseTicks;
			if (t < swing)
				return t * 1000 / swing;
			if (t < swing + pause)
				return 1000;
			if (t < 2 * swing + pause)
				return 1000 - (t - swing - pause) * 1000 / swing;
			return 0;
		}
	}

	/// <summary>+1 swinging towards the last tile, -1 back, 0 while it pauses at an end.</summary>
	public int SwingDirection
	{
		get
		{
			int t = Phase % TrapStats.PendulumPeriod;
			const int swing = TrapStats.PendulumSwingTicks, pause = TrapStats.PendulumPauseTicks;
			return t < swing ? 1 : t < swing + pause ? 0 : t < 2 * swing + pause ? -1 : 0;
		}
	}

	/// <summary>The weight's centre in sub-tiles.</summary>
	public (int X, int Y) Bob
	{
		get
		{
			const int half = UnitStats.SubTile / 2;
			int along = half + (System.Math.Max(Width, Height) - 1) * UnitStats.SubTile * Swing / 1000;
			return AlongX
				? (X * UnitStats.SubTile + along, Y * UnitStats.SubTile + half)
				: (X * UnitStats.SubTile + half, Y * UnitStats.SubTile + along);
		}
	}

	protected override void Spring(World world)
	{
		int direction = SwingDirection;
		if (direction == 0)
			return;
		var (bx, by) = Bob;
		long reach2 = (long)TrapStats.PendulumHitRadius * TrapStats.PendulumHitRadius;
		int step = TrapStats.PushDistance / TrapStats.PushTicks * direction;
		foreach (var unit in world.Units)
		{
			if (unit.Owner == Owner || unit.Health <= 0 || unit.PushTicks > 0)
				continue;
			long dx = unit.X - bx, dy = unit.Y - by;
			if (dx * dx + dy * dy > reach2)
				continue;
			unit.PushX = AlongX ? step : 0;
			unit.PushY = AlongX ? 0 : step;
			unit.PushTicks = TrapStats.PushTicksFor(unit.Type);
			unit.Health -= TrapStats.PendulumDamage;
			world.WearTrap(this, TrapStats.PendulumWear);
		}
	}
}
