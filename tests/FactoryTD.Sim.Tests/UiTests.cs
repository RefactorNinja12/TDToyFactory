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

public class PowerUiTests
{
	private static Scenario Real() => Scenario.Match().NoWorkers().Instant().Rich().RealPower();

	[Fact]
	public void Meter_CountsChargedAndUsed_LastMinute()
	{
		var s = Real();
		s.Pylon(30, 20);
		var charger = s.Charger(31, 20);
		s.Feed(charger, ItemType.Battery, 1);
		s.World.Tick();
		s.World.TryDrawPower(0, 30, 20, 1000);
		var (text, mood) = Meter(s);
		Assert.Contains($"+{PowerMeter.Bolts(PowerStats.EnergyPerBattery)}⚡/min laddas", text);
		Assert.Contains("−10⚡/min används", text);
		Assert.Contains($"lagrat {PowerMeter.Bolts(PowerStats.EnergyPerBattery - 1000)}⚡", text);
		Assert.Equal(Mood.Good, mood);
		s.World.Seconds(61);
		Assert.Contains("+0⚡/min", Meter(s).Text);
	}

	private static (string Text, Mood Mood) Meter(Scenario s) => PowerMeter.Describe(s.World, 0);

	[Fact]
	public void Meter_WarnsAboutUnpoweredBuildings()
	{
		var s = Real();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 30, 20);
		foreach (var input in factory.Recipe) s.Feed(factory, input.Type, input.Amount);
		s.World.Tick();
		var (text, mood) = PowerMeter.Describe(s.World, 0);
		Assert.Contains("1 byggnad utan ström", text);
		Assert.Equal(Mood.Bad, mood);
	}

	[Fact]
	public void InfoRows_Consumer_SaysWhatIsMissing()
	{
		var s = Real();
		var tower = s.Place<Tower>(BuildingType.FoamTower, 30, 20);
		Assert.Contains(InfoRows.For(s.World, tower, 0), r => r.Text.StartsWith("⚡ Ingen ström här") && r.Tone == Tone.Missing);
		s.Pylon(31, 20);
		Assert.Contains(InfoRows.For(s.World, tower, 0), r => r.Text.StartsWith("⚡ Nätet är tomt") && r.Tone == Tone.Missing);
		s.Charger(32, 20, energy: 5000);
		Assert.Contains(InfoRows.For(s.World, tower, 0), r => r.Text == "⚡ Ström: ok (50⚡ i nätet)" && r.Tone == Tone.Good);
		Assert.DoesNotContain(InfoRows.For(s.World, s.World.GetCore(0), 0), r => r.Text != null && r.Text.StartsWith("⚡"));
	}

	[Fact]
	public void InfoRows_ChargerAndPylon_DescribeTheirNetwork()
	{
		var s = Real();
		var pylon = s.Pylon(30, 20);
		Assert.Contains(InfoRows.For(s.World, pylon, 0), r => r.Text == "Ingen batteriladdare i det här nätet.");
		var charger = s.Charger(32, 20, energy: 3000);
		s.Feed(charger, ItemType.Battery, 1);
		var rows = InfoRows.For(s.World, charger, 0);
		Assert.Contains(rows, r => r.Item == ItemType.Battery && r.Text == $"Batterier som väntar: 1/{PowerStats.ChargerBatteryBuffer}");
		Assert.Contains(rows, r => r.Kind == RowKind.Progress && r.Done == 3000 && r.Total == PowerStats.ChargerCapacity);
		Assert.Contains(rows, r => r.Text == "Nätet: 30⚡ lagrat, 1 master, 1 laddare");
	}

	[Fact]
	public void Overlay_CirclesForPylonsAndToybox_ChargedFlag()
	{
		var s = Real();
		s.Pylon(30, 20);
		var circles = PowerOverlay.Circles(s.World, 0);
		Assert.Contains(circles, c => c.X == 30.5f && c.Y == 20.5f && c.Radius == PowerStats.PylonRadius && !c.Charged);
		Assert.Contains(circles, c => c.X == 7f && c.Y == 31f && c.Radius == PowerStats.CoreRadius);
		s.Charger(31, 20, energy: 1);
		Assert.Contains(PowerOverlay.Circles(s.World, 0), c => c.X == 30.5f && c.Charged);
		Assert.DoesNotContain(PowerOverlay.Circles(s.World, 1), c => c.X == 30.5f);
	}

	[Fact]
	public void CordPoints_HangBetweenTheEnds()
	{
		var points = PowerOverlay.CordPoints(0, 0, 10, 0, 8);
		Assert.Equal(9, points.Length);
		Assert.Equal((0f, 0f), points[0]);
		Assert.Equal((10f, 0f), points[8]);
		Assert.Equal(5f, points[4].X);
		Assert.Equal(10 * PowerOverlay.SagPerLength, points[4].Y, 3);
	}

	[Fact]
	public void Cords_OnePerLink_OnlyTheirOwner()
	{
		var s = Real();
		s.Pylon(30, 20);
		s.Pylon(35, 20);
		Assert.Single(PowerOverlay.Cords(s.World, 0), c => c[0].X > 29);
		Assert.Empty(PowerOverlay.Cords(s.World, 1));
	}

	[Fact]
	public void PreviewLinks_NearestFirst_OnlyFinishedOwnNodes()
	{
		var s = Real();
		var far = s.Pylon(30, 20);
		var near = s.Pylon(36, 20);
		s.Pylon(38, 22); // further than the one at (36, 20)
		var links = PowerOverlay.PreviewLinks(s.World, 0, 37, 20);
		Assert.Equal(near, links[0]);
		Assert.Contains(far, links);
		Assert.Empty(PowerOverlay.PreviewLinks(s.World, 1, 37, 20));
	}
}

public class BuildHotkeyTests
{
	private static BuildHotkeys Keys() => new(new[]
	{
		new[] { BuildingType.Conveyor, BuildingType.Splitter, BuildingType.Sorter, BuildingType.Junction },
		new[] { BuildingType.BatteryCharger, BuildingType.Pylon },
	});

	[Fact]
	public void NumberThenLetter_PicksTheBuilding()
	{
		var keys = Keys();
		Assert.Equal(new HotkeyResult(HotkeyOutcome.CategoryOpened, 0), keys.Press('1'));
		Assert.True(keys.Armed);
		Assert.Equal(new HotkeyResult(HotkeyOutcome.Selected, 0, BuildingType.Sorter), keys.Press('c'));
		Assert.False(keys.Armed);
		keys.Press('2');
		Assert.Equal(BuildingType.Pylon, keys.Press('X').Type);
	}

	[Fact]
	public void CameraAndOtherKeys_NeverTaken_EvenWhileACategoryWaits()
	{
		var keys = Keys();
		keys.Press('1');
		foreach (char c in "WASDQERV")
			Assert.False(keys.Press(c).Consumed, $"{c} should stay with the camera / its own job");
		Assert.True(keys.Armed); // still waiting for its letter
	}

	[Fact]
	public void EmptyPlace_IsSwallowed_StillWaiting_EscCancels()
	{
		var keys = Keys();
		keys.Press('2');
		Assert.Equal(HotkeyOutcome.Swallowed, keys.Press('G').Outcome); // only two buildings in this category
		Assert.True(keys.Armed);
		Assert.Equal(HotkeyOutcome.Cancelled, keys.Press(BuildHotkeys.Escape).Outcome);
		Assert.False(keys.Armed);
		Assert.False(keys.Press('9').Consumed); // no ninth category
		Assert.False(keys.Press('Z').Consumed); // letters do nothing without a category
	}

	[Fact]
	public void LettersInOrder() => Assert.Equal("ZXCFGT", string.Concat(System.Linq.Enumerable.Range(0, 6).Select(BuildHotkeys.LetterFor)));
}

public class HudModelTests
{
	[Fact]
	public void ResourceBar_RawStocksAlways_OthersOnlyWhenThere()
	{
		var s = Scenario.Match().NoWorkers();
		var bar = ResourceBarModel.For(s.World, 0);
		Assert.Contains(bar.Stocks, e => e.Item == ItemType.Battery && e.Count == 0);
		Assert.DoesNotContain(bar.Stocks, e => e.Item == ItemType.Gear);
		s.Give(ItemType.Gear, PlayerState.CoreCapacity);
		bar = ResourceBarModel.For(s.World, 0);
		Assert.Contains(bar.Stocks, e => e.Item == ItemType.Gear && e.Full);
	}

	[Fact]
	public void ResourceBar_ShortGauges_LongTextInTooltips()
	{
		var s = Scenario.Match().NoWorkers();
		for (int i = 0; i < 10; i++) s.Spawn(UnitType.PlasticSoldier, 2, 5 + i); // eat 10/min, nothing cooked
		s.World.Tick();
		var bar = ResourceBarModel.For(s.World, 0);
		Assert.Equal("-10/min", bar.Food.Short);
		Assert.Contains("äts", bar.Food.Tooltip);
		Assert.EndsWith("⚡", bar.Power.Short);
		Assert.Equal(new Bar(s.World.GetCore(0).MaxHealth, s.World.GetCore(0).MaxHealth), bar.OwnCore);
		Assert.Contains(bar.Workers, w => w.Type == UnitType.Builder && w.Max == UnitStats.MaxBuilders);
	}

	[Fact]
	public void BuildCard_CostPerItem_RedWhereThereIsTooLittle()
	{
		var s = Scenario.Match().NoWorkers().Empty(ItemType.Plastic);
		var card = BuildCardModel.For(BuildingType.Assembler, 2, s.P0);
		Assert.Equal('C', card.Hotkey);
		Assert.Contains(card.Cost, c => c.Item == ItemType.Brick && c.Affordable);
		Assert.Contains(card.Cost, c => c.Item == ItemType.Plastic && !c.Affordable);
		Assert.False(card.Affordable);
		Assert.Equal(Texts.DisplayName(BuildingType.Assembler), card.Name);
	}

	[Fact]
	public void Toast_ShowsThenFades_NewestReplaces()
	{
		var toasts = new Toasts();
		Assert.Equal(0f, toasts.At(0).Alpha);
		toasts.Show("Inte tillräckligt", 10);
		Assert.Equal(("Inte tillräckligt", 1f), toasts.At(11));
		Assert.InRange(toasts.At(10 + Toasts.ShowSeconds - Toasts.FadeSeconds / 2).Alpha, 0.4f, 0.6f);
		Assert.Equal(0f, toasts.At(10 + Toasts.ShowSeconds).Alpha);
		toasts.Show("Nytt", 20);
		Assert.Equal("Nytt", toasts.At(20.5).Text);
	}
}
