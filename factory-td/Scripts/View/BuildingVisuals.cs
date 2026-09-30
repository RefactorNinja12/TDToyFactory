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
		[BuildingType.GolemWorkshop] = "res://Assets/Sprites/Buildings/factory_golem.png",
		[BuildingType.CarFactory] = "res://Assets/Sprites/Buildings/factory_car.png",
	};

	/// <summary>Build menu tabs, in order.</summary>
	public static readonly (string Name, BuildingType[] Types)[] MenuCategories =
	{
		("Logistik", new[] { BuildingType.Conveyor, BuildingType.Splitter, BuildingType.Sorter, BuildingType.Junction }),
		("Produktion", new[]
		{
			BuildingType.BrickExtractor, BuildingType.PlasticExtractor, BuildingType.BatteryExtractor,
			BuildingType.Assembler, BuildingType.Toolbox,
		}),
		("Armé", new[] { BuildingType.SoldierFactory, BuildingType.GolemWorkshop, BuildingType.CarFactory }),
		("Försvar", new[] { BuildingType.FoamTower, BuildingType.Catapult, BuildingType.WaterTower, BuildingType.LaserTower }),
	};

	private static readonly Dictionary<ItemType, string> ItemTexturePaths = new()
	{
		[ItemType.Brick] = "res://Assets/Sprites/Resources/brick.png",
		[ItemType.Plastic] = "res://Assets/Sprites/Resources/plastic.png",
		[ItemType.Battery] = "res://Assets/Sprites/Resources/battery.png",
		[ItemType.Gear] = "res://Assets/Sprites/Items/gear.png",
		[ItemType.Spring] = "res://Assets/Sprites/Items/spring.png",
		[ItemType.CircuitBoard] = "res://Assets/Sprites/Items/circuit_board.png",
	};

	private static readonly Dictionary<string, Texture2D> Cache = new();

	/// <summary>Drawn turning from east (entering at the west edge) to south (leaving at the south edge).</summary>
	public static Texture2D ConveyorCurveTexture => Load("res://Assets/Sprites/Conveyors/conveyor_curve.png");

	public static Texture2D GetTexture(BuildingType type) => Load(TexturePaths[type]);

	public static Texture2D GetItemTexture(ItemType type) => Load(ItemTexturePaths[type]);

	private static Texture2D Load(string path)
	{
		if (!Cache.TryGetValue(path, out var texture))
			Cache[path] = texture = GD.Load<Texture2D>(path);
		return texture;
	}

	/// <summary>Unit vector for a direction, in Godot's y-down space.</summary>
	public static Vector2 ToVector(Direction d) => new(d.DX(), d.DY());

	public static string DisplayName(BuildingType type) => type switch
	{
		BuildingType.Conveyor => "Transportband",
		BuildingType.BrickExtractor => "Klossgrävare",
		BuildingType.PlasticExtractor => "Plastsmältare",
		BuildingType.BatteryExtractor => "Batteriklo",
		BuildingType.Core => "Leksakslåda",
		BuildingType.Splitter => "Delare",
		BuildingType.Sorter => "Sorterare",
		BuildingType.Assembler => "Monteringsmaskin",
		BuildingType.SoldierFactory => "Soldatfabrik",
		BuildingType.FoamTower => "Skumpiltorn",
		BuildingType.Catapult => "Katapult",
		BuildingType.WaterTower => "Vattenpistol",
		BuildingType.LaserTower => "Lasertorn",
		BuildingType.Junction => "Korsning",
		BuildingType.Toolbox => "Verktygslåda",
		BuildingType.GolemWorkshop => "Golemverkstad",
		BuildingType.CarFactory => "Bilfabrik",
		_ => type.ToString(),
	};

	public static string Description(BuildingType type) => type switch
	{
		BuildingType.Conveyor => "Flyttar resurser. Kan byggas på allt golv.",
		BuildingType.BrickExtractor => "Måste stå på klossar.",
		BuildingType.PlasticExtractor => "Måste stå på plast.",
		BuildingType.BatteryExtractor => "Måste stå på batterier.",
		BuildingType.Splitter => "Tar emot från alla håll och delar ut i tur och ordning rakt fram, höger och vänster.",
		BuildingType.Sorter => "Tar emot från alla håll. Vald sort fortsätter rakt fram, allt annat svänger av åt sidorna. Klicka på den för att byta sort.",
		BuildingType.Assembler => "Gör kugghjul (2 klossar + 1 plast), fjädrar (2 plast) eller kretskort (1 batteri + 1 plast). Tar emot från alla håll, lämnar ut åt pilens håll (R roterar). Klicka på den för att byta.",
		BuildingType.SoldierFactory => "2x2. Gör en plastsoldat av 3 plast + 1 fjäder. Soldaterna går själva mot fiendens låda.",
		BuildingType.FoamTower => "Ammo: plast (1 plast = 4 pilar). Snabb, ett mål i taget, räckvidd 6. Halv skada mot golems.",
		BuildingType.Catapult => "Ammo: klossar (kastar dem). Långsam, skadar ett område, räckvidd 8. Dubbel skada mot plastsoldater.",
		BuildingType.WaterTower => "Ammo: batterier (1 batteri = 10 skott). Mycket snabb, räckvidd 4. Trippel skada mot elektronik (radiobilar). Halv skada mot golems.",
		BuildingType.Junction => "Låter två band korsa varandra. Allt åker rakt igenom, banden blandas aldrig.",
		BuildingType.Toolbox => "2x2. Skruvar ihop en uppdragsrobot (byggare) av 2 plast + 2 klossar. Byggarna går själva till nya byggplatser och bygger dem. Max 20.",
		BuildingType.LaserTower => "Ammo: batterier (1 batteri = 5 skott). Räckvidd 7. Dubbel skada mot klossgolems.",
		BuildingType.GolemWorkshop => "2x2. Gör en klossgolem av 4 klossar + 2 kugghjul. Långsam och tålig, bryr sig inte om trupper, slår dubbelt så hårt på byggnader. Svag mot laser.",
		BuildingType.CarFactory => "2x2. Gör en radiobil av 1 kretskort + 2 kugghjul + 1 batteri. Snabb men skör. Svag mot vattenpistoler.",
		_ => "",
	};

	public static string ErrorText(BuildingType type, PlaceError error) => error switch
	{
		PlaceError.NotFloor => "Kan bara byggas på golv.",
		PlaceError.Occupied => "Det står redan något här.",
		PlaceError.WrongResource => $"{DisplayName(type)} måste stå på {ResourceName(BuildingRules.RequiredResource(type))} (gröna rutor).",
		PlaceError.OutsideZone => "Du kan bara bygga i ditt eget rum och i hallen.",
		PlaceError.NotEnoughResources => $"Inte tillräckligt med resurser. {DisplayName(type)} kostar {CostText(type)}.",
		_ => "",
	};

	public static string ResourceName(ResourceType type) => type switch
	{
		ResourceType.Brick => "klossar",
		ResourceType.Plastic => "plast",
		ResourceType.Battery => "batterier",
		_ => "",
	};

	/// <summary>All sprites are drawn facing east (rotation 0).</summary>
	public static float Rotation(Direction facing) => (int)facing * Mathf.Pi / 2f;

	public static Vector2 CellCenter(int x, int y) =>
		new((x + 0.5f) * TileSize, (y + 0.5f) * TileSize);

	/// <summary>Centre of all tiles a building covers.</summary>
	public static Vector2 FootprintCenter(Building building) =>
		new((building.X + building.Width / 2f) * TileSize, (building.Y + building.Height / 2f) * TileSize);

	/// <summary>E.g. "10 klossar, 5 plast".</summary>
	public static string CostText(BuildingType type, string separator = ", ")
	{
		var parts = new List<string>();
		foreach (var stack in BuildingRules.Cost(type))
			parts.Add(ItemCount(stack.Type, stack.Amount));
		return parts.Count == 0 ? "gratis" : string.Join(separator, parts);
	}

	/// <summary>E.g. "1 fjäder", "3 plast", "2 kugghjul".</summary>
	public static string ItemCount(ItemType type, int amount)
	{
		if (amount != 1)
			return $"{amount} {ItemName(type).ToLowerInvariant()}";
		string one = type switch
		{
			ItemType.Brick => "kloss",
			ItemType.Battery => "batteri",
			ItemType.Spring => "fjäder",
			_ => ItemName(type).ToLowerInvariant(),
		};
		return $"1 {one}";
	}

	public static string UnitName(UnitType type) => type switch
	{
		UnitType.PlasticSoldier => "Plastsoldat",
		UnitType.BrickGolem => "Klossgolem",
		UnitType.RcCar => "Radiobil",
		UnitType.Builder => "Byggare (uppdragsrobot)",
		_ => type.ToString(),
	};

	public static string ItemName(ItemType type) => type switch
	{
		ItemType.Brick => "Klossar",
		ItemType.Plastic => "Plast",
		ItemType.Battery => "Batterier",
		ItemType.Gear => "Kugghjul",
		ItemType.Spring => "Fjädrar",
		ItemType.CircuitBoard => "Kretskort",
		_ => type.ToString(),
	};

	public static Vector2I WorldToCell(Vector2 position) =>
		new(Mathf.FloorToInt(position.X / TileSize), Mathf.FloorToInt(position.Y / TileSize));
}
