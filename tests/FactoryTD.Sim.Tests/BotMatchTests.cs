using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace FactoryTD.Sim.Tests;

// The longest whole matches, each in a class of its own: xUnit runs classes side by side, but the tests in one class one
// after the other.

/// <summary>The bot alone, growing for a whole long match. Slow.</summary>
[Trait("Speed", "Slow")]
public class BotGrowthMatchTests
{
	private readonly ITestOutputHelper _out;
	public BotGrowthMatchTests(ITestOutputHelper output) => _out = output;

	[Fact]
	public void Bot_KeepsGrowing_TwentyFiveMinutes()
	{
		var s = Scenario.Match(obstacles: true).RealPower().RealFog();
		s.World.CoresInvulnerable = true;
		var bot = new BotPlayer(1);
		var p = s.World.Players[1];
		int Count(System.Func<Building, bool> what) => s.World.Buildings.Count(b => b.Owner == 1 && b.IsBuilt && what(b));
		var buildings = new Dictionary<int, int>();
		var towers = new Dictionary<int, int>();
		var armies = new Dictionary<int, int>();
		int walls = 0, wizards = 0;
		var log = new System.Text.StringBuilder();
		for (int minute = 5; minute <= 25; minute += 5)
		{
			// Armies die at the front in a real match: here they fall every minute (else they'd eat it all).
			for (int m = 0; m < 5; m++)
			{
				s.World.Minutes(1, bot);
				wizards += s.World.CountUnits(1, UnitType.Wizard);
				foreach (var unit in s.World.Units)
					if (unit.Owner == 1 && !UnitStats.IsWorker(unit.Type))
						unit.Health = 0;
			}
			buildings[minute] = Count(_ => true);
			towers[minute] = Count(b => b is Tower);
			armies[minute] = Count(b => b is UnitFactory f && !UnitStats.IsWorker(f.Produces));
			walls = Count(b => b is Wall);
			log.Append($" | min {minute}: buildings {buildings[minute]}, towers {towers[minute]}, army factories {armies[minute]}, walls {walls}, dojos {s.World.CountBuildings(1, BuildingType.Dojo)}, " +
				$"army {s.World.Units.Count(u => u.Owner == 1 && !UnitStats.IsWorker(u.Type))}, bricks {p.GetCount(ItemType.Brick)}, " +
				$"plastic {p.GetCount(ItemType.Plastic)}, batteries {p.GetCount(ItemType.Battery)}, food {p.GetCount(ItemType.Food)}");
		}
		log.Append(" | growth " + string.Join(" ", bot.GrowthLog));
		_out.WriteLine(log.ToString());
		Assert.True(buildings[25] >= buildings[20] + 20 && buildings[20] >= buildings[15] + 20, $"buildings {buildings[15]} -> {buildings[25]}{log}");
		Assert.True(towers[25] >= towers[10] + 3, $"towers {towers[10]} -> {towers[25]}");
		Assert.True(armies[25] >= armies[10] + 2, $"army factories {armies[10]} -> {armies[25]}");
		Assert.True(walls >= 10, $"walls {walls}");
		Assert.True(s.World.CountBuildings(1, BuildingType.Dojo) >= 1, $"no dojo{log} | {string.Join(",", bot.ModulesBuilt)}");
		Assert.True(s.World.CountBuildings(1, BuildingType.WizardTower) >= 1, $"no wizard tower{log} | {string.Join(",", bot.ModulesBuilt)}");
		Assert.True(wizards >= 1, $"no wizard trained{log}");
	}
}

/// <summary>Bot against bot: a healthy economy over a whole match. Slow.</summary>
[Trait("Speed", "Slow")]
public class BotEconomyMatchTests
{
	private readonly ITestOutputHelper _out;
	public BotEconomyMatchTests(ITestOutputHelper output) => _out = output;

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
				// Crops piling up only says there are too few kitchens while the kitchens can get rid of their food: once the
				// food store is full, they hold on to what they cooked and stop, and the crops wait in the toybox.
				bool kitchensStuck = s.World.Buildings.OfType<Kitchen>().Any(k => k.Owner == p && k.Finished > 0);
				if (!kitchensStuck)
					maxCrops[p] = System.Math.Max(maxCrops[p], s.World.Players[p].GetCount(ItemType.Crop));
				maxUnits[p] = System.Math.Max(maxUnits[p], s.World.Units.Count(u => u.Owner == p && !UnitStats.IsWorker(u.Type)));
			}
		}
		for (int p = 0; p < 2; p++)
			_out.WriteLine($"player {p}: starving {starvingTicks[p] / WorldRunner.TicksPerSecond} s, max stored crops while the kitchens could deliver {maxCrops[p]}, " +
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
}

/// <summary>Bot against bot on the many-rooms layout. Slow.</summary>
[Trait("Speed", "Slow")]
public class ManyRoomsMatchTests
{
	private readonly ITestOutputHelper _out;
	public ManyRoomsMatchTests(ITestOutputHelper output) => _out = output;

	/// <summary>The winding way on ManyRooms makes a long match: bot against bot, nobody wins in the first ten minutes.</summary>
	[Fact]
	public void BotVsBot_ManyRooms_LastsAtLeastTenMinutes()
	{
		const int minutes = 10;
		var s = Scenario.Match(obstacles: true, layout: MapLayoutKind.ManyRooms).RealPower().RealFog();
		var bots = new[] { new BotPlayer(0), new BotPlayer(1) };
		int[] maxArmy = new int[2];
		for (int minute = 0; minute < minutes && s.World.Winner < 0; minute++)
		{
			s.World.Minutes(1, bots);
			for (int p = 0; p < 2; p++)
				maxArmy[p] = System.Math.Max(maxArmy[p], s.World.Units.Count(u => u.Owner == p && !UnitStats.IsWorker(u.Type)));
		}
		string state = $"many rooms after {s.World.TickCount / WorldRunner.TicksPerSecond} s: winner {s.World.Winner}, " +
			$"cores {s.World.GetCore(0)?.Health}/{s.World.GetCore(1)?.Health}, max armies {maxArmy[0]}/{maxArmy[1]}";
		_out.WriteLine(state);
		Assert.True(s.World.Winner < 0, state);
		Assert.True(maxArmy[0] > 0 && maxArmy[1] > 0, state); // both sides fought, it isn't just quiet
	}
}
