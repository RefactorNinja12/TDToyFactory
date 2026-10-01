using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class CheeseMelterTests
{
	[Fact]
	public void Melter_OnlyOnCheese_MakesMeltedCheese()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		Assert.Equal(PlaceError.WrongResource, s.World.CheckPlace(BuildingType.CheeseMelter, 11, 23, 0)); // bricks
		Assert.Equal(PlaceError.WrongResource, s.World.CheckPlace(BuildingType.PlasticExtractor, 51, 11, 0)); // cheese
		var melter = s.Place<Extractor>(BuildingType.CheeseMelter, 51, 11);
		Assert.Equal(ItemType.MeltedCheese, melter.Output);
		s.World.Ticks(Extractor.ProductionTicks * 2);
		Assert.Equal(2, melter.Stored);
	}

	[Fact]
	public void CheeseIsFoundUnderTheFog_LikeTheOtherDeposits()
	{
		var s = Scenario.Match().NoWorkers().Rich().RealFog();
		Assert.Equal(PlaceError.Unexplored, s.World.CheckPlace(BuildingType.CheeseMelter, 51, 11, 0));
		s.Spawn(UnitType.PlasticSoldier, 51, 13);
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.Equal(PlaceError.None, s.World.CheckPlace(BuildingType.CheeseMelter, 51, 11, 0));
	}

	[Fact]
	public void BigToys_StayClearOfTheCheese()
	{
		var map = MapLayout.CreateDefault();
		foreach (var o in map.Obstacles)
			for (int y = o.Y - 2; y < o.Y + o.Height + 2; y++)
				for (int x = o.X - 2; x < o.X + o.Width + 2; x++)
					Assert.NotEqual(ResourceType.Cheese, map.GetResource(x, y));
	}
}

public class CheeseKitchenTests
{
	[Fact]
	public void Kitchen_CooksFromMeltedCheeseAlone()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var kitchen = s.Place<Kitchen>(BuildingType.Kitchen, 6, 32, Direction.North); // into the toybox
		Assert.True(kitchen.CanTake(ItemType.MeltedCheese));
		Assert.Equal(5, s.Feed(kitchen, ItemType.MeltedCheese, 10));
		int food = s.P0.GetCount(ItemType.Food);
		s.World.Ticks(Kitchen.CookTicks * 5 + 5);
		Assert.Equal(food + 5, s.P0.GetCount(ItemType.Food));
	}

	[Fact]
	public void Kitchen_CropsFirst_ThenCheese()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var kitchen = s.Place<Kitchen>(BuildingType.Kitchen, 6, 32, Direction.North);
		s.Feed(kitchen, ItemType.MeltedCheese, 1);
		s.Feed(kitchen, ItemType.Crop, 2);
		s.World.Ticks(2);
		Assert.True(kitchen.Crafter.Progress > 0);
		Assert.Equal(0, kitchen.CheeseCrafter.Progress);
		s.World.Ticks(Kitchen.CookTicks * 2 + 4);
		Assert.Equal(0, kitchen.CheeseCrafter.Stock(ItemType.MeltedCheese)); // the cheese got cooked after
	}

	[Fact]
	public void InfoRows_ShowBothWaysToCook()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var kitchen = s.Place<Kitchen>(BuildingType.Kitchen, 20, 20);
		var rows = FactoryTD.UI.InfoRows.For(s.World, kitchen, 0);
		Assert.Contains(rows, r => r.Item == ItemType.Crop);
		Assert.Contains(rows, r => r.Item == ItemType.MeltedCheese);
	}
}

public class TreadmillTests
{
	private static Scenario Setup(int builders, out Treadmill mill)
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower(); // no power grid at all
		mill = s.Place<Treadmill>(BuildingType.Treadmill, 20, 20);
		for (int i = 0; i < builders; i++)
			s.Spawn(UnitType.Builder, 24, 20 + i);
		return s;
	}

	[Fact]
	public void BuilderPlusCheese_BecomesACheeseHunter_WithoutPower()
	{
		var s = Setup(builders: 3, out var mill);
		s.Feed(mill, ItemType.MeltedCheese, 2);
		s.World.Until(() => mill.HasTrainee, 20, "a builder climbing on");
		Assert.Equal(2, s.World.CountUnits(0, UnitType.Builder));
		s.World.Until(() => s.World.CountUnits(0, UnitType.CheeseHunter) == 1, 20, "a cheese hunter");
		Assert.False(mill.HasTrainee);
		Assert.Equal(0, mill.Crafter.Stock(ItemType.MeltedCheese));
	}

	[Fact]
	public void NoCheese_NoBuilderCalled()
	{
		var s = Setup(builders: 3, out var mill);
		s.World.Seconds(10);
		Assert.Equal(3, s.World.CountUnits(0, UnitType.Builder));
		Assert.False(mill.HasTrainee);
	}

	[Fact]
	public void NeverTakesTheLastBuilder()
	{
		var s = Setup(builders: 1, out var mill);
		s.Feed(mill, ItemType.MeltedCheese, 2);
		s.World.Seconds(10);
		Assert.Equal(1, s.World.CountUnits(0, UnitType.Builder));
		Assert.Equal(0, s.World.CountUnits(0, UnitType.CheeseHunter));
	}

	[Fact]
	public void Starving_PausesTraining()
	{
		var s = Setup(builders: 3, out var mill);
		s.Empty(ItemType.Food);
		for (int i = 0; i < 10; i++) s.Spawn(UnitType.PlasticSoldier, 2, 5 + i);
		s.World.Until(() => s.P0.Starving, 90, "starving");
		s.Feed(mill, ItemType.MeltedCheese, 2);
		s.World.Seconds(15);
		Assert.Equal(0, s.World.CountUnits(0, UnitType.CheeseHunter));
		Assert.False(mill.HasTrainee);
	}
}

public class CheeseHunterTests
{
	[Fact]
	public void Punches_EnemyUnits_UpClose()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var hunter = s.Spawn(UnitType.CheeseHunter, 30, 45);
		var enemy = s.Spawn(UnitType.Farmer, 31, 45, owner: 1);
		s.World.Until(() => enemy.Health < UnitStats.MaxHealth(UnitType.Farmer), 10, "the farmer hit");
		Assert.DoesNotContain(s.World.Projectiles, p => p.Owner == 0 && p.Kind == DamageKind.Bullet);
	}

	[Fact]
	public void Smashes_EnemyBuildings()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		s.Place(BuildingType.Conveyor, 82, 30, owner: 1);
		s.Spawn(UnitType.CheeseHunter, 80, 30);
		s.World.Until(() => s.World.GetBuilding(82, 30) == null, 60, "the belt smashed");
	}

	[Fact]
	public void LowTier_ASoldierBeatsIt()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var hunter = s.Spawn(UnitType.CheeseHunter, 80, 30);
		var soldier = s.Spawn(UnitType.PlasticSoldier, 84, 30, owner: 1);
		s.World.Until(() => hunter.Health <= 0 || soldier.Health <= 0, 60, "the duel over");
		Assert.True(soldier.Health > 0, "the soldier should win");
		Assert.True(UnitStats.MaxHealth(UnitType.CheeseHunter) < UnitStats.MaxHealth(UnitType.PlasticSoldier));
	}
}

public class TreadmillInfoTests
{
	private static string All(Scenario s, Treadmill mill) =>
		string.Join(" | ", System.Linq.Enumerable.Select(FactoryTD.UI.InfoRows.For(s.World, mill, 0), r => r.Text));

	[Fact]
	public void SaysWhatItIsWaitingFor()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var mill = s.Place<Treadmill>(BuildingType.Treadmill, 20, 20);
		Assert.Contains("Väntar på smält ost.", All(s, mill));
		Assert.Contains(FactoryTD.UI.InfoRows.For(s.World, mill, 0), r => r.Item == ItemType.MeltedCheese);
		s.Feed(mill, ItemType.MeltedCheese, 2);
		Assert.Contains("Väntar på en ledig byggarmus", All(s, mill));
		s.Spawn(UnitType.Builder, 26, 20);
		s.Spawn(UnitType.Builder, 26, 22);
		s.World.Ticks(2);
		Assert.Contains("En byggarmus är på väg.", All(s, mill));
		s.World.Until(() => mill.HasTrainee, 20, "trainee");
		Assert.Contains("En byggarmus springer efter osten.", All(s, mill));
	}
}

