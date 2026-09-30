using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class ConveyorLookTests
{
	private static ConveyorLook Look(Scenario s, int x, int y) => ConveyorLook.For(s.World, (Conveyor)s.World.GetBuilding(x, y));

	[Fact]
	public void Straight_WhenFedFromBehind()
	{
		var s = Scenario.Match().Instant();
		s.Belt(20, 20, 22, 20);
		Assert.Equal(new ConveyorLook(false, false, (int)Direction.East), Look(s, 21, 20));
	}

	[Fact]
	public void RightTurn_EastThenSouth_IsTheUnrotatedCurve()
	{
		var s = Scenario.Match().Instant();
		s.Conveyor(20, 20, Direction.East);
		s.Conveyor(21, 20, Direction.South);
		Assert.Equal(new ConveyorLook(true, false, 0), Look(s, 21, 20));
	}

	[Fact]
	public void LeftTurn_EastThenNorth_IsTheFlippedCurve()
	{
		var s = Scenario.Match().Instant();
		s.Conveyor(20, 20, Direction.East);
		s.Conveyor(21, 20, Direction.North);
		Assert.Equal(new ConveyorLook(true, true, 0), Look(s, 21, 20));
	}

	[Fact]
	public void RightTurn_SouthThenWest_IsRotatedAQuarter()
	{
		var s = Scenario.Match().Instant();
		s.Conveyor(20, 20, Direction.South);
		s.Conveyor(20, 21, Direction.West);
		Assert.Equal(new ConveyorLook(true, false, 1), Look(s, 20, 21));
	}

	[Fact]
	public void FedFromBothSides_StaysStraight()
	{
		var s = Scenario.Match().Instant();
		s.Conveyor(21, 19, Direction.South);
		s.Conveyor(21, 21, Direction.North);
		s.Conveyor(21, 20, Direction.East);
		Assert.False(Look(s, 21, 20).Curve);
	}

	[Fact]
	public void TwoByTwoNeighbour_DoesntBendIt()
	{
		var s = Scenario.Match().Instant().Rich();
		s.Place(BuildingType.SoldierFactory, 20, 18); // covers (20..21, 18..19), above the belt
		s.Conveyor(21, 20, Direction.East);
		Assert.False(Look(s, 21, 20).Curve);
	}
}

public class DragPathTests
{
	[Fact]
	public void FillsEveryTile_XFirstThenY()
	{
		var path = DragPath.Walk(0, 0, 2, -2).ToList();
		Assert.Equal(new[]
		{
			(1, 0, Direction.East), (2, 0, Direction.East), (2, -1, Direction.North), (2, -2, Direction.North),
		}, path);
	}

	[Fact]
	public void SameTile_NoSteps() => Assert.Empty(DragPath.Walk(3, 3, 3, 3));

	[Fact]
	public void WestAndSouth()
	{
		Assert.Equal(new[] { (4, 5, Direction.West) }, DragPath.Walk(5, 5, 4, 5));
		Assert.Equal(new[] { (5, 6, Direction.South) }, DragPath.Walk(5, 5, 5, 6));
	}
}

public class FoodMeterTests
{
	[Fact]
	public void Surplus_IsGood()
	{
		var s = Scenario.Match().NoWorkers();
		var (text, mood) = FoodMeter.Describe(s.P0);
		Assert.Equal(Mood.Good, mood);
		Assert.Contains("netto +0/min", text);
	}

	[Fact]
	public void Deficit_SaysHowLongItLasts()
	{
		var s = Scenario.Match().NoWorkers().Empty(ItemType.Food).Give(ItemType.Food, 50);
		for (int i = 0; i < 10; i++) s.Spawn(UnitType.PlasticSoldier, 2, 5 + i); // eats 10/min
		s.World.Tick();
		var (text, mood) = FoodMeter.Describe(s.P0);
		Assert.Contains("−10/min", text);
		Assert.Contains("räcker ~5 min", text);
		Assert.Equal(Mood.Warn, mood);
	}

	[Fact]
	public void UnderAMinuteLeft_IsBad()
	{
		var s = Scenario.Match().NoWorkers().Empty(ItemType.Food).Give(ItemType.Food, 5);
		for (int i = 0; i < 10; i++) s.Spawn(UnitType.PlasticSoldier, 2, 5 + i);
		s.World.Tick();
		var (text, mood) = FoodMeter.Describe(s.P0);
		Assert.Contains("inom en minut", text);
		Assert.Equal(Mood.Bad, mood);
	}

	[Fact]
	public void Starving_SaysSo()
	{
		var s = Scenario.Match().NoWorkers().Empty(ItemType.Food);
		s.Spawn(UnitType.PlasticSoldier, 2, 5);
		s.World.Until(() => s.P0.Starving, 90, "starving");
		var (text, mood) = FoodMeter.Describe(s.P0);
		Assert.Contains("SVÄLT", text);
		Assert.Equal(Mood.Bad, mood);
	}
}

public class InfoRowsTests
{
	private static string All(System.Collections.Generic.List<InfoRow> rows) => string.Join(" | ", rows.Select(r => r.Text));

	[Fact]
	public void ConstructionSite_ShowsProgressAndBuilders()
	{
		var s = Scenario.Match().NoWorkers();
		var belt = s.Place(BuildingType.Conveyor, 20, 20);
		var rows = InfoRows.For(s.World, belt, 0);
		Assert.Equal(RowKind.Title, rows[0].Kind);
		Assert.Equal("Transportband", rows[0].Text);
		Assert.Contains(rows, r => r.Text == "Byggs: 0%");
		Assert.Contains(rows, r => r.Kind == RowKind.Progress && r.Total == belt.BuildTime);
		Assert.Contains(rows, r => r.Text != null && r.Text.StartsWith("Ingen byggare") && r.Tone == Tone.Missing);
	}

	[Fact]
	public void EnemyBuilding_IsMarked()
	{
		var s = Scenario.Match();
		Assert.Equal("Leksakslåda  (fiende)", InfoRows.For(s.World, s.World.GetCore(1), 0)[0].Text);
	}

	[Fact]
	public void Factory_ShowsWhatItHasOfEachInput()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 20, 20);
		var first = factory.Recipe[0];
		s.Feed(factory, first.Type, first.Amount);
		var needs = InfoRows.For(s.World, factory, 0).Where(r => r.Kind == RowKind.Item).ToList();
		Assert.Equal(factory.Recipe.Length, needs.Count);
		Assert.Equal(Tone.Good, needs.Single(r => r.Item == first.Type).Tone);
		Assert.All(needs.Where(r => r.Item != first.Type), r => Assert.Equal(Tone.Missing, r.Tone));
	}

	[Fact]
	public void Tower_ShowsAmmoLeft()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var tower = s.Place<Tower>(BuildingType.FoamTower, 20, 20);
		Assert.Contains(InfoRows.For(s.World, tower, 0), r => r.Text == $"Skott kvar: 0/{tower.Stats.MaxShots}" && r.Tone == Tone.Missing);
		s.Feed(tower, ItemType.Plastic, 1);
		Assert.Contains(InfoRows.For(s.World, tower, 0), r => r.Text == $"Skott kvar: {tower.Stats.ShotsPerItem}/{tower.Stats.MaxShots}" && r.Tone == Tone.Good);
	}

	[Fact]
	public void Toybox_ListsStorage_FullIsMissing()
	{
		var s = Scenario.Match();
		s.Give(ItemType.Gear, PlayerState.CoreCapacity);
		var rows = InfoRows.For(s.World, s.World.GetCore(0), 0);
		Assert.Contains(rows, r => r.Item == ItemType.Gear && r.Text == $"{PlayerState.CoreCapacity}/{PlayerState.CoreCapacity}" && r.Tone == Tone.Missing);
		Assert.DoesNotContain(rows, r => r.Item == ItemType.Spring); // empty and not a raw material
	}

	[Fact]
	public void CropField_CountsDownThenRipe()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var field = s.Place<CropField>(BuildingType.CropField, 20, 20);
		Assert.Contains($"mogen om {CropField.GrowTicks / World.TicksPerSecond} s", All(InfoRows.For(s.World, field, 0)));
		s.World.Until(() => field.IsRipe, 60, "ripe");
		Assert.Contains("Mogen!", All(InfoRows.For(s.World, field, 0)));
	}
}

public class TextsTests
{
	[Fact]
	public void EveryBuildableBuilding_HasANameAndCost()
	{
		foreach (var type in BuildingRules.Buildable)
		{
			Assert.NotEqual(type.ToString(), Texts.DisplayName(type));
			Assert.False(string.IsNullOrEmpty(Texts.CostText(type)));
		}
	}

	[Fact]
	public void EveryItem_HasAName()
	{
		foreach (var item in Items.All)
			Assert.NotEqual(item.ToString(), Texts.ItemName(item));
	}

	[Theory]
	[InlineData(ItemType.Brick, 1, "1 kloss")]
	[InlineData(ItemType.Brick, 3, "3 klossar")]
	[InlineData(ItemType.Crop, 2, "2 morötter")]
	[InlineData(ItemType.Gear, 1, "1 kugghjul")]
	public void ItemCount_Singular(ItemType item, int amount, string expected) =>
		Assert.Equal(expected, Texts.ItemCount(item, amount));
}
