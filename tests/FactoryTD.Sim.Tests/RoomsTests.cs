using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class RoomsTests
{
	private static MapLayout Rooms(bool obstacles = false, int seed = MapLayout.DefaultSeed) =>
		MapLayout.CreateDefault(obstacles, seed, layout: MapLayoutKind.ManyRooms);

	/// <summary>Steps of the shortest walk between the two home doors (x 61 and 98, y 30).</summary>
	private static int DoorToDoor(MapLayout map) =>
		GridSearch.PathTo(map.Width, map.Height, 61, 30, (x, y) => map[x, y] == TileType.Floor, (x, y) => x == 98 && y == 30).Count;

	[Fact]
	public void ManyRooms_TheWayWinds_ThroughTheCentreRoom()
	{
		var map = Rooms();
		var twoRooms = MapLayout.CreateDefault(false);
		Assert.True(DoorToDoor(map) >= 3 * DoorToDoor(twoRooms), $"{DoorToDoor(map)} steps vs {DoorToDoor(twoRooms)}");
		var path = GridSearch.PathTo(map.Width, map.Height, 61, 30, (x, y) => map[x, y] == TileType.Floor, (x, y) => x == 98 && y == 30);
		var (cx0, cy0, cx1, cy1) = MapLayout.CentreRoom;
		Assert.Contains(path, p => p.X >= cx0 && p.X <= cx1 && p.Y >= cy0 && p.Y <= cy1);
		// No door from the entry room straight into the centre.
		Assert.Equal(TileType.Wall, map[69, 30]);
		Assert.Equal(TileType.Wall, map[90, 30]);
	}

	[Fact]
	public void ManyRooms_IsMirrored_AllFloorReachable_DepositsOnFloor()
	{
		var map = Rooms();
		Assert.True(map.AllFloorConnected());
		for (int y = 0; y < map.Height; y++)
			for (int x = 0; x < map.Width; x++)
			{
				int mx = map.Width - 1 - x;
				Assert.Equal(map[x, y], map[mx, y]);
				Assert.Equal(map.GetResource(x, y), map.GetResource(mx, y));
				if (map.GetResource(x, y) != ResourceType.None)
					Assert.Equal(TileType.Floor, map[x, y]);
			}
		// Every small room has a deposit, except the entry room behind the home door (y 26..35).
		foreach (var (x0, y0, x1, y1) in MapLayout.SmallRooms.Where(r => r.Y0 != 26))
			Assert.Contains(Enumerable.Range(x0, x1 - x0 + 1), x => Enumerable.Range(y0, y1 - y0 + 1).Any(y => map.GetResource(x, y) != ResourceType.None));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	public void ManyRooms_WithObstacles_StaysConnected(int seed) => Assert.True(Rooms(true, seed).AllFloorConnected());

	[Fact]
	public void SmallRooms_AreSharedGround()
	{
		var s = Scenario.Match(layout: MapLayoutKind.ManyRooms).Rich();
		Assert.Equal(PlaceError.None, s.World.CheckPlace(BuildingType.Conveyor, 66, 20, 0));
		Assert.Equal(PlaceError.None, s.World.CheckPlace(BuildingType.Conveyor, 66, 20, 1));
		Assert.Equal(PlaceError.None, s.World.CheckPlace(BuildingType.Conveyor, 85, 30, 0));
	}

	[Fact]
	public void Armies_FindTheLongWay()
	{
		var s = Scenario.Match(layout: MapLayoutKind.ManyRooms).NoWorkers();
		var soldier = s.Spawn(UnitType.PlasticSoldier, 96, 30, owner: 1);
		s.World.Until(() => soldier.TileX <= 61, 120, "the soldier reaches the left home door");
	}
}
