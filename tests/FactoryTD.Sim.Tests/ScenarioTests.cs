using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace FactoryTD.Sim.Tests;

/// <summary>Whole matches. Slow: run with `tests/run.sh -Slow` before commits that touch balance or the bot.</summary>
[Trait("Speed", "Slow")]
public class ScenarioTests
{
	private readonly ITestOutputHelper _out;
	public ScenarioTests(ITestOutputHelper output) => _out = output;

	[Fact]
	public void Bot_BeatsAnIdlePlayer()
	{
		var s = Scenario.Match();
		var bot = new BotPlayer(1);
		int ticks = s.World.Until(() => s.World.Winner >= 0, 20 * 60, "a winner", bot);
		_out.WriteLine($"bot won after {ticks / WorldRunner.TicksPerSecond} s");
		Assert.Equal(1, s.World.Winner);
		Assert.True(ticks <= 12 * WorldRunner.TicksPerMinute, $"took {ticks / WorldRunner.TicksPerSecond} s");
	}

	[Fact]
	public void BotVsBot_FifteenMinutes_HealthyEconomy()
	{
		var s = Scenario.Match();
		var bots = new[] { new BotPlayer(0), new BotPlayer(1) };
		int[] starvingTicks = new int[2];
		int[] maxUnits = new int[2];
		for (int t = 0; t < 15 * WorldRunner.TicksPerMinute && s.World.Winner < 0; t++)
		{
			s.World.Ticks(1, bots);
			for (int p = 0; p < 2; p++)
			{
				if (s.World.Players[p].Starving) starvingTicks[p]++;
				maxUnits[p] = System.Math.Max(maxUnits[p], s.World.Units.Count(u => u.Owner == p && !UnitStats.IsWorker(u.Type)));
			}
		}
		for (int p = 0; p < 2; p++)
			_out.WriteLine($"player {p}: starving {starvingTicks[p] / WorldRunner.TicksPerSecond} s, max army {maxUnits[p]}, " +
				$"modules {string.Join(",", bots[p].ModulesBuilt)}");
		_out.WriteLine($"winner {s.World.Winner} at {s.World.TickCount / WorldRunner.TicksPerSecond} s");
		for (int p = 0; p < 2; p++)
		{
			Assert.True(starvingTicks[p] <= 60 * WorldRunner.TicksPerSecond, $"player {p} starved {starvingTicks[p] / WorldRunner.TicksPerSecond} s");
			Assert.True(maxUnits[p] >= 5, $"player {p} never had an army (max {maxUnits[p]})");
			Assert.Contains("mat", bots[p].ModulesBuilt);
		}
	}

	[Fact]
	public void Balance_SixFieldsTwoKitchens_FeedTwentySoldiers()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().Give(ItemType.Food, 20);
		for (int i = 0; i < 6; i++) s.Place(BuildingType.CropField, 3 + i, 34);
		s.Place(BuildingType.Kitchen, 6, 32, Direction.North); // both straight into the core
		s.Place(BuildingType.Kitchen, 7, 32, Direction.North);
		s.Spawn(UnitType.Farmer, 4, 35);
		s.Spawn(UnitType.Farmer, 7, 35);
		for (int i = 0; i < 20; i++) s.Spawn(UnitType.PlasticSoldier, 2, 5 + i % 20);
		s.World.Minutes(1); // warm up
		int food = s.P0.GetCount(ItemType.Food);
		s.World.Minutes(3);
		_out.WriteLine($"food {food} -> {s.P0.GetCount(ItemType.Food)}, produced last minute {s.P0.FoodProducedLastMinute}, upkeep {s.P0.FoodUpkeepPerMinute}");
		Assert.False(s.P0.Starving);
		Assert.True(s.P0.FoodProducedLastMinute >= s.P0.FoodUpkeepPerMinute,
			$"produced {s.P0.FoodProducedLastMinute}/min, eats {s.P0.FoodUpkeepPerMinute}/min");
	}

	[Theory]
	[InlineData(BuildingType.FoamTower, ItemType.Plastic, UnitType.PlasticSoldier, 3)]
	[InlineData(BuildingType.WaterTower, ItemType.Battery, UnitType.RcCar, 3)]
	[InlineData(BuildingType.LaserTower, ItemType.Battery, UnitType.BrickGolem, 8)]
	public void Balance_TowerKillsItsCounter(BuildingType tower, ItemType ammo, UnitType unit, int maxSeconds)
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var t = s.Place<Tower>(tower, 30, 30);
		s.Feed(t, ammo, 50);
		var target = s.Spawn(unit, 33, 30, owner: 1);
		int ticks = s.World.Until(() => target.Health <= 0, 30, $"{unit} dead");
		_out.WriteLine($"{tower} killed {unit} in {ticks / (double)WorldRunner.TicksPerSecond:0.0} s");
		Assert.True(ticks <= maxSeconds * WorldRunner.TicksPerSecond, $"took {ticks / (double)WorldRunner.TicksPerSecond:0.0} s");
	}
}
