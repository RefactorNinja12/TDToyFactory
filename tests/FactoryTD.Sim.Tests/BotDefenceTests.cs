using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class BotDefenceTests
{
	private static (int X, int Y, bool Gate)[] Layer(MapLayoutKind layout, int layer) =>
		BotPlayer.WallLayerTiles(layout, layer).ToArray();

	[Fact]
	public void HallLayers_HaveTheirOpeningAtAlternateEnds_AndGatesInLine()
	{
		var first = Layer(MapLayoutKind.TwoRooms, 0);
		var second = Layer(MapLayoutKind.TwoRooms, 1);
		Assert.DoesNotContain(first, t => t.Y <= 26);
		Assert.DoesNotContain(second, t => t.Y >= 35);
		Assert.Equal(new[] { 30, 31 }, first.Where(t => t.Gate).Select(t => t.Y));
		Assert.Equal(new[] { 30, 31 }, second.Where(t => t.Gate).Select(t => t.Y));
		Assert.Equal(2, second[0].X - first[0].X); // a free column between the layers
	}

	[Fact]
	public void ExitLayers_BlockBothExits_OpeningsAlternate_GatesInLine()
	{
		var door = Layer(MapLayoutKind.ManyRooms, 0);
		var next = Layer(MapLayoutKind.ManyRooms, 1);
		foreach (int y in new[] { 25, 36 })
		{
			Assert.Equal(new[] { 66, 67 }, door.Where(t => t.Y == y).Select(t => t.X));
			Assert.Contains(door, t => t.Y == y && t.X == 66 && t.Gate);
		}
		Assert.Equal(new[] { 62, 63, 64, 65, 66 }, next.Where(t => t.Y == 23).Select(t => t.X));
		Assert.Equal(new[] { 62, 63, 64, 65, 66 }, next.Where(t => t.Y == 38).Select(t => t.X));
		Assert.All(next.Where(t => t.Gate), t => Assert.Equal(66, t.X));
	}

	/// <summary>Player 0's wall layers as the bot lays them, built at once.</summary>
	private static Scenario Walled(MapLayoutKind layout, bool walls)
	{
		var s = Scenario.Match(layout: layout).NoWorkers().Instant().Rich();
		if (walls)
			for (int layer = 0; layer < 3; layer++)
				foreach (var (x, y, gate) in BotPlayer.WallLayerTiles(layout, layer))
					s.Place(gate ? BuildingType.Gate : BuildingType.BrickWall, x, y);
		return s;
	}

	[Theory]
	[InlineData(MapLayoutKind.TwoRooms, 80, 30, 66, 30)]
	[InlineData(MapLayoutKind.ManyRooms, 66, 17, 66, 30)]
	public void Labyrinth_IsLongerForTheEnemy_NeverSealed_AndNoLongerForItsOwnArmy(MapLayoutKind layout, int outX, int outY, int inX, int inY)
	{
		var open = Walled(layout, walls: false).World;
		var walled = Walled(layout, walls: true).World;
		int enemyOpen = open.GetFlowField(1).Distance(outX, outY), enemyWalled = walled.GetFlowField(1).Distance(outX, outY);
		Assert.True(enemyWalled > enemyOpen, $"the enemy's way {enemyOpen} -> {enemyWalled}");
		Assert.True(enemyWalled < WallStats.SealedWallPenalty, "sealed: the enemy would beat on the walls");
		Assert.Equal(open.GetFlowField(0).Distance(inX, inY), walled.GetFlowField(0).Distance(inX, inY));
	}

	[Theory]
	[InlineData(MapLayoutKind.TwoRooms)]
	[InlineData(MapLayoutKind.ManyRooms)]
	public void Bot_BuildsItsEarlyDefence_WallsAndTowersBehindThem(MapLayoutKind layout)
	{
		var s = Scenario.Match(layout: layout).Rich();
		var bot = new BotPlayer(0);
		s.World.Until(() => bot.ModulesBuilt.Contains("utgångsförsvar"), 6 * 60, "the early defence", bot);
		var planned = bot.PlannedSteps.ToList();
		var gates = BotPlayer.WallLayerTiles(layout, 0).Where(t => t.Gate).ToList();
		Assert.All(gates, g => Assert.Contains(planned, p => p.Type == BuildingType.Gate && p.X == g.X && p.Y == g.Y));
		int towers = planned.Count(p => p.Type is BuildingType.FoamTower or BuildingType.Catapult && p.X >= 55);
		Assert.True(towers >= 2, $"{towers} towers by the way in");
	}
}
