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
