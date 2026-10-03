using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class BeltReplaceTests
{
	[Fact]
	public void ASplitterGoesStraightOntoABelt_AndSplitsTheLine()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		s.Belt(20, 40, 24, 40);                                    // a belt running east
		s.Conveyor(22, 41, Direction.South);                       // a branch off it
		Assert.True(BeltReplace.CanReplace(s.World, BuildingType.Splitter, 22, 40, 0));
		foreach (var command in BeltReplace.Commands(BuildingType.Splitter, 22, 40, Direction.East, 0))
			Assert.True(s.World.Apply(command), $"{command}");
		s.Instant();
		Assert.Equal(BuildingType.Splitter, s.World.GetBuilding(22, 40).Type);

		for (int i = 0; i < 4; i++)
		{
			s.Feed(s.World.GetBuilding(20, 40), ItemType.Brick);
			s.World.Seconds(1);
		}
		s.World.Seconds(4);
		Assert.NotEmpty(((Conveyor)s.World.GetBuilding(22, 41)).Items);  // some went down the branch
		Assert.NotEmpty(((Conveyor)s.World.GetBuilding(24, 40)).Items);  // and some straight on
	}

	[Fact]
	public void OnlyLogisticsPieces_OnlyOnYourOwnBelts_OnlyIfYouCanPay()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		s.Conveyor(20, 40, Direction.East);
		s.Place(BuildingType.Assembler, 22, 40);
		Assert.True(BeltReplace.CanReplace(s.World, BuildingType.Sorter, 20, 40, 0));
		Assert.True(BeltReplace.CanReplace(s.World, BuildingType.Junction, 20, 40, 0));
		Assert.False(BeltReplace.CanReplace(s.World, BuildingType.Conveyor, 20, 40, 0));   // a belt on a belt: no
		Assert.False(BeltReplace.CanReplace(s.World, BuildingType.Assembler, 20, 40, 0));  // not a belt piece
		Assert.False(BeltReplace.CanReplace(s.World, BuildingType.Splitter, 22, 40, 0));   // not a belt under it
		Assert.False(BeltReplace.CanReplace(s.World, BuildingType.Splitter, 21, 40, 0));   // empty floor: just build
		Assert.False(BeltReplace.CanReplace(s.World, BuildingType.Splitter, 20, 40, 1));   // someone else's belt

		var poor = Scenario.Match().NoWorkers().Instant();
		poor.Conveyor(20, 40, Direction.East);
		poor.P0.TrySpend(new[] { new ItemStack(ItemType.Plastic, poor.P0.GetCount(ItemType.Plastic)) });
		Assert.False(BeltReplace.CanReplace(poor.World, BuildingType.Splitter, 20, 40, 0));
	}

	[Fact]
	public void StatusText_SaysTheBeltMakesWay() =>
		Assert.StartsWith("Delare ersätter bandet", Texts.ReplacesBelt(BuildingType.Splitter));
}
