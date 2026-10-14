using System.Collections.Generic;

namespace FactoryTD.Sim;

public enum UnitType : byte
{
	PlasticSoldier,
	BrickGolem,
	RcCar,
	Builder,
	Farmer,
	Scout,
	CheeseHunter,
	Samurai,
	Wizard,
}

/// <summary>What a unit is made of; decides which tower hurts it most.</summary>
public enum ArmorClass : byte
{
	Plastic,     // weak to area damage
	Brick,       // weak to lasers
	Electronic,  // weak to water
}

/// <summary>Everything that differs between unit types. Distances are in sub-tile units, times in ticks.</summary>
public sealed record UnitDef(
	ArmorClass Armor,
	int MaxHealth,
	int Speed,                 // per tick
	int Damage,
	int AttackTicks,           // time between shots
	int Range,                 // shoots within this
	int Sight,                 // walks over to targets within this
	int ShotTravelTicks,
	bool TargetsUnits,         // false: ignores enemy units and goes for buildings
	int BuildingDamagePercent, // damage to buildings, e.g. 200 for siege units
	ItemStack[] Recipe,
	int BuildTicks);

public static class UnitStats
{
	/// <summary>Positions are in sub-tile units: 256 per tile.</summary>
	public const int SubTile = 256;

	// ---- Balance: units ----
	private static readonly UnitDef PlasticSoldier = new(
		ArmorClass.Plastic, MaxHealth: 30, Speed: 24, Damage: 4, AttackTicks: 20,
		Range: SubTile * 3, Sight: SubTile * 7, ShotTravelTicks: 5,
		TargetsUnits: true, BuildingDamagePercent: 100,
		Recipe: new ItemStack[] { new(ItemType.Plastic, 3), new(ItemType.Spring, 1) }, BuildTicks: 100);

	// Slow tank that ignores units and smashes buildings. Weak to lasers, shrugs off darts/water/bullets.
	private static readonly UnitDef BrickGolem = new(
		ArmorClass.Brick, MaxHealth: 140, Speed: 14, Damage: 12, AttackTicks: 30,
		Range: SubTile * 3 / 2, Sight: SubTile * 7, ShotTravelTicks: 3,
		TargetsUnits: false, BuildingDamagePercent: 200,
		Recipe: new ItemStack[] { new(ItemType.Brick, 4), new(ItemType.Gear, 2) }, BuildTicks: 160);

	// Fast, fragile raider with a quick trigger. Weak to water.
	private static readonly UnitDef RcCar = new(
		ArmorClass.Electronic, MaxHealth: 22, Speed: 44, Damage: 3, AttackTicks: 8,
		Range: SubTile * 5 / 2, Sight: SubTile * 7, ShotTravelTicks: 4,
		TargetsUnits: true, BuildingDamagePercent: 100,
		Recipe: new ItemStack[] { new(ItemType.CircuitBoard, 1), new(ItemType.Gear, 2), new(ItemType.Battery, 1) }, BuildTicks: 120);

	// Wind-up builder robot: doesn't fight, walks to construction sites and builds them.
	private static readonly UnitDef Builder = new(
		ArmorClass.Plastic, MaxHealth: 20, Speed: 30, Damage: 0, AttackTicks: 20,
		Range: 0, Sight: 0, ShotTravelTicks: 1,
		TargetsUnits: false, BuildingDamagePercent: 0,
		Recipe: new ItemStack[] { new(ItemType.Plastic, 2), new(ItemType.Brick, 2) }, BuildTicks: 200);

	/// <summary>Builders each player starts with, and the most they can have.</summary>
	public const int StartingBuilders = 3;
	public const int MaxBuilders = 20;

	// Wind-up farmer figurine: doesn't fight, harvests crop fields and carries the crops home.
	private static readonly UnitDef Farmer = new(
		ArmorClass.Plastic, MaxHealth: 20, Speed: 28, Damage: 0, AttackTicks: 20,
		Range: 0, Sight: 0, ShotTravelTicks: 1,
		TargetsUnits: false, BuildingDamagePercent: 0,
		Recipe: new ItemStack[] { new(ItemType.Brick, 3) }, BuildTicks: 200);

	// Wind-up toy explorer from a tent: doesn't fight, walks the edge of the fog to find the map (and runs away).
	private static readonly UnitDef Scout = new(
		ArmorClass.Plastic, MaxHealth: 18, Speed: 36, Damage: 0, AttackTicks: 20,
		Range: 0, Sight: 0, ShotTravelTicks: 1,
		TargetsUnits: false, BuildingDamagePercent: 0,
		Recipe: new ItemStack[] { new(ItemType.Plastic, 3) }, BuildTicks: 160);

	// Cheese hunter: a builder mouse trained on a treadmill. Cheap, low tier, weaker than a soldier;
	// punches enemy units and buildings up close (it has to get right next to them).
	private static readonly UnitDef CheeseHunter = new(
		ArmorClass.Plastic, MaxHealth: 20, Speed: 28, Damage: 3, AttackTicks: 16,
		Range: SubTile * 2 / 3, Sight: SubTile * 6, ShotTravelTicks: 2,
		TargetsUnits: true, BuildingDamagePercent: 100,
		Recipe: new ItemStack[] { new(ItemType.MeltedCheese, 2) }, BuildTicks: 140);

	// Samurai mouse: a sword fighter that sweeps the arc in front of it and hits every enemy there (SamuraiStats).
	// The counter to crowds of small units; plastic armour, so area damage hurts, and golems shrug off the sword.
	private static readonly UnitDef Samurai = new(
		ArmorClass.Plastic, MaxHealth: 60, Speed: 26, Damage: 10, AttackTicks: 20,
		Range: SubTile * 13 / 10, Sight: SubTile * 7, ShotTravelTicks: 2,
		TargetsUnits: true, BuildingDamagePercent: 100,
		Recipe: new ItemStack[] { new(ItemType.Plastic, 3), new(ItemType.Spring, 1), new(ItemType.Gear, 1) }, BuildTicks: 180);

	// Wizard mouse: a cute old caster that calls a thunderbolt down on a spot (WizardStats): a small area, double against
	// plastic, units thrown back. Long range, slow to cast, fragile, and slightly expensive (end of the electronics chain).
	private static readonly UnitDef Wizard = new(
		ArmorClass.Plastic, MaxHealth: 22, Speed: 20, Damage: 8, AttackTicks: 45,
		Range: SubTile * 5, Sight: SubTile * 7, ShotTravelTicks: WizardStats.BoltDelayTicks,
		TargetsUnits: true, BuildingDamagePercent: 50,
		Recipe: new ItemStack[] { new(ItemType.Battery, 2), new(ItemType.CircuitBoard, 1), new(ItemType.Spring, 1) }, BuildTicks: 220);

	/// <summary>Most scouts a player can have, and how many each tent houses.</summary>
	public const int MaxScouts = 6;
	public const int ScoutsPerTent = 2;

	public const int StartingFarmers = 1;
	public const int MaxFarmers = 20;

	/// <summary>A farmhouse makes another farmer only while there are more than this many fields per farmer.</summary>
	public const int FieldsPerFarmer = 3;

	/// <summary>Ticks a farmer spends picking a ripe field.</summary>
	public const int HarvestTicks = World.TicksPerSecond * 2;

	// ---- Balance: upkeep ----
	/// <summary>Food each unit eats per minute.</summary>
	public static int FoodPerMinute(UnitType type) => type switch
	{
		UnitType.PlasticSoldier => 1,
		UnitType.BrickGolem => 2,
		UnitType.RcCar => 2,
		_ => 1, // builders, farmers
	};

	/// <summary>While starving, every unit loses StarveDamage health this often.</summary>
	public const int StarveTicks = World.TicksPerSecond * 3;
	public const int StarveDamage = 1;

	/// <summary>Worker units (builders, farmers) never fight and aren't counted as an army.</summary>
	public static bool IsWorker(UnitType type) => type is UnitType.Builder or UnitType.Farmer or UnitType.Scout;

	/// <summary>The most of this unit type a player can have, or int.MaxValue.</summary>
	public static int Cap(UnitType type) => type switch
	{
		UnitType.Builder => MaxBuilders,
		UnitType.Farmer => MaxFarmers,
		UnitType.Scout => MaxScouts,
		_ => int.MaxValue,
	};

	/// <summary>Work one builder puts into a construction site per tick.</summary>
	public const int BuilderWorkPerTick = 1;

	public static UnitDef Def(UnitType type) => type switch
	{
		UnitType.BrickGolem => BrickGolem,
		UnitType.RcCar => RcCar,
		UnitType.Builder => Builder,
		UnitType.Farmer => Farmer,
		UnitType.Scout => Scout,
		UnitType.CheeseHunter => CheeseHunter,
		UnitType.Samurai => Samurai,
		UnitType.Wizard => Wizard,
		_ => PlasticSoldier,
	};

	public static ArmorClass Armor(UnitType type) => Def(type).Armor;
	public static ItemStack[] Recipe(UnitType type) => Def(type).Recipe;
	public static int BuildTicks(UnitType type) => Def(type).BuildTicks;
	public static int MaxHealth(UnitType type) => Def(type).MaxHealth;
	public static int Speed(UnitType type) => Def(type).Speed;
	public static int Damage(UnitType type) => Def(type).Damage;
	public static int AttackTicks(UnitType type) => Def(type).AttackTicks;
	public static int Range(UnitType type) => Def(type).Range;
	public static int Sight(UnitType type) => Def(type).Sight;
	public static int ShotTravelTicks(UnitType type) => Def(type).ShotTravelTicks;
}

public sealed class Unit
{
	public int Id { get; }
	public UnitType Type { get; }
	public int Owner { get; }

	/// <summary>Centre position in sub-tile units (UnitStats.SubTile per tile).</summary>
	public int X { get; internal set; }
	public int Y { get; internal set; }

	/// <summary>Position before the last tick, for interpolation in the view.</summary>
	public int PrevX { get; internal set; }
	public int PrevY { get; internal set; }

	/// <summary>Last movement step, so the view can turn the sprite.</summary>
	public int MoveX { get; internal set; }
	public int MoveY { get; internal set; }

	public int Health { get; internal set; }

	/// <summary>Sword swings so far (samurai): the view slashes when it changes.</summary>
	public int SwingCount { get; internal set; }

	/// <summary>Shoved by a pendulum: moves (PushX, PushY) per tick for PushTicks ticks instead of acting.</summary>
	public int PushX { get; internal set; }
	public int PushY { get; internal set; }
	public int PushTicks { get; internal set; }

	/// <summary>Thrown by a golem's slam: the whole flight in ticks while it is in the air (0 on the ground).</summary>
	public int AirTicks { get; internal set; }

	/// <summary>Golems: ticks left of the slam's wind-up (hands up), and until the next slam.</summary>
	public int SlamTicks { get; internal set; }
	public int SlamCooldown { get; internal set; }
	internal int AttackCooldown { get; set; }

	/// <summary>Battery charge (golems and cars only, see PowerStats).</summary>
	public int Charge { get; internal set; }
	public UnitPower PowerState { get; internal set; }

	/// <summary>Direction index (Vision.DirectionIndex) it last moved or aimed in: where car headlights point.</summary>
	public int LightDirection { get; internal set; } = -1;

	// Scouts: the fog-edge tile it is heading for (-1 = none), and running away from an enemy.
	public int ScoutTargetX { get; internal set; } = -1;
	public int ScoutTargetY { get; internal set; } = -1;
	internal int FleeTicks { get; set; }
	internal int FleeX { get; set; }
	internal int FleeY { get; set; }

	// Workers: the building it is working on or walking to, and the tiles left to walk there.
	public Building Job { get; internal set; }
	internal List<(int X, int Y)> Path { get; set; }
	internal Building PathTarget { get; set; }
	internal int PathIndex { get; set; }
	internal int WorkTimer { get; set; }

	// Farmers: what they are carrying home.
	public ItemType Carrying { get; internal set; }
	public int CarryAmount { get; internal set; }

	public int TileX => X / UnitStats.SubTile;
	public int TileY => Y / UnitStats.SubTile;

	public Unit(int id, UnitType type, int owner, int x, int y)
	{
		Id = id;
		Type = type;
		Owner = owner;
		X = PrevX = x;
		Y = PrevY = y;
		Health = UnitStats.MaxHealth(type);
		Charge = PowerStats.MaxCharge(type);
	}
}

/// <summary>
/// The golem's ground slam: when an enemy unit comes close it raises both hands, then slams the ground. Every enemy unit
/// round it takes area damage and is thrown back, outwards from the golem. Distances in sub-tile units.
/// </summary>
public static class GolemStats
{
	public const int SlamTrigger = UnitStats.SubTile * 3 / 2;
	public const int SlamRadius = UnitStats.SubTile * 7 / 4;
	public const int SlamDamage = 5;
	public const int SlamMaxTargets = 5;
	public const int SlamWindupTicks = 8;
	public const int SlamCooldownTicks = 80;
	public const int ThrowDistance = UnitStats.SubTile * 3 / 2;
	public const int ThrowTicks = 8;

	/// <summary>Golems weigh more: thrown half as far (by a slam or a thunderbolt).</summary>
	public static int ThrowTicksFor(UnitType type, int ticks = ThrowTicks) => type == UnitType.BrickGolem ? ticks / 2 : ticks;
}

/// <summary>
/// The wizard's thunderbolt: cast at the spot where the target stands, it falls after <see cref="BoltDelayTicks"/> (time
/// to step aside) and hits every enemy unit within <see cref="BoltRadius"/>, throwing them back a little. Sub-tiles.
/// </summary>
public static class WizardStats
{
	public const int BoltDelayTicks = 6;
	public const int BoltRadius = UnitStats.SubTile * 9 / 10;
	public const int ThrowDistance = UnitStats.SubTile * 3 / 4;
	public const int ThrowTicks = 4;
}

/// <summary>A golem's slam or a thunderbolt striking: where (sub-tiles) and on which tick (for the view's effects).</summary>
public readonly record struct ImpactMark(int X, int Y, long Tick, bool Bolt);

/// <summary>The samurai's sword sweep. Distances in sub-tile units.</summary>
public static class SamuraiStats
{
	public const int SweepReach = UnitStats.SubTile * 8 / 5;
	public const int SweepMaxTargets = 5;

	/// <summary>Half the arc's width: cos² of 60°, as a fraction (dot² ≥ cos²·|v|²·|f|²).</summary>
	private const int CosSquaredNumerator = 1, CosSquaredDenominator = 4;

	/// <summary>Whether <paramref name="other"/> is in the arc the samurai sweeps when it strikes at the target.</summary>
	public static bool InArc(Unit samurai, Unit target, Unit other)
	{
		long fx = target.X - samurai.X, fy = target.Y - samurai.Y;
		long vx = other.X - samurai.X, vy = other.Y - samurai.Y;
		long v2 = vx * vx + vy * vy;
		if (v2 > (long)SweepReach * SweepReach)
			return false;
		long dot = vx * fx + vy * fy;
		if (v2 == 0)
			return true;
		return dot > 0 && CosSquaredDenominator * dot * dot >= CosSquaredNumerator * v2 * (fx * fx + fy * fy);
	}
}
