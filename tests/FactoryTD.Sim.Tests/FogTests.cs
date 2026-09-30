using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class VisionTests
{
	private static Scenario Fog() => Scenario.Match().NoWorkers().Instant().Rich().RealFog();

	[Fact]
	public void AtTheStart_TheToyboxLightsBothNearbyDeposits_NotTheBatteries()
	{
		var w = Scenario.Match().RealFog().World;
		Assert.True(w.IsExplored(0, 10, 25)); // bricks
		Assert.True(w.IsExplored(0, 10, 36)); // plastic
		Assert.False(w.IsExplored(0, 41, 47)); // batteries
		Assert.False(w.IsExplored(0, 150, 31)); // the enemy room
		Assert.True(w.IsExplored(1, 150, 31));
	}

	[Fact]
	public void FullVision_SeesEverything()
	{
		var w = Scenario.Match().World;
		Assert.True(w.IsExplored(0, 150, 31));
		Assert.True(w.IsVisible(0, 41, 47));
	}

	[Fact]
	public void BuildingLight_GrowsWithItsSize()
	{
		var s = Fog();
		s.Place(BuildingType.BatteryCharger, 30, 45); // 1x1: radius 3
		s.Place(BuildingType.SoldierFactory, 30, 15); // 2x2: radius 5 from tile (31, 16)
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.True(s.World.IsVisible(0, 33, 45));
		Assert.False(s.World.IsVisible(0, 34, 45));
		Assert.True(s.World.IsVisible(0, 36, 16));
		Assert.False(s.World.IsVisible(0, 37, 16));
	}

	[Fact]
	public void ConstructionSite_LightsOnlyItsNeighbours()
	{
		var s = Scenario.Match().NoWorkers().Rich().RealFog();
		s.Place(BuildingType.Conveyor, 40, 15);
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.True(s.World.IsVisible(0, 41, 15));
		Assert.False(s.World.IsVisible(0, 42, 15));
	}

	[Fact]
	public void Walls_BlockLight_TheDoorLetsItThrough()
	{
		var s = Fog();
		Assert.Equal(TileType.Floor, s.World.Map[63, 26]); // the hall, behind the room's east wall
		s.Place(BuildingType.SoldierFactory, 57, 25); // light from tile (58, 26), radius 5
		s.Place(BuildingType.SoldierFactory, 57, 29); // light from tile (58, 30), in front of the door
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.True(s.World.IsVisible(0, 61, 26)); // the wall itself is lit
		Assert.False(s.World.IsVisible(0, 62, 26)); // ...but not what is behind it
		Assert.True(s.World.IsVisible(0, 62, 30)); // through the door
		Assert.True(s.World.IsVisible(0, 63, 30));
	}

	[Fact]
	public void WalkingUnit_ExploresItsPath_VisibleOnlyWhereItIsNow()
	{
		var s = Fog();
		var soldier = s.Spawn(UnitType.PlasticSoldier, 30, 45); // walks off towards the enemy
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.True(s.World.IsVisible(0, 30, 45));
		s.World.Seconds(10);
		Assert.True(s.World.IsExplored(0, 30, 45));
		Assert.False(s.World.IsVisible(0, 30, 45));
		Assert.True(s.World.IsVisible(0, soldier.TileX, soldier.TileY));
	}

	[Fact]
	public void CarHeadlights_SeeFarAhead_NotBehind()
	{
		var s = Fog();
		var car = s.Spawn(UnitType.RcCar, 70, 30); // in the hall, drives east towards the enemy
		s.World.Seconds(1);
		Assert.Equal(0, car.LightDirection); // east
		s.World.Ticks(VisionStats.VisionTicks);
		int x = car.TileX, y = car.TileY;
		Assert.True(s.World.IsVisible(0, x + VisionStats.HeadlightRange - 1, y));
		Assert.True(s.World.IsVisible(0, x + VisionStats.UnitRadius(UnitType.RcCar), y));
		Assert.False(s.World.IsVisible(0, x - VisionStats.UnitRadius(UnitType.RcCar) - 1, y));
		Assert.False(s.World.IsVisible(0, x + 2, y + 8)); // outside the cone
	}

	[Theory]
	[InlineData(5, 0, 0)]
	[InlineData(5, 5, 1)]
	[InlineData(0, 5, 2)]
	[InlineData(-5, 1, 4)]
	[InlineData(2, -7, 6)]
	[InlineData(3, -7, 7)] // 23° off north: diagonal
	[InlineData(0, 0, -1)]
	public void DirectionIndex_NearestOfEight(int dx, int dy, int expected) =>
		Assert.Equal(expected, Vision.DirectionIndex(dx, dy));
}

public class FogPlacementTests
{
	[Fact]
	public void BatteryPatch_NotBuildableUntilSomeoneHasBeenThere()
	{
		var s = Scenario.Match().NoWorkers().Rich().RealFog();
		Assert.Equal(PlaceError.Unexplored, s.World.CheckPlace(BuildingType.BatteryExtractor, 41, 47, 0));
		Assert.Equal(PlaceError.Unexplored, s.World.CheckPlace(BuildingType.BrickExtractor, 41, 47, 0)); // doesn't leak "wrong resource"
		s.Spawn(UnitType.PlasticSoldier, 41, 45);
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.Equal(PlaceError.None, s.World.CheckPlace(BuildingType.BatteryExtractor, 41, 47, 0));
		Assert.Equal(PlaceError.WrongResource, s.World.CheckPlace(BuildingType.BrickExtractor, 41, 47, 0));
	}

	[Fact]
	public void FullVision_IgnoresTheFog() =>
		Assert.Equal(PlaceError.None, Scenario.Match().Rich().World.CheckPlace(BuildingType.BatteryExtractor, 41, 47, 0));

	[Fact]
	public void OtherBuildings_GoInTheDark_AndTheBuilderLightsTheWay()
	{
		var s = Scenario.Match().Rich().RealFog(); // with the starting builders
		Assert.False(s.World.IsExplored(0, 40, 45));
		Assert.Equal(PlaceError.None, s.World.CheckPlace(BuildingType.Conveyor, 40, 45, 0));
		s.Place(BuildingType.Conveyor, 40, 45);
		s.World.Until(() => s.World.IsExplored(0, 41, 47), 60, "the builder walking there lights the battery patch");
	}
}
