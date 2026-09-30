using System.Collections.Generic;

namespace FactoryTD.Sim;

public enum UnitType : byte
{
	PlasticSoldier,
	BrickGolem,
	RcCar,
	Builder,
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

	/// <summary>Work one builder puts into a construction site per tick.</summary>
	public const int BuilderWorkPerTick = 1;

	public static UnitDef Def(UnitType type) => type switch
	{
		UnitType.BrickGolem => BrickGolem,
		UnitType.RcCar => RcCar,
		UnitType.Builder => Builder,
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
	internal int AttackCooldown { get; set; }

	// Builders only: the site it is working on and the tiles left to walk there.
	public Building Job { get; internal set; }
	internal List<(int X, int Y)> Path { get; set; }
	internal int PathIndex { get; set; }

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
	}
}
