using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class BeltTests
{
	private static int Items(Scenario s, int x, int y) => ((Conveyor)s.World.GetBuilding(x, y)).Items.Count;

	[Fact]
	public void OneTile_TakesLengthOverSpeedTicks()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var belt = s.Conveyor(8, 30, Direction.West); // into the core
		int before = s.P0.GetCount(ItemType.Gear);
		s.Feed(belt, ItemType.Gear, 1, Direction.West);
		int ticks = s.World.Until(() => s.P0.GetCount(ItemType.Gear) > before, 5, "gear in toybox");
		Assert.InRange(ticks, Conveyor.Length / Conveyor.Speed, Conveyor.Length / Conveyor.Speed + 1);
	}

	[Fact]
	public void DeadEnd_BacksUp_AtMostThreeItemsPerTile()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var belt = s.Conveyor(20, 20, Direction.East); // front is empty floor
		for (int i = 0; i < 100; i++) { s.Feed(belt, ItemType.Brick); s.World.Tick(); }
		Assert.Equal(Conveyor.Length / Conveyor.Spacing + 1, belt.Items.Count);
	}

	[Fact]
	public void SideFeed_Merges_WithEntryDirection()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		s.Belt(20, 20, 24, 20);
		var side = s.Conveyor(22, 21, Direction.North); // feeds (22,20) from below
		s.Feed(side, ItemType.Plastic, 1, Direction.North);
		s.World.Until(() => Items(s, 22, 20) > 0, 3, "item merged");
		Assert.Equal(Direction.North, ((Conveyor)s.World.GetBuilding(22, 20)).Items[0].EntryDirection);
	}

	[Fact]
	public void BeltsFacingEachOther_DontSwapItems()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var a = s.Conveyor(20, 20, Direction.East);
		var b = s.Conveyor(21, 20, Direction.West);
		s.Feed(a, ItemType.Brick);
		s.World.Seconds(3);
		Assert.Single(a.Items);
		Assert.Empty(b.Items);
	}
}

public class SplitterSorterJunctionTests
{
	private static int Items(Scenario s, int x, int y) => ((Conveyor)s.World.GetBuilding(x, y)).Items.Count;

	[Fact]
	public void Splitter_DealsStraightRightLeft_FromAnySide()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var splitter = s.Place(BuildingType.Splitter, 20, 20, Direction.North); // its facing doesn't matter
		s.Conveyor(21, 20, Direction.East);  // straight (items come in moving east)
		s.Conveyor(20, 21, Direction.South); // right
		s.Conveyor(20, 19, Direction.North); // left
		for (int i = 0; i < 6; i++) { s.Feed(splitter, ItemType.Brick, 1, Direction.East); s.World.Seconds(1); }
		Assert.Equal((2, 2, 2), (Items(s, 21, 20), Items(s, 20, 21), Items(s, 20, 19)));
	}

	[Fact]
	public void Sorter_FilterStraight_OthersAlternateSides()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var sorter = s.Place<Sorter>(BuildingType.Sorter, 20, 20);
		s.World.TryConfigure(20, 20, 0); // Brick -> Plastic
		Assert.Equal(ItemType.Plastic, sorter.Filter);
		s.Conveyor(21, 20, Direction.East);
		s.Conveyor(20, 21, Direction.South);
		s.Conveyor(20, 19, Direction.North);
		foreach (var item in new[] { ItemType.Plastic, ItemType.Brick, ItemType.Brick, ItemType.Plastic })
		{
			s.Feed(sorter, item, 1, Direction.East);
			s.World.Seconds(1);
		}
		Assert.Equal((2, 1, 1), (Items(s, 21, 20), Items(s, 20, 21), Items(s, 20, 19)));
		var straight = ((Conveyor)s.World.GetBuilding(21, 20)).Items;
		Assert.All(straight, i => Assert.Equal(ItemType.Plastic, i.Type));
	}

	[Fact]
	public void Junction_CrossesWithoutMixing()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		s.Belt(18, 20, 19, 20);
		s.Place(BuildingType.Junction, 20, 20);
		s.Belt(21, 20, 22, 20);
		s.Belt(20, 18, 20, 19);
		s.Belt(20, 21, 20, 22);
		var west = s.World.GetBuilding(18, 20);
		var north = s.World.GetBuilding(20, 18);
		for (int i = 0; i < 40; i++)
		{
			s.Feed(west, ItemType.Brick, 1, Direction.East);
			s.Feed(north, ItemType.Plastic, 1, Direction.South);
			s.World.Tick();
		}
		s.World.Seconds(3);
		var eastEnd = ((Conveyor)s.World.GetBuilding(22, 20)).Items;
		var southEnd = ((Conveyor)s.World.GetBuilding(20, 22)).Items;
		Assert.NotEmpty(eastEnd);
		Assert.NotEmpty(southEnd);
		Assert.All(eastEnd, i => Assert.Equal(ItemType.Brick, i.Type));
		Assert.All(southEnd, i => Assert.Equal(ItemType.Plastic, i.Type));
	}
}

public class ExtractorTests
{
	[Fact]
	public void Produces_OneItemPerProductionTicks_UpToMaxStored()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var extractor = s.Place<Extractor>(BuildingType.BrickExtractor, 11, 23);
		s.World.Ticks(Extractor.ProductionTicks * 3);
		Assert.Equal(3, extractor.Stored);
		s.World.Ticks(Extractor.ProductionTicks * 10);
		Assert.Equal(Extractor.MaxStored, extractor.Stored);
	}

	[Fact]
	public void PushesToAnyNeighbourBelt_ButNotIntoBeltsPointingAtIt()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		s.Place(BuildingType.BrickExtractor, 11, 23, Direction.North); // facing doesn't matter
		var outBelt = s.Conveyor(12, 23, Direction.East);
		var inBelt = s.Conveyor(10, 23, Direction.East); // points into the extractor
		s.World.Ticks(Extractor.ProductionTicks * 2 + 5);
		Assert.NotEmpty(outBelt.Items);
		Assert.Empty(inBelt.Items);
	}
}

public class CraftingTests
{
	[Fact]
	public void Assembler_MakesGear_OutOfItsFrontOnly()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var assembler = s.Place<Assembler>(BuildingType.Assembler, 20, 20, Direction.East);
		Assert.Equal(ItemType.Gear, assembler.Recipe.Output);
		var front = s.Conveyor(21, 20, Direction.East);
		var side = s.Conveyor(20, 21, Direction.South);
		s.Feed(assembler, ItemType.Brick, 2);
		s.Feed(assembler, ItemType.Plastic, 1);
		s.World.Ticks(assembler.Recipe.Ticks + 2);
		Assert.Equal(ItemType.Gear, Assert.Single(front.Items).Type);
		Assert.Empty(side.Items);
	}

	[Fact]
	public void Assembler_BuffersTwoCrafts_CycleResets()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var assembler = s.Place<Assembler>(BuildingType.Assembler, 20, 20);
		Assert.Equal(4, s.Feed(assembler, ItemType.Brick, 10)); // 2 per gear x 2
		Assert.Equal(0, s.Feed(assembler, ItemType.Battery, 1)); // not in the recipe
		s.World.TryConfigure(20, 20, 0);
		Assert.Equal(ItemType.Spring, assembler.Recipe.Output);
		Assert.Equal(0, assembler.Crafter.Stock(ItemType.Brick));
	}

	[Fact]
	public void Kitchen_TakesTenCrops_CooksIntoTheToybox()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var kitchen = s.Place<Kitchen>(BuildingType.Kitchen, 6, 32, Direction.North); // into the core
		Assert.True(kitchen.CanTake(ItemType.Crop));
		Assert.Equal(10, s.Feed(kitchen, ItemType.Crop, 20));
		Assert.False(kitchen.CanTake(ItemType.Crop));
		int food = s.P0.GetCount(ItemType.Food);
		s.World.Ticks(Kitchen.CookTicks * 5 + 5);
		Assert.Equal(food + 5, s.P0.GetCount(ItemType.Food));
	}
}

public class UnitFactoryTests
{
	[Fact]
	public void SoldierFactory_RecipeBecomesSoldier()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 20, 20);
		foreach (var input in factory.Recipe)
			s.Feed(factory, input.Type, input.Amount);
		s.World.Ticks(UnitStats.BuildTicks(UnitType.PlasticSoldier) + 1);
		var soldier = Assert.Single(s.World.Units);
		Assert.Equal(UnitType.PlasticSoldier, soldier.Type);
	}

	[Fact]
	public void Toolbox_StopsAtMaxBuilders()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		for (int i = 0; i < UnitStats.MaxBuilders; i++)
			s.Spawn(UnitType.Builder, 3, 3 + i);
		var toolbox = s.Place<UnitFactory>(BuildingType.Toolbox, 20, 20);
		foreach (var input in toolbox.Recipe)
			s.Feed(toolbox, input.Type, input.Amount);
		s.World.Ticks(UnitStats.BuildTicks(UnitType.Builder) * 2);
		Assert.Equal(UnitStats.MaxBuilders, s.World.CountUnits(0, UnitType.Builder));
	}

	[Fact]
	public void Farmhouse_OneFarmerPerFieldsPerFarmerFields()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var farmhouse = s.Place<UnitFactory>(BuildingType.Farmhouse, 20, 20);
		for (int x = 0; x < UnitStats.FieldsPerFarmer * 2; x++)
			s.Place(BuildingType.CropField, 30 + x, 20);
		for (int i = 0; i < 20; i++)
		{
			s.Feed(farmhouse, ItemType.Brick, 3);
			s.World.Ticks(UnitStats.BuildTicks(UnitType.Farmer));
		}
		Assert.Equal(2, s.World.CountUnits(0, UnitType.Farmer));
	}
}
