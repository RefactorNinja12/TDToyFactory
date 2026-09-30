using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class FarmingTests
{
	[Fact]
	public void Field_GrowsForGrowTicks_WaitsRipe_Replants()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var field = s.Place<CropField>(BuildingType.CropField, 20, 20);
		Assert.Equal(CropField.GrowTicks, s.World.Until(() => field.IsRipe, 60, "ripe"));
		s.World.Seconds(10);
		Assert.True(field.IsRipe);
		Assert.Equal(CropField.Yield, field.Harvest());
		Assert.False(field.IsRipe);
		Assert.Equal(0, field.Harvest());
	}

	[Fact]
	public void Farmer_HarvestsAndCarriesToTheToybox()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		s.Place(BuildingType.CropField, 10, 32);
		s.Spawn(UnitType.Farmer, 9, 32);
		s.World.Until(() => s.P0.GetCount(ItemType.Crop) == CropField.Yield, 40, "a field's crops in the toybox");
	}

	[Fact]
	public void Farmer_PrefersAKitchen()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var kitchen = s.Place<Kitchen>(BuildingType.Kitchen, 15, 20, Direction.East);
		s.Place(BuildingType.CropField, 18, 20);
		s.Spawn(UnitType.Farmer, 17, 21);
		s.World.Until(() => kitchen.Crafter.Stock(ItemType.Crop) + kitchen.Crafter.Progress > 0, 40, "crops in the kitchen");
		Assert.Equal(0, s.P0.GetCount(ItemType.Crop));
	}

	[Fact]
	public void TwoFarmers_TwoRipeFields_OneEach()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var a = s.Place<CropField>(BuildingType.CropField, 20, 20);
		var b = s.Place<CropField>(BuildingType.CropField, 22, 20);
		s.World.Until(() => a.IsRipe && b.IsRipe, 60, "ripe");
		var f1 = s.Spawn(UnitType.Farmer, 21, 25);
		var f2 = s.Spawn(UnitType.Farmer, 21, 25);
		s.World.Ticks(2);
		Assert.NotNull(f1.Job);
		Assert.NotNull(f2.Job);
		Assert.NotSame(f1.Job, f2.Job);
	}

	[Fact]
	public void IdleFarmer_BringsStoredCropsToAKitchen()
	{
		var s = Scenario.Match().NoWorkers().Instant().Give(ItemType.Crop, 6);
		var kitchen = s.Place<Kitchen>(BuildingType.Kitchen, 12, 32, Direction.East);
		s.Spawn(UnitType.Farmer, 9, 32);
		s.World.Until(() => s.P0.GetCount(ItemType.Crop) < 6 && kitchen.Crafter.Stock(ItemType.Crop) + kitchen.Crafter.Progress > 0,
			40, "stored crops fetched to the kitchen");
	}
}

public class UpkeepTests
{
	[Fact]
	public void UnitsEat_TheirFoodPerMinute()
	{
		var s = Scenario.Match().NoWorkers().Give(ItemType.Food, 100);
		for (int i = 0; i < 10; i++) s.Spawn(UnitType.PlasticSoldier, 2, 5 + i);
		s.Spawn(UnitType.BrickGolem, 2, 20);
		int before = s.P0.GetCount(ItemType.Food);
		s.World.Minutes(1);
		int expected = 10 * UnitStats.FoodPerMinute(UnitType.PlasticSoldier) + UnitStats.FoodPerMinute(UnitType.BrickGolem);
		Assert.Equal(expected, before - s.P0.GetCount(ItemType.Food));
		Assert.Equal(expected, s.P0.FoodUpkeepPerMinute);
		Assert.False(s.P0.Starving);
	}

	[Fact]
	public void Starving_PausesArmyFactories_NotWorkers_AndHurtsUnits()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().Empty(ItemType.Food);
		var soldiers = s.Place<UnitFactory>(BuildingType.SoldierFactory, 20, 20);
		var farmhouse = s.Place<UnitFactory>(BuildingType.Farmhouse, 20, 26);
		for (int x = 0; x < 9; x++) s.Place(BuildingType.CropField, 30 + x, 30);
		var soldier = s.Spawn(UnitType.PlasticSoldier, 2, 5);
		s.World.Until(() => s.P0.Starving, 90, "starving");

		int hp = soldier.Health;
		foreach (var input in soldiers.Recipe) s.Feed(soldiers, input.Type, input.Amount);
		s.Feed(farmhouse, ItemType.Brick, 3);
		s.World.Ticks(UnitStats.BuildTicks(UnitType.PlasticSoldier) + UnitStats.StarveTicks * 2);
		Assert.Equal(0, s.World.CountUnits(0, UnitType.PlasticSoldier) - 1);
		Assert.Equal(1, s.World.CountUnits(0, UnitType.Farmer));
		Assert.True(soldier.Health <= hp - 2 * UnitStats.StarveDamage, $"hp {hp} -> {soldier.Health}");

		s.Give(ItemType.Food, 50);
		s.World.Ticks(2);
		Assert.False(s.P0.Starving);
		hp = soldier.Health;
		s.World.Ticks(UnitStats.StarveTicks * 2);
		Assert.Equal(hp, soldier.Health);
	}

	[Fact]
	public void FoodMeter_CountsTheLastMinute()
	{
		var s = Scenario.Match().NoWorkers();
		var core = s.World.GetCore(0);
		for (int t = 1; t <= WorldRunner.TicksPerMinute * 3 / 2; t++)
		{
			if (t % 80 == 0) core.Offer(ItemType.Food, Direction.West); // 15 per minute
			s.World.Tick();
		}
		Assert.Equal(15, s.P0.FoodProducedLastMinute);
		s.World.Seconds(61);
		Assert.Equal(0, s.P0.FoodProducedLastMinute);
	}
}
