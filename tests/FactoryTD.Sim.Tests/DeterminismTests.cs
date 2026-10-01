using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

/// <summary>Same inputs must give the same game, tick for tick (required for lockstep networking).</summary>
public class DeterminismTests
{
	/// <summary>Checksums every <paramref name="every"/> ticks of a fresh match played by two bots.</summary>
	private static List<ulong> BotMatchChecksums(int ticks, int every)
	{
		var world = World.CreateMatch();
		var bots = new[] { new BotPlayer(0), new BotPlayer(1) };
		var sums = new List<ulong>();
		for (int t = 0; t < ticks && world.Winner < 0; t++)
		{
			foreach (var bot in bots)
				bot.Tick(world);
			world.Tick();
			if (t % every == 0)
				sums.Add(world.Checksum());
		}
		return sums;
	}

	/// <summary>
	/// The recorded result of 2 minutes of bot vs bot (default map and seed): every second's checksum folded
	/// into one number. A refactor must leave it unchanged. When a change is MEANT to change how the game
	/// plays, update the value on purpose (the failure message prints the new one) and say so in the commit.
	/// </summary>
	private const ulong GoldenBotMatch = 0x299E99DC031BB7E0UL;

	[Fact]
	public void Golden_BotMatch_PlaysExactlyAsRecorded()
	{
		var hash = StateHash.Start();
		foreach (ulong sum in BotMatchChecksums(WorldRunner.TicksPerMinute * 2, every: 20))
			hash.Add((long)sum);
		Assert.True(hash.Value == GoldenBotMatch,
			$"Behaviour changed: golden bot match is now 0x{hash.Value:X}UL (was 0x{GoldenBotMatch:X}UL). " +
			"Refactor: find what changed. Intended change: update GoldenBotMatch.");
	}

	[Fact]
	public void SameScenario_SameChecksumsEveryTick()
	{
		static List<ulong> Run()
		{
			var s = Scenario.Match().Rich();
			s.Place(BuildingType.BrickExtractor, 13, 25, Direction.South);
			s.Belt(13, 26, 13, 29).Belt(13, 30, 8, 30);
			s.Place(BuildingType.CropField, 3, 34);
			s.Spawn(UnitType.PlasticSoldier, 100, 30, owner: 1);
			var sums = new List<ulong>();
			for (int t = 0; t < 20 * 60; t++)
			{
				s.World.Tick();
				sums.Add(s.World.Checksum());
			}
			return sums;
		}
		AssertSame(Run(), Run(), "tick");
	}

	/// <summary>Reports only the first point where two runs differ.</summary>
	private static void AssertSame(List<ulong> first, List<ulong> second, string unit)
	{
		for (int i = 0; i < System.Math.Min(first.Count, second.Count); i++)
			Assert.True(first[i] == second[i], $"Runs diverged at {unit} {i}");
		Assert.True(first.Count == second.Count, $"Runs had different lengths: {first.Count} vs {second.Count} {unit}s");
	}

	[Fact]
	[Trait("Speed", "Slow")]
	public void BotVsBot_FiveMinutes_IsDeterministic()
	{
		var first = BotMatchChecksums(WorldRunner.TicksPerMinute * 5, every: 20);
		var second = BotMatchChecksums(WorldRunner.TicksPerMinute * 5, every: 20);
		AssertSame(first, second, "second");
	}

	[Fact]
	public void Checksum_SeesSmallChanges()
	{
		var a = Scenario.Match();
		var b = Scenario.Match();
		Assert.Equal(a.World.Checksum(), b.World.Checksum());

		b.Give(ItemType.Brick, 1);
		Assert.NotEqual(a.World.Checksum(), b.World.Checksum());

		var c = Scenario.Match();
		c.World.Units[0].X += 1;
		Assert.NotEqual(a.World.Checksum(), c.World.Checksum());

		var d = Scenario.Match().Instant();
		var e = Scenario.Match().Instant();
		d.Place(BuildingType.Conveyor, 20, 20);
		e.Place(BuildingType.Conveyor, 20, 20);
		d.Feed(d.World.GetBuilding(20, 20), ItemType.Brick);
		Assert.NotEqual(d.World.Checksum(), e.World.Checksum()); // an item on a belt counts

		var f = Scenario.Match();
		var g = Scenario.Match();
		f.Spawn(UnitType.BrickGolem, 30, 30).Charge -= 1;
		g.Spawn(UnitType.BrickGolem, 30, 30);
		Assert.NotEqual(f.World.Checksum(), g.World.Checksum()); // battery charge counts

		var h = Scenario.Match().Instant();
		var i = Scenario.Match().Instant();
		h.Charger(30, 30, energy: 1);
		i.Charger(30, 30);
		Assert.NotEqual(h.World.Checksum(), i.World.Checksum()); // charger energy counts

		var j = Scenario.Match().RealFog();
		var k = Scenario.Match().RealFog();
		j.World.ExploreAll(0);
		Assert.NotEqual(j.World.Checksum(), k.World.Checksum()); // explored tiles count
	}

	[Fact]
	public void RealFog_ScoutsFleeingAndExploring_IsDeterministic()
	{
		static List<ulong> Run()
		{
			var s = Scenario.Match().NoWorkers().Instant().Rich().RealFog();
			s.Place(BuildingType.Lamp, 30, 42);
			for (int k = 0; k < 3; k++) s.Spawn(UnitType.Scout, 9, 29 + k);
			s.Spawn(UnitType.RcCar, 70, 30);
			s.Spawn(UnitType.PlasticSoldier, 40, 20, owner: 1);
			var sums = new List<ulong>();
			for (int t = 0; t < 20 * 90; t++)
			{
				s.World.Tick();
				sums.Add(s.World.Checksum());
			}
			return sums;
		}
		AssertSame(Run(), Run(), "tick");
	}

	[Fact]
	public void RealPower_ChargingAndReturning_IsDeterministic()
	{
		static List<ulong> Run()
		{
			var s = Scenario.Match().Rich().Instant().RealPower();
			s.Pylon(20, 30);
			var charger = s.Charger(21, 30, energy: 3000);
			s.Place(BuildingType.BatteryExtractor, 41, 47);
			s.Belt(41, 46, 41, 31).Belt(41, 30, 22, 30); // batteries all the way to the charger
			s.Place<UnitFactory>(BuildingType.GolemWorkshop, 16, 26);
			for (int k = 0; k < 4; k++) s.Spawn(UnitType.BrickGolem, 30 + k, 32).Charge = 500 * k;
			for (int k = 0; k < 3; k++) s.Spawn(UnitType.RcCar, 30 + k, 34);
			var sums = new List<ulong>();
			for (int t = 0; t < 20 * 90; t++)
			{
				s.World.Tick();
				sums.Add(s.World.Checksum());
			}
			return sums;
		}
		AssertSame(Run(), Run(), "tick");
	}
}
