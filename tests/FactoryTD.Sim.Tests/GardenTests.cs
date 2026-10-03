using System.Linq;
using FactoryTD.Sim;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class GardenTests
{
	private static readonly ObstacleKind[] Plants =
		{ ObstacleKind.Pumpkin, ObstacleKind.Sunflower, ObstacleKind.Cabbage, ObstacleKind.Carrot, ObstacleKind.Tulips };

	[Fact]
	public void TheGarden_HasTheRoomsShape_WallsDepositsZones()
	{
		var room = MapLayout.CreateDefault(theme: MapTheme.Nursery);
		var garden = MapLayout.CreateDefault(theme: MapTheme.Garden);
		Assert.Equal(MapTheme.Garden, garden.Theme);
		Assert.Equal((room.Width, room.Height), (garden.Width, garden.Height));
		for (int y = 0; y < room.Height; y++)
			for (int x = 0; x < room.Width; x++)
			{
				Assert.Equal(room.GetResource(x, y), garden.GetResource(x, y));
				Assert.Equal(room.GetZone(x, y), garden.GetZone(x, y));
				if (room[x, y] != TileType.Obstacle && garden[x, y] != TileType.Obstacle)
					Assert.Equal(room[x, y], garden[x, y]);
			}
	}

	[Fact]
	public void TheGarden_HasGiantPlants_Mirrored_AndEveryTileReachable()
	{
		var garden = MapLayout.CreateDefault(theme: MapTheme.Garden);
		Assert.All(garden.Obstacles, o => Assert.Contains(o.Kind, Plants));
		Assert.Equal(Plants.Length * 2, garden.Obstacles.Count);
		foreach (var left in garden.Obstacles.Where(o => o.X < garden.Width / 2))
			Assert.Contains(garden.Obstacles, o => o.Kind == left.Kind && o.Y == left.Y && o.X == garden.Width - 1 - left.X - (left.Width - 1));
		Assert.True(garden.AllFloorConnected());
		Assert.DoesNotContain(MapLayout.CreateDefault(theme: MapTheme.Nursery).Obstacles, o => Plants.Contains(o.Kind));
	}

	[Fact]
	public void ABot_BuildsInTheGarden_AsInTheRoom()
	{
		static int Built(MapTheme theme)
		{
			var world = World.CreateMatch(theme: theme);
			var bot = new BotPlayer(0);
			for (int t = 0; t < 20 * 90; t++)
			{
				bot.Tick(world);
				world.Tick();
			}
			return world.Buildings.Count(b => b.Owner == 0 && b.IsBuilt);
		}
		int room = Built(MapTheme.Nursery), garden = Built(MapTheme.Garden);
		Assert.True(garden * 10 >= room * 8, $"garden {garden} buildings, room {room}");
	}
}
