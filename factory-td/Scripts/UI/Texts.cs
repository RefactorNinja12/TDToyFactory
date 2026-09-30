using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>Everything the player reads: names, descriptions and error messages (no Godot types, so it is testable).</summary>
public static class Texts
{
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
		BuildingType.Warehouse => "Lager",
		BuildingType.Pylon => "Leksaksmast",
		BuildingType.BatteryCharger => "Batteriladdare",
		BuildingType.CropField => "Odlingslåda",
		BuildingType.Farmhouse => "Bondgård",
		BuildingType.Kitchen => "Kök",
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
		BuildingType.Pylon => $"Leder ström till allt inom {PowerStats.PylonRadius} rutor och kopplas med sladd till master, laddare och leksakslådan inom {PowerStats.LinkRange} rutor.",
		BuildingType.BatteryCharger => $"Äter batterier från band (1 batteri = {PowerStats.EnergyPerBattery} ström) och laddar nätet den är kopplad till. Rymmer {PowerStats.ChargerCapacity} ström.",
		BuildingType.Warehouse => $"2x2. Leksakshylla som lagrar allt som körs in på band, i samma förråd som leksakslådan. Varje lager ger plats för {PlayerState.WarehouseCapacity} till av varje sort.",
		BuildingType.CropField => $"Plastmorötter växer här på {CropField.GrowTicks / World.TicksPerSecond} s och ger {CropField.Yield} morötter. En bonde skördar, sedan växer den igen.",
		BuildingType.Farmhouse => $"2x2. Leksaksladugård som vevar upp en bonde av 3 klossar. Bönder skördar mogna odlingslådor och bär morötterna till kök, lager eller leksakslådan. Gör bara så många bönder som odlingslådorna behöver (1 per {UnitStats.FieldsPerFarmer}), max {UnitStats.MaxFarmers}.",
		BuildingType.Kitchen => $"Leksaksspis som lagar {ItemCount(ItemType.Crop, Kitchen.Recipe[0].Amount)} till en matlåda på {Kitchen.CookTicks / World.TicksPerSecond} s. Tar emot morötter från bönder och band, lämnar ut maten åt pilens håll (R roterar), t.ex. rakt in i ett lager.",
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
			ItemType.Crop => "morot",
			ItemType.Food => "matlåda",
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
		UnitType.Farmer => "Bonde",
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
		ItemType.Crop => "Morötter",
		ItemType.Food => "Matlådor",
		_ => type.ToString(),
	};
}
