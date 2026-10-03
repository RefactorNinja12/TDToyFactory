using System.Collections.Generic;
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
		var s = Scenario.Match(obstacles: true).RealPower().RealFog();
		var bot = new BotPlayer(1);
		int ticks = s.World.Until(() => s.World.Winner >= 0, 20 * 60, "a winner", bot);
		_out.WriteLine($"bot won after {ticks / WorldRunner.TicksPerSecond} s");
		Assert.Equal(1, s.World.Winner);
		Assert.True(ticks <= 12 * WorldRunner.TicksPerMinute, $"took {ticks / WorldRunner.TicksPerSecond} s");
	}

	[Fact]
	public void BotVsBot_FifteenMinutes_HealthyEconomy()
	{
		var s = Scenario.Match(obstacles: true).RealPower().RealFog();
		var bots = new[] { new BotPlayer(0), new BotPlayer(1) };
		int[] starvingTicks = new int[2];
		int[] maxUnits = new int[2];
		int[] maxCrops = new int[2];
		int[] firstBatteryExtractor = { -1, -1 };
		int[] maxHunters = new int[2];
		// Army factories/assemblers/towers without power, in the first 8 minutes (later a losing side's grid
		// gets shot to pieces, which is fair).
		int[] factoryTicks = new int[2], unpoweredTicks = new int[2];
		for (int t = 0; t < 15 * WorldRunner.TicksPerMinute && s.World.Winner < 0; t++)
		{
			s.World.Ticks(1, bots);
			for (int p = 0; p < 2; p++)
			{
				if (s.World.Players[p].Starving) starvingTicks[p]++;
				foreach (var b in s.World.TickCount < 8 * WorldRunner.TicksPerMinute ? s.World.Buildings : new List<Building>())
				{
					if (b.Owner != p || !b.IsBuilt || !(b is Tower or Assembler || (b is UnitFactory f && !UnitStats.IsWorker(f.Produces))))
						continue;
					factoryTicks[p]++;
					if (b.NoPower) unpoweredTicks[p]++;
				}
				maxHunters[p] = System.Math.Max(maxHunters[p], s.World.CountUnits(p, UnitType.CheeseHunter));
				if (firstBatteryExtractor[p] < 0 && s.World.CountBuildings(p, BuildingType.BatteryExtractor) > 0)
					firstBatteryExtractor[p] = (int)(s.World.TickCount / WorldRunner.TicksPerSecond);
				maxCrops[p] = System.Math.Max(maxCrops[p], s.World.Players[p].GetCount(ItemType.Crop));
				maxUnits[p] = System.Math.Max(maxUnits[p], s.World.Units.Count(u => u.Owner == p && !UnitStats.IsWorker(u.Type)));
			}
		}
		for (int p = 0; p < 2; p++)
			_out.WriteLine($"player {p}: starving {starvingTicks[p] / WorldRunner.TicksPerSecond} s, max stored crops {maxCrops[p]}, " +
				$"kitchens {s.World.CountBuildings(p, BuildingType.Kitchen)}, max army {maxUnits[p]}, " +
				$"first battery extractor at {firstBatteryExtractor[p]} s, max cheese hunters {maxHunters[p]}, consumers without power {unpoweredTicks[p] * 100 / System.Math.Max(1, factoryTicks[p])}% (first 8 min), " +
				$"modules {string.Join(",", bots[p].ModulesBuilt)}");
		_out.WriteLine($"winner {s.World.Winner} at {s.World.TickCount / WorldRunner.TicksPerSecond} s");
		for (int p = 0; p < 2; p++)
		{
			Assert.True(starvingTicks[p] <= 60 * WorldRunner.TicksPerSecond, $"player {p} starved {starvingTicks[p] / WorldRunner.TicksPerSecond} s");
			Assert.True(maxUnits[p] >= 5, $"player {p} never had an army (max {maxUnits[p]})");
			Assert.Contains("mat", bots[p].ModulesBuilt);
			Assert.InRange(firstBatteryExtractor[p], 0, 5 * 60);
			Assert.True(maxHunters[p] >= 1, $"player {p} never trained a cheese hunter");
			Assert.True(unpoweredTicks[p] * 100 <= factoryTicks[p] * 10, $"player {p}: consumers without power {unpoweredTicks[p] * 100 / System.Math.Max(1, factoryTicks[p])}% of the time");
			Assert.True(maxCrops[p] <= 300, $"player {p} piled up {maxCrops[p]} crops (cap {PlayerState.CoreCapacity}): too few kitchens");
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

	[Theory]
	[InlineData(UnitType.BrickGolem)]
	[InlineData(UnitType.RcCar)]
	public void Balance_FromTheHallsEnd_UnitsFightAtTheEnemyToyboxBeforeTurningBack(UnitType type)
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		s.Pylon(92, 30); // the last forward pylon a player can build (the hall ends at x = 97)
		s.Charger(92, 31, energy: PowerStats.ChargerCapacity);
		var unit = s.Spawn(type, 92, 30);
		var core = s.World.GetCore(1);
		int firstHit = -1, ticks = 0;
		while (unit.PowerState == UnitPower.Normal && ticks < 5 * WorldRunner.TicksPerMinute)
		{
			s.World.Tick();
			ticks++;
			if (firstHit < 0 && core.Health < core.MaxHealth)
				firstHit = ticks;
		}
		int fighting = firstHit < 0 ? 0 : (ticks - firstHit) / WorldRunner.TicksPerSecond;
		_out.WriteLine($"{type}: reached the toybox after {firstHit / WorldRunner.TicksPerSecond} s, fought {fighting} s, " +
			$"did {core.MaxHealth - core.Health} damage, then {unit.PowerState}");
		Assert.True(fighting >= 10, $"{type} fought only {fighting} s at the enemy toybox");
	}

	[Fact]
	public void Balance_OneBatteryAMinute_RunsTwoFactoriesAndTwoBusyTowers()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		s.Pylon(33, 22);
		var charger = s.Charger(34, 22);
		var factories = new[]
		{
			s.Place<UnitFactory>(BuildingType.SoldierFactory, 30, 20),
			s.Place<UnitFactory>(BuildingType.SoldierFactory, 35, 20),
		};
		var towers = new[] { s.Place<Tower>(BuildingType.FoamTower, 31, 24), s.Place<Tower>(BuildingType.LaserTower, 35, 24) };
		// Targets that never die and never fight back: flat enemy golems crawl, so put them back every tick.
		var targets = new[] { s.Spawn(UnitType.BrickGolem, 33, 26, owner: 1), s.Spawn(UnitType.BrickGolem, 34, 26, owner: 1) };
		s.Feed(charger, ItemType.Battery, 1);
		int working = 0, unpowered = 0;
		for (int t = 0; t < 5 * WorldRunner.TicksPerMinute; t++)
		{
			if (t > 0 && t % WorldRunner.TicksPerMinute == 0)
				s.Feed(charger, ItemType.Battery, 1);
			foreach (var f in factories)
				if (!f.Crafter.CanWork(f.Recipe))
					foreach (var input in f.Recipe) s.Feed(f, input.Type, input.Amount);
			foreach (var tower in towers)
				s.Feed(tower, tower.Stats.Ammo, 1);
			for (int k = 0; k < targets.Length; k++)
			{
				targets[k].Charge = 0;
				targets[k].Health = UnitStats.MaxHealth(UnitType.BrickGolem);
				targets[k].X = (33 + k) * UnitStats.SubTile + UnitStats.SubTile / 2;
				targets[k].Y = 26 * UnitStats.SubTile + UnitStats.SubTile / 2;
			}
			s.World.Tick();
			s.World.ClearSoldiers();
			foreach (var f in factories) { working++; if (f.NoPower) unpowered++; }
			foreach (var tower in towers) { working++; if (tower.NoPower) unpowered++; }
		}
		_out.WriteLine($"without power {unpowered * 100 / working}% of the time, used {s.P0.EnergyUsedLastMinute * 100 / PowerStats.EnergyPerBattery}% of a battery in the last minute, {charger.Energy} left");
		Assert.True(unpowered * 100 <= working * 5, $"without power {unpowered * 100 / working}% of the time");
	}

	private static Scenario Fog(bool lamps)
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealFog();
		if (lamps)
		{
			s.Place(BuildingType.Lamp, 22, 38);
			s.Place(BuildingType.Lamp, 30, 42);
		}
		return s;
	}

	private static int SecondsToFindBatteries(int scouts, bool lamps)
	{
		var s = Fog(lamps);
		for (int i = 0; i < scouts; i++) s.Spawn(UnitType.Scout, 9, 30 + i);
		return s.World.Until(() => s.World.IsExplored(0, 41, 47), 300, "the battery patch explored") / WorldRunner.TicksPerSecond;
	}

	[Fact]
	public void Balance_Scouting_FindsTheBatteryPatch_FasterWithMoreScouts_LampsExploreMore()
	{
		int one = SecondsToFindBatteries(1, false), two = SecondsToFindBatteries(2, false);
		int dark = ExploredAfterAMinute(lamps: false), lit = ExploredAfterAMinute(lamps: true);
		_out.WriteLine($"battery patch found by 1 scout in {one} s, 2 scouts {two} s; explored after 60 s: {dark} tiles, with 2 lamps {lit}");
		Assert.True(one <= 180, $"1 scout took {one} s (wide before deep: the patch is in the far corner)");
		Assert.True(two < one, $"2 scouts {two} s, 1 scout {one} s");
		Assert.True(lit > dark, $"with lamps {lit} tiles explored, without {dark}");
	}

	private static int ExploredAfterAMinute(bool lamps)
	{
		var s = Fog(lamps);
		s.Spawn(UnitType.Scout, 9, 30);
		s.World.Seconds(60);
		return s.World.ExploredCount(0);
	}

	[Fact]
	public void Balance_OneCheeseMelter_KeepsAKitchenCooking()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		s.Place(BuildingType.CheeseMelter, 6, 22);                  // on the cheese patch
		s.Place(BuildingType.Kitchen, 6, 21, Direction.North);      // filled straight by the melter
		s.Place(BuildingType.Warehouse, 6, 19);                     // the food goes into storage
		s.World.Minutes(2);
		int perMinute = s.P0.FoodProducedLastMinute;
		_out.WriteLine($"one cheese melter + kitchen: {perMinute} food/min");
		Assert.True(perMinute >= 15, $"only {perMinute} food/min");
	}

	[Fact]
	public void SimSpeed_BotVsBot_MillisecondsPerSimulatedMinute()
	{
		var s = Scenario.Match(obstacles: true).RealPower().RealFog();
		var bots = new[] { new BotPlayer(0), new BotPlayer(1) };
		s.World.Ticks(WorldRunner.TicksPerMinute * 4, bots); // into the mid game
		var watch = System.Diagnostics.Stopwatch.StartNew();
		s.World.Ticks(WorldRunner.TicksPerMinute * 2, bots);
		long perMinute = watch.ElapsedMilliseconds / 2;
		_out.WriteLine($"sim speed: {perMinute} ms per simulated minute ({s.World.Units.Count} units, {s.World.Buildings.Count} buildings)");
		Assert.True(perMinute < 6000, $"{perMinute} ms per simulated minute: too slow to play in real time");
	}
}
