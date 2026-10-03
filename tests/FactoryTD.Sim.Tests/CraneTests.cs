using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class CraneTests
{
	// The brick field in the left room is x 10..13, y 22..25: the crane stands west of it at (9, 23), facing east.
	private static (Scenario, ClawCrane) CraneAtTheBrickField(bool belt = true)
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var crane = s.Place<ClawCrane>(BuildingType.ClawCrane, 9, 23, Direction.East);
		if (belt)
			s.Conveyor(8, 23, Direction.West);
		return (s, crane);
	}

	private static int OnBelt(Scenario s, int x, int y) => ((Conveyor)s.World.GetBuilding(x, y)).Items.Count;

	[Fact]
	public void CarriesFromAnExtractorInTheField_ToTheBeltBehind()
	{
		var (s, crane) = CraneAtTheBrickField();
		s.Place(BuildingType.BrickExtractor, 12, 23);      // three tiles in, no belt of its own
		s.World.Seconds(12);
		Assert.True(OnBelt(s, 8, 23) > 0, "bricks reached the belt behind the crane");
		Assert.All(((Conveyor)s.World.GetBuilding(8, 23)).Items, item => Assert.Equal(ItemType.Brick, item.Type));
	}

	[Fact]
	public void PicksTheNearestExtractorWithSomethingStored()
	{
		var (s, crane) = CraneAtTheBrickField();
		var near = s.Place<Extractor>(BuildingType.BrickExtractor, 10, 23);
		var far = s.Place<Extractor>(BuildingType.BrickExtractor, 13, 23);
		s.World.Until(() => crane.State == ClawState.Out, 10, "the claw sets off");
		s.World.Until(() => crane.State == ClawState.Grab, 10, "it reaches an extractor");
		Assert.Equal(1 * UnitStats.SubTile, crane.Claw);
		Assert.True(far.Stored >= near.Stored, "the far one waits its turn");
	}

	[Fact]
	public void BlockedOutput_ItWaitsHoldingTheItem()
	{
		var (s, crane) = CraneAtTheBrickField(belt: false);
		var extractor = s.Place<Extractor>(BuildingType.BrickExtractor, 11, 23);
		s.World.Seconds(15);
		Assert.Equal(ClawState.Drop, crane.State);
		Assert.Equal(ItemType.Brick, crane.Carrying);
		Assert.Equal(Extractor.MaxStored, extractor.Stored); // took one, then the store filled up again
	}

	[Fact]
	public void IgnoresExtractorsBesideTheRail_AndNeverReachesFurtherThanItsRail()
	{
		var (s, crane) = CraneAtTheBrickField();
		s.Place(BuildingType.BrickExtractor, 11, 22);      // beside the rail
		Assert.Empty(ClawCrane.Served(s.World, 9, 23, Direction.East, 0));
		s.World.Seconds(8);
		Assert.Equal(ClawState.Home, crane.State);
		Assert.Equal(0, OnBelt(s, 8, 23));

		s.Place(BuildingType.BrickExtractor, 13, 23);      // the far end of the field, four tiles in
		Assert.Single(ClawCrane.Served(s.World, 9, 23, Direction.East, 0));
		int furthest = 0;
		for (int t = 0; t < 20 * 15; t++)
		{
			s.World.Tick();
			furthest = System.Math.Max(furthest, crane.Claw);
		}
		Assert.Equal(4 * UnitStats.SubTile, furthest);
		Assert.InRange(furthest, 0, CraneStats.Reach * UnitStats.SubTile);
	}

	[Fact]
	public void TheChecksumSeesTheClaw()
	{
		var (s, crane) = CraneAtTheBrickField();
		s.Place(BuildingType.BrickExtractor, 12, 23);
		s.World.Until(() => crane.State == ClawState.Out, 10, "the claw sets off");
		var before = s.World.Checksum();
		s.World.Tick();
		Assert.NotEqual(before, s.World.Checksum());
		Assert.True(crane.OutputsToward(Direction.West) && !crane.OutputsToward(Direction.East));
	}
}
