using System.Collections.Generic;
using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>Textures, names and grid helpers shared by the building view, ghost and menu.</summary>
public static class BuildingVisuals
{
	public const int TileSize = 64;

	private static readonly Dictionary<BuildingType, string> TexturePaths = new()
	{
		[BuildingType.Conveyor] = "res://Assets/Sprites/Conveyors/conveyor_straight.png",
		[BuildingType.BrickExtractor] = "res://Assets/Sprites/Buildings/extractor_brick.png",
		[BuildingType.PlasticExtractor] = "res://Assets/Sprites/Buildings/extractor_plastic.png",
		[BuildingType.BatteryExtractor] = "res://Assets/Sprites/Buildings/extractor_battery.png",
		[BuildingType.Core] = "res://Assets/Sprites/Buildings/core_toybox.png",
		[BuildingType.Splitter] = "res://Assets/Sprites/Buildings/splitter.png",
		[BuildingType.Sorter] = "res://Assets/Sprites/Buildings/sorter.png",
		[BuildingType.Assembler] = "res://Assets/Sprites/Buildings/assembler.png",
		[BuildingType.SoldierFactory] = "res://Assets/Sprites/Buildings/factory_soldier.png",
		[BuildingType.FoamTower] = "res://Assets/Sprites/Towers/tower_foam.png",
		[BuildingType.Catapult] = "res://Assets/Sprites/Towers/tower_catapult.png",
		[BuildingType.WaterTower] = "res://Assets/Sprites/Towers/tower_water.png",
		[BuildingType.LaserTower] = "res://Assets/Sprites/Towers/tower_laser.png",
		[BuildingType.Junction] = "res://Assets/Sprites/Buildings/junction.png",
		[BuildingType.Toolbox] = "res://Assets/Sprites/Buildings/toolbox.png",
		[BuildingType.Warehouse] = "res://Assets/Sprites/Buildings/warehouse.png",
		[BuildingType.CropField] = "res://Assets/Sprites/Farming/field_3_ripe.png",
		[BuildingType.Farmhouse] = "res://Assets/Sprites/Buildings/farmhouse.png",
		[BuildingType.Kitchen] = "res://Assets/Sprites/Buildings/kitchen.png",
		[BuildingType.GolemWorkshop] = "res://Assets/Sprites/Buildings/factory_golem.png",
		[BuildingType.CarFactory] = "res://Assets/Sprites/Buildings/factory_car.png",
		[BuildingType.Pylon] = "res://Assets/Sprites/Power/pylon.png",
		[BuildingType.BatteryCharger] = "res://Assets/Sprites/Power/charger.png",
		[BuildingType.Lamp] = "res://Assets/Sprites/Fog/lamp.png",
		[BuildingType.Tent] = "res://Assets/Sprites/Fog/tent.png",
		[BuildingType.CheeseMelter] = "res://Assets/Sprites/Buildings/cheese_melter.png",
		[BuildingType.Treadmill] = "res://Assets/Sprites/Buildings/treadmill.png",
	};

	/// <summary>Build menu tabs, in order.</summary>
	public static readonly (string Name, BuildingType[] Types)[] MenuCategories =
	{
		("Logistik", new[] { BuildingType.Conveyor, BuildingType.Splitter, BuildingType.Sorter, BuildingType.Junction }),
		("Produktion", new[]
		{
			BuildingType.BrickExtractor, BuildingType.PlasticExtractor, BuildingType.BatteryExtractor, BuildingType.CheeseMelter,
			BuildingType.Assembler, BuildingType.Toolbox,
		}),
		("Ström", new[] { BuildingType.BatteryCharger, BuildingType.Pylon }),
		("Utforska", new[] { BuildingType.Tent, BuildingType.Lamp }),
		("Mat", new[] { BuildingType.CropField, BuildingType.Farmhouse, BuildingType.Kitchen, BuildingType.Warehouse }),
		("Armé", new[] { BuildingType.SoldierFactory, BuildingType.GolemWorkshop, BuildingType.CarFactory, BuildingType.Treadmill }),
		("Försvar", new[] { BuildingType.FoamTower, BuildingType.Catapult, BuildingType.WaterTower, BuildingType.LaserTower }),
	};

	private static readonly Dictionary<UnitType, string> UnitTexturePaths = new()
	{
		[UnitType.PlasticSoldier] = "res://Assets/Sprites/Units/soldier.png",
		[UnitType.BrickGolem] = "res://Assets/Sprites/Units/golem.png",
		[UnitType.RcCar] = "res://Assets/Sprites/Units/rc_car.png",
		[UnitType.Builder] = "res://Assets/Sprites/Units/builder.png",
		[UnitType.Farmer] = "res://Assets/Sprites/Units/farmer.png",
		[UnitType.Scout] = "res://Assets/Sprites/Units/scout.png",
		[UnitType.CheeseHunter] = "res://Assets/Sprites/Units/cheese_hunter.png",
	};

	public static Texture2D GetUnitTexture(UnitType type) => Load(UnitTexturePaths[type]);

	private static readonly Dictionary<ItemType, string> ItemTexturePaths = new()
	{
		[ItemType.Brick] = "res://Assets/Sprites/Resources/brick.png",
		[ItemType.Plastic] = "res://Assets/Sprites/Resources/plastic.png",
		[ItemType.Battery] = "res://Assets/Sprites/Resources/battery.png",
		[ItemType.Gear] = "res://Assets/Sprites/Items/gear.png",
		[ItemType.Spring] = "res://Assets/Sprites/Items/spring.png",
		[ItemType.CircuitBoard] = "res://Assets/Sprites/Items/circuit_board.png",
		[ItemType.Crop] = "res://Assets/Sprites/Items/crop.png",
		[ItemType.Food] = "res://Assets/Sprites/Items/food.png",
		[ItemType.MeltedCheese] = "res://Assets/Sprites/Items/melted_cheese.png",
	};

	private static readonly Dictionary<string, Texture2D> Cache = new();

	/// <summary>Drawn turning from east (entering at the west edge) to south (leaving at the south edge).</summary>
	public static Texture2D ConveyorCurveTexture => Load("res://Assets/Sprites/Conveyors/conveyor_curve.png");

	public static Texture2D GetTexture(BuildingType type) => Load(TexturePaths[type]);

	public static Texture2D GetItemTexture(ItemType type) => Load(ItemTexturePaths[type]);

	/// <summary>A moving part drawn over a building (UI/BuildingParts), from Assets/Sprites/Parts.</summary>
	public static Texture2D GetPartTexture(string name) => Load($"res://Assets/Sprites/Parts/{name}.png");

	/// <summary>Crop field look for growth stage 0 (just planted) .. 3 (ripe).</summary>
	public static Texture2D CropFieldTexture(int stage) => Load(stage switch
	{
		0 => "res://Assets/Sprites/Farming/field_0_empty.png",
		1 => "res://Assets/Sprites/Farming/field_1_sprout.png",
		2 => "res://Assets/Sprites/Farming/field_2_growing.png",
		_ => "res://Assets/Sprites/Farming/field_3_ripe.png",
	});

	private static Texture2D Load(string path)
	{
		if (!Cache.TryGetValue(path, out var texture))
			Cache[path] = texture = GD.Load<Texture2D>(path);
		return texture;
	}

	/// <summary>Unit vector for a direction, in Godot's y-down space.</summary>
	public static Vector2 ToVector(Direction d) => new(d.DX(), d.DY());

	/// <summary>All sprites are drawn facing east (rotation 0).</summary>
	public static float Rotation(Direction facing) => (int)facing * Mathf.Pi / 2f;

	public static Vector2 CellCenter(int x, int y) =>
		new((x + 0.5f) * TileSize, (y + 0.5f) * TileSize);

	/// <summary>Centre of all tiles a building covers.</summary>
	public static Vector2 FootprintCenter(Building building) =>
		new((building.X + building.Width / 2f) * TileSize, (building.Y + building.Height / 2f) * TileSize);

	public static Vector2I WorldToCell(Vector2 position) =>
		new(Mathf.FloorToInt(position.X / TileSize), Mathf.FloorToInt(position.Y / TileSize));
}
