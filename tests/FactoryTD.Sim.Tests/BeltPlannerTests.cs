using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class BeltPlannerTests
{
	private static Scenario Floor() => Scenario.Match().NoWorkers().Instant().Rich();

	private static int Turns(System.Collections.Generic.List<BeltStep> plan) =>
		plan.Zip(plan.Skip(1)).Count(p => !p.First.Junction && !p.Second.Junction && p.First.Facing != p.Second.Facing);

	[Fact]
	public void StraightLine_OnEmptyFloor()
	{
		var s = Floor();
		var plan = BeltPlanner.Plan(s.World, 0, (20, 40), (26, 40));
		Assert.Equal(7, plan.Count);
		Assert.All(plan, step => Assert.Equal(Direction.East, step.Facing));
		Assert.Equal((20, 40), (plan[0].X, plan[0].Y));
		Assert.Equal((26, 40), (plan[^1].X, plan[^1].Y));
	}

	[Fact]
	public void Diagonal_TurnsOnce_NotAStaircase()
	{
		var s = Floor();
		var plan = BeltPlanner.Plan(s.World, 0, (20, 40), (27, 45));
		Assert.Equal(7 + 5 + 1, plan.Count);
		Assert.Equal(1, Turns(plan));
	}

	[Fact]
	public void GoesAroundBuildings()
	{
		var s = Floor();
		s.Place(BuildingType.Warehouse, 23, 39);          // 2x2 in the way
		var plan = BeltPlanner.Plan(s.World, 0, (20, 40), (27, 40));
		Assert.DoesNotContain(plan, step => s.World.GetBuilding(step.X, step.Y) != null);
		Assert.True(plan.Count > 8, "a detour");
		Assert.Equal((27, 40), (plan[^1].X, plan[^1].Y));
	}

	[Fact]
	public void CrossesABeltRunningAcross_WithAJunction_AndItemsStillFlowBothWays()
	{
		var s = Floor();
		s.Belt(24, 36, 24, 44);                            // a straight belt running south, across the way
		var plan = BeltPlanner.Plan(s.World, 0, (20, 40), (28, 40));
		Assert.Equal(9, plan.Count);                       // straight through, no detour
		var crossing = Assert.Single(plan, step => step.Junction);
		Assert.Equal((24, 40), (crossing.X, crossing.Y));

		foreach (var command in BeltPlanner.Commands(plan, 0))
			Assert.True(s.World.Apply(command), $"{command}");
		Assert.Equal(BuildingType.Junction, s.World.GetBuilding(24, 40).Type);
		s.Instant();
		Assert.Equal(1, s.Feed(s.World.GetBuilding(20, 40), ItemType.Brick));
		Assert.Equal(1, s.Feed(s.World.GetBuilding(24, 36), ItemType.Plastic, moving: Direction.South));
		s.World.Seconds(6);
		Assert.Contains(((Conveyor)s.World.GetBuilding(28, 40)).Items, i => i.Type == ItemType.Brick);
		Assert.Contains(((Conveyor)s.World.GetBuilding(24, 44)).Items, i => i.Type == ItemType.Plastic);
	}

	[Fact]
	public void DoesNotCrossBeltsAlongItsWay_OrAtATurn()
	{
		var s = Floor();
		s.Belt(22, 40, 26, 40);                            // running the same way: in the way, not crossable
		Assert.False(BeltPlanner.CanCross(s.World, 0, 24, 40, Direction.East));
		s.Conveyor(30, 40, Direction.South);               // a single belt turning: nothing straight behind it
		Assert.False(BeltPlanner.CanCross(s.World, 0, 30, 40, Direction.East));
		var plan = BeltPlanner.Plan(s.World, 0, (20, 40), (28, 40));
		Assert.DoesNotContain(plan, step => step.Junction);
		Assert.DoesNotContain(plan, step => step.Y == 40 && step.X >= 22 && step.X <= 26);
	}

	[Fact]
	public void StartsAndEndsNextToBuildings_PointingIntoTheGoal()
	{
		var s = Floor();
		var extractor = s.Place(BuildingType.BrickExtractor, 11, 23);
		var core = s.World.GetCore(0);
		var plan = BeltPlanner.Plan(s.World, 0, (extractor.X, extractor.Y), (core.X, core.Y));
		Assert.NotNull(plan);
		var first = plan[0];
		Assert.Equal(1, System.Math.Abs(first.X - 11) + System.Math.Abs(first.Y - 23));
		var last = plan[^1];
		var into = s.World.GetBuilding(last.X + last.Facing.DX(), last.Y + last.Facing.DY());
		Assert.Same(core, into);
	}

	[Fact]
	public void NoWay_NoPlan()
	{
		var s = Floor();
		Assert.Null(BeltPlanner.Plan(s.World, 0, (20, 40), (-1, 40)));
		Assert.Null(BeltPlanner.Plan(s.World, 0, (20, 40), (100, 40))); // the enemy's room: can't build there
	}

	[Fact]
	public void StatusText_SaysWhatToDo_AndWhatItCosts()
	{
		Assert.StartsWith("Klicka där bandet ska börja", Texts.BeltPlanText(false, 0, 0, false));
		Assert.Equal("Ingen väg dit för bandet.", Texts.BeltPlanText(true, 0, 0, false));
		var text = Texts.BeltPlanText(true, 12, 2, true);
		Assert.StartsWith("12 band, 2 korsningar: ", text);
		Assert.Contains("Klicka för att bygga", text);
	}
}
