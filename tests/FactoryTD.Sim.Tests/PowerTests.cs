using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class PowerBuildingTests
{
	[Theory]
	[InlineData(BuildingType.Pylon)]
	[InlineData(BuildingType.BatteryCharger)]
	public void Buildable_OneTile_PaysItsCost(BuildingType type)
	{
		var s = Scenario.Match();
		Assert.Contains(type, BuildingRules.Buildable);
		Assert.Equal((1, 1), BuildingRules.Size(type));
		int bricks = s.P0.GetCount(ItemType.Brick), plastic = s.P0.GetCount(ItemType.Plastic);
		s.Place(type, 20, 20);
		Assert.True(s.P0.GetCount(ItemType.Brick) < bricks || s.P0.GetCount(ItemType.Plastic) < plastic);
		Assert.IsType(type == BuildingType.Pylon ? typeof(Pylon) : typeof(BatteryCharger), s.World.GetBuilding(20, 20));
	}

	[Theory]
	[InlineData(BuildingType.Pylon)]
	[InlineData(BuildingType.BatteryCharger)]
	public void OwnRoomAndHall_NotTheEnemyRoom(BuildingType type)
	{
		var w = Scenario.Match().Rich().World;
		Assert.Equal(PlaceError.None, w.CheckPlace(type, 80, 30, 0));
		Assert.Equal(PlaceError.OutsideZone, w.CheckPlace(type, 120, 30, 0));
	}

	[Fact]
	public void PowerBuildings_NeverPushItemsOntoBelts()
	{
		var s = Scenario.Match().Instant();
		var pylon = s.Pylon(20, 20);
		var charger = s.Charger(22, 20);
		foreach (var d in new[] { Direction.East, Direction.South, Direction.West, Direction.North })
		{
			Assert.False(pylon.OutputsToward(d));
			Assert.False(charger.OutputsToward(d));
		}
	}

	[Fact]
	public void Charger_StartsEmpty_TestHelperCanFillIt()
	{
		var s = Scenario.Match().Instant();
		Assert.Equal(0, s.Charger(20, 20).Energy);
		Assert.Equal(500, s.Charger(22, 20, energy: 500).Energy);
	}
}

public class PowerGridTests
{
	private static PowerNetwork At(Scenario s, int x, int y, int player = 0) => s.World.Power.NetworkAt(player, x, y);

	[Fact]
	public void Toybox_IsAGridRoot_WithItsOwnRadius()
	{
		var s = Scenario.Match();
		var core = s.World.GetCore(0);
		var network = At(s, 10, 30); // 3.5 tiles right of the core's centre
		Assert.NotNull(network);
		Assert.Contains(core, network.Nodes);
		Assert.Null(At(s, 11, 30)); // 4.5 tiles: outside CoreRadius 4
	}

	[Fact]
	public void Pylon_CoversItsRadius_EuclideanEdge()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		Assert.NotNull(At(s, 25, 20)); // 5 tiles
		Assert.Null(At(s, 26, 20));
		Assert.NotNull(At(s, 24, 23)); // 4² + 3² = 5²
		Assert.Null(At(s, 24, 24));
	}

	[Fact]
	public void UnfinishedPylon_DoesNothing()
	{
		var s = Scenario.Match().NoWorkers();
		s.Pylon(20, 20);
		Assert.Null(At(s, 20, 20));
	}

	[Fact]
	public void Pylons_LinkWithinLinkRange()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		s.Pylon(20 + PowerStats.LinkRange, 20);
		s.Pylon(20, 40);
		s.Pylon(20 + PowerStats.LinkRange + 1, 40);
		Assert.Same(At(s, 20, 20), At(s, 20 + PowerStats.LinkRange, 20));
		Assert.NotSame(At(s, 20, 40), At(s, 20 + PowerStats.LinkRange + 1, 40));
	}

	[Fact]
	public void PylonNearTheToybox_JoinsItsNetwork()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(14, 30); // centre 7.5 tiles from the core's centre
		Assert.Same(At(s, 7, 30), At(s, 14, 30));
	}

	[Fact]
	public void RemovingTheMiddlePylon_SplitsTheNetwork()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		s.Pylon(27, 20);
		s.Pylon(34, 20);
		Assert.Same(At(s, 20, 20), At(s, 34, 20));
		Assert.True(s.World.TryRemove(27, 20, 0));
		Assert.NotNull(At(s, 20, 20));
		Assert.NotSame(At(s, 20, 20), At(s, 34, 20));
	}

	[Fact]
	public void EnemyPylons_NeverLink()
	{
		var s = Scenario.Match().Instant().Rich();
		s.Pylon(80, 30, owner: 0);
		s.Pylon(82, 30, owner: 1);
		Assert.NotNull(At(s, 81, 30, player: 0));
		Assert.NotSame(At(s, 81, 30, player: 0), At(s, 81, 30, player: 1));
		Assert.Equal(0, At(s, 81, 30, player: 0).Owner);
	}

	[Fact]
	public void Charger_LinksButCoversNothing()
	{
		var s = Scenario.Match().Instant();
		var charger = s.Charger(40, 40);
		Assert.Null(At(s, 41, 40));
		var pylon = s.Pylon(44, 40);
		var network = At(s, 44, 40);
		Assert.Contains(charger, network.Chargers);
		Assert.Contains(pylon, network.Nodes);
	}

	[Fact]
	public void Cords_FormATree()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		s.Pylon(24, 20);
		s.Pylon(22, 23); // all three within link range of each other
		Assert.Equal(2, s.World.Power.Cords.Count(c => c.A.Owner == 0 && c.A.Y >= 20 && c.A.Y <= 23 && c.A is Pylon));
	}
}
