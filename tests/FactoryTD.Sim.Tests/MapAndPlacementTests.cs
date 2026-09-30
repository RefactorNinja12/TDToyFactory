using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class MapTests
{
	private static readonly MapLayout Map = MapLayout.CreateDefault();

	[Fact]
	public void Layout_RoomsHallAndDoors()
	{
		Assert.Equal(TileType.Wall, Map[0, 0]);
		Assert.Equal(TileType.Floor, Map[30, 30]);                 // left room
		Assert.Equal(TileType.Floor, Map[61, 30]);                 // left door
		Assert.Equal(TileType.Wall, Map[61, 20]);                  // left room's wall beside the door
		Assert.Equal(TileType.Floor, Map[80, 30]);                 // hall
		Assert.Equal(TileType.Floor, Map[Map.Width - 1 - 61, 30]); // right door
	}

	[Fact]
	public void Zones_HomeRoomsAndSharedHall()
	{
		Assert.Equal(Zone.LeftRoom, Map.GetZone(30, 30));
		Assert.Equal(Zone.RightRoom, Map.GetZone(Map.Width - 31, 30));
		Assert.Equal(Zone.Hall, Map.GetZone(80, 30));
		Assert.Equal(Zone.LeftRoom, MapLayout.HomeZone(0));
		Assert.Equal(Zone.RightRoom, MapLayout.HomeZone(1));
	}

	[Theory]
	[InlineData(11, 23)] // bricks, left room
	[InlineData(12, 37)] // plastic, left room
	[InlineData(41, 47)] // batteries, left room
	[InlineData(67, 26)] // bricks, hall
	[InlineData(78, 30)] // batteries, hall
	public void Deposits_AreMirrored(int x, int y)
	{
		Assert.NotEqual(ResourceType.None, Map.GetResource(x, y));
		Assert.Equal(Map.GetResource(x, y), Map.GetResource(Map.Width - 1 - x, y));
	}
}

public class PlacementTests
{
	[Fact]
	public void Place_PaysCost_RemoveRefunds()
	{
		var s = Scenario.Match();
		int bricks = s.P0.GetCount(ItemType.Brick);
		s.Place(BuildingType.BrickExtractor, 11, 23);
		Assert.Equal(bricks - 10, s.P0.GetCount(ItemType.Brick));
		Assert.True(s.World.TryRemove(11, 23, 0));
		Assert.Equal(bricks, s.P0.GetCount(ItemType.Brick));
	}

	[Theory]
	[InlineData(BuildingType.Conveyor, 0, 0, PlaceError.NotFloor)]          // wall
	[InlineData(BuildingType.Conveyor, 7, 30, PlaceError.Occupied)]         // core
	[InlineData(BuildingType.BrickExtractor, 20, 20, PlaceError.WrongResource)]
	[InlineData(BuildingType.PlasticExtractor, 11, 23, PlaceError.WrongResource)] // on bricks
	[InlineData(BuildingType.Conveyor, 120, 20, PlaceError.OutsideZone)]    // enemy room
	[InlineData(BuildingType.SoldierFactory, 97, 30, PlaceError.OutsideZone)] // 2x2 poking into the enemy door
	public void CheckPlace_Errors(BuildingType type, int x, int y, PlaceError expected) =>
		Assert.Equal(expected, Scenario.Match().World.CheckPlace(type, x, y, 0));

	[Fact]
	public void CheckPlace_NotEnoughResources()
	{
		var s = Scenario.Match();
		Assert.Equal(PlaceError.NotEnoughResources, s.World.CheckPlace(BuildingType.LaserTower, 20, 20, 0)); // needs batteries
	}

	[Fact]
	public void BothPlayers_BuildInTheHall_NotInEachOthersRoom()
	{
		var w = Scenario.Match().World;
		Assert.Equal(PlaceError.None, w.CheckLocation(BuildingType.Conveyor, 80, 30, 0));
		Assert.Equal(PlaceError.None, w.CheckLocation(BuildingType.Conveyor, 80, 30, 1));
		Assert.Equal(PlaceError.OutsideZone, w.CheckLocation(BuildingType.Conveyor, 20, 20, 1));
	}

	[Fact]
	public void TwoByTwo_CoversFourTiles()
	{
		var s = Scenario.Match().Rich();
		var factory = s.Place(BuildingType.SoldierFactory, 20, 20);
		Assert.Same(factory, s.World.GetBuilding(21, 21));
		Assert.Equal(PlaceError.Occupied, s.World.CheckPlace(BuildingType.Conveyor, 21, 20, 0));
		Assert.True(s.World.TryRemove(21, 21, 0)); // removing from any of its tiles
		Assert.Null(s.World.GetBuilding(20, 20));
	}

	[Fact]
	public void CantRemove_CoreOrEnemyBuildings()
	{
		var s = Scenario.Match().Rich();
		s.Place(BuildingType.Conveyor, 80, 30, owner: 1);
		Assert.False(s.World.TryRemove(6, 30, 0));  // own core
		Assert.False(s.World.TryRemove(80, 30, 0)); // enemy belt
		Assert.True(s.World.TryRemove(80, 30, 1));
	}
}

public class ConstructionTests
{
	[Fact]
	public void Unfinished_DoesNothing()
	{
		var s = Scenario.Match().NoWorkers();
		var extractor = s.Place<Extractor>(BuildingType.BrickExtractor, 11, 23);
		var belt = s.Place(BuildingType.Conveyor, 20, 20);
		s.World.Seconds(10);
		Assert.False(extractor.IsBuilt);
		Assert.Equal(0, extractor.Stored);
		Assert.False(belt.Offer(ItemType.Brick, Direction.East));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	public void BuildTime_DividedByBuilders(int builders)
	{
		var s = Scenario.Match().NoWorkers().Rich();
		var factory = s.Place(BuildingType.SoldierFactory, 20, 20);
		for (int i = 0; i < builders; i++)
			s.Spawn(UnitType.Builder, 19, 20 + i % 2); // already standing next to it
		int ticks = s.World.Until(() => factory.IsBuilt, 60, "factory built");
		int expected = factory.BuildTime / builders;
		Assert.InRange(ticks, expected, expected + 3);
	}

	[Fact]
	public void Builders_SpreadOverSites()
	{
		var s = Scenario.Match().NoWorkers().Rich();
		var a = s.Place(BuildingType.Toolbox, 20, 20);
		var b = s.Place(BuildingType.Toolbox, 26, 20);
		s.Spawn(UnitType.Builder, 23, 18);
		s.Spawn(UnitType.Builder, 23, 18);
		s.World.Ticks(2);
		Assert.Equal(1, s.World.BuildersOn(a));
		Assert.Equal(1, s.World.BuildersOn(b));
	}

	[Fact]
	public void StartingBuilders_BuildAnExtractorAndBelt()
	{
		var s = Scenario.Match();
		s.Place(BuildingType.BrickExtractor, 13, 25, Direction.South);
		s.Belt(13, 26, 13, 29).Belt(13, 30, 8, 30);
		s.World.Until(() => System.Linq.Enumerable.All(s.World.Buildings, b => b.IsBuilt), 20, "all built");
	}

	[Fact]
	public void FirstMinute_ExtractorChainDeliversBricks()
	{
		var s = Scenario.Match();
		int bricks = s.P0.GetCount(ItemType.Brick);
		s.Place(BuildingType.BrickExtractor, 13, 25, Direction.South);
		s.Belt(13, 26, 13, 29).Belt(13, 30, 8, 30);
		int spent = bricks - s.P0.GetCount(ItemType.Brick);
		s.World.Minutes(1); // includes building time and the walk there
		int delivered = s.P0.GetCount(ItemType.Brick) - (bricks - spent);
		Assert.True(delivered >= 20, $"only {delivered} bricks in the first minute");
	}
}

public class StorageTests
{
	[Fact]
	public void Toybox_RefusesWhenFull()
	{
		var s = Scenario.Match();
		var core = s.World.GetCore(0);
		s.Give(ItemType.Gear, PlayerState.CoreCapacity - 1);
		Assert.True(core.Offer(ItemType.Gear, Direction.West));
		Assert.False(core.Offer(ItemType.Gear, Direction.West));
		Assert.False(core.CanTake(ItemType.Gear));
		Assert.True(core.CanTake(ItemType.Spring));
	}

	[Fact]
	public void Warehouse_AddsCapacityOnlyWhenFinished_AndSharesThePool()
	{
		var s = Scenario.Match().NoWorkers().Rich();
		var warehouse = s.Place(BuildingType.Warehouse, 20, 20);
		s.World.Tick();
		Assert.Equal(PlayerState.CoreCapacity, s.P0.Capacity(ItemType.Gear));
		warehouse.CompleteConstruction();
		s.World.Tick();
		Assert.Equal(PlayerState.CoreCapacity + PlayerState.WarehouseCapacity, s.P0.Capacity(ItemType.Gear));
		Assert.Equal(1, s.Feed(warehouse, ItemType.Gear));
		Assert.Equal(1, s.P0.GetCount(ItemType.Gear));
	}

	[Fact]
	public void FullToybox_BacksUpTheBelt()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		s.Give(ItemType.Gear, PlayerState.CoreCapacity);
		s.Belt(12, 30, 8, 30);
		var start = s.World.GetBuilding(12, 30);
		for (int i = 0; i < 200; i++) { s.Feed(start, ItemType.Gear, 1, Direction.West); s.World.Tick(); } // 5 tiles x 16 ticks to reach the end
		Assert.Equal(PlayerState.CoreCapacity, s.P0.GetCount(ItemType.Gear));
		Assert.True(((Conveyor)s.World.GetBuilding(8, 30)).Items.Count > 0);
	}
}
