namespace FactoryTD.Sim;

/// <summary>What a shot does. Each unit armor class is weak to one kind (see Combat.DamagePercent).</summary>
public enum DamageKind : byte
{
	Foam,   // single target, no bonus
	Area,   // splash; strong vs plastic
	Water,  // single target; strong vs electronics
	Laser,  // single target; strong vs bricks
	Bullet, // soldiers and cars
	Punch,  // golems (short range)
}

public sealed record TowerStats(
	ItemType Ammo,
	int ShotsPerItem,     // one ammo item gives this many shots
	int MaxShots,         // magazine size
	int ReloadTicks,
	int RangeTiles,
	int Damage,
	DamageKind Kind,
	int AreaRadius,       // sub-tile units, Area only
	int TravelTicks);     // projectile flight time

/// <summary>
/// Takes its ammo item from belts on any side into a magazine and shoots the nearest enemy unit in range.
/// No ammo, no shooting.
/// </summary>
public sealed class Tower : Building
{
	// ---- Balance: towers ----
	public static TowerStats StatsFor(BuildingType type) => type switch
	{
		BuildingType.FoamTower => FoamStats,
		BuildingType.Catapult => CatapultStats,
		BuildingType.LaserTower => LaserStats,
		_ => WaterStats,
	};

	private static readonly TowerStats LaserStats = new(
		ItemType.Battery, ShotsPerItem: 5, MaxShots: 30, ReloadTicks: 15, RangeTiles: 7,
		Damage: 8, DamageKind.Laser, AreaRadius: 0, TravelTicks: 2);

	private static readonly TowerStats FoamStats = new(
		ItemType.Plastic, ShotsPerItem: 4, MaxShots: 40, ReloadTicks: 10, RangeTiles: 6,
		Damage: 6, DamageKind.Foam, AreaRadius: 0, TravelTicks: 6);

	private static readonly TowerStats CatapultStats = new(
		ItemType.Brick, ShotsPerItem: 1, MaxShots: 10, ReloadTicks: 40, RangeTiles: 8,
		Damage: 12, DamageKind.Area, AreaRadius: UnitStats.SubTile * 5 / 4, TravelTicks: 16);

	private static readonly TowerStats WaterStats = new(
		ItemType.Battery, ShotsPerItem: 10, MaxShots: 60, ReloadTicks: 4, RangeTiles: 4,
		Damage: 2, DamageKind.Water, AreaRadius: 0, TravelTicks: 3);

	private int _cooldown;

	public TowerStats Stats { get; }
	public int Shots { get; private set; }

	/// <summary>Direction to the last target (sub-tile units), for turning the sprite.</summary>
	public int AimX { get; private set; } = 1;
	public int AimY { get; private set; }

	public Tower(BuildingType type, int x, int y, Direction facing, int owner)
		: base(type, x, y, facing, owner)
	{
		Stats = StatsFor(type);
		AimX = facing.DX();
		AimY = facing.DY();
	}

	protected override void HashState(ref StateHash hash)
	{
		hash.Add(Shots); hash.Add(_cooldown); hash.Add(AimX); hash.Add(AimY);
	}

	public override bool TryAccept(ItemType item, Direction moving)
	{
		if (item != Stats.Ammo || Shots + Stats.ShotsPerItem > Stats.MaxShots)
			return false;
		Shots += Stats.ShotsPerItem;
		return true;
	}

	public override void Tick(World world)
	{
		if (_cooldown > 0)
		{
			_cooldown--;
			return;
		}
		NoPower = !world.HasPower(this, PowerStats.TowerShotEnergy);
		if (Shots == 0 || NoPower)
			return;

		var target = world.FindNearestEnemy(Owner, CenterX, CenterY, Stats.RangeTiles * UnitStats.SubTile);
		if (target == null || !world.TryDrawPower(this, PowerStats.TowerShotEnergy))
			return;

		Shots--;
		_cooldown = Stats.ReloadTicks;
		AimX = target.X - CenterX;
		AimY = target.Y - CenterY;
		world.Fire(new Projectile(Stats, Owner, CenterX, CenterY, target));
	}
}

/// <summary>A shot in flight. Single-target shots follow their target; area shots land where the target was.</summary>
public sealed class Projectile
{
	public DamageKind Kind { get; }
	public int Owner { get; }
	public int Damage { get; }
	public int AreaRadius { get; }
	public int FromX { get; }
	public int FromY { get; }
	public int ToX { get; internal set; }
	public int ToY { get; internal set; }
	public int TotalTicks { get; }
	public int TicksLeft { get; internal set; }

	/// <summary>What a single-target shot is aimed at: a unit or a building (one of them is null).</summary>
	public Unit TargetUnit { get; }
	public Building TargetBuilding { get; }

	public Projectile(DamageKind kind, int owner, int damage, int areaRadius, int travelTicks,
		int fromX, int fromY, Unit targetUnit, Building targetBuilding)
	{
		Kind = kind;
		Owner = owner;
		Damage = damage;
		AreaRadius = areaRadius;
		FromX = fromX;
		FromY = fromY;
		TargetUnit = targetUnit;
		TargetBuilding = targetBuilding;
		(ToX, ToY) = targetUnit != null ? (targetUnit.X, targetUnit.Y) : (targetBuilding.CenterX, targetBuilding.CenterY);
		TotalTicks = TicksLeft = travelTicks;
	}

	public Projectile(TowerStats stats, int owner, int fromX, int fromY, Unit target)
		: this(stats.Kind, owner, stats.Damage, stats.AreaRadius, stats.TravelTicks, fromX, fromY, target, null) { }
}

public static class Combat
{
	/// <summary>Damage in percent for a damage kind against an armor class. The counters from the design doc.</summary>
	public static int DamagePercent(DamageKind kind, ArmorClass armor) => (kind, armor) switch
	{
		// Each armor class has one weakness...
		(DamageKind.Area, ArmorClass.Plastic) => 200,
		(DamageKind.Water, ArmorClass.Electronic) => 300,
		(DamageKind.Laser, ArmorClass.Brick) => 200,
		// ...and bricks shrug off the light stuff, so golems need lasers (or catapults).
		(DamageKind.Foam or DamageKind.Water or DamageKind.Bullet, ArmorClass.Brick) => 50,
		_ => 100,
	};
}
