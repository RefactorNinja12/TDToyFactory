using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class TentTests
{
	[Fact]
	public void Tent_MakesScouts_TwoPerTent_WithoutPower()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var tent = s.Place<UnitFactory>(BuildingType.Tent, 20, 20);
		Assert.Equal(UnitType.Scout, tent.Produces);
		for (int i = 0; i < 4; i++)
		{
			foreach (var input in tent.Recipe) s.Feed(tent, input.Type, input.Amount);
			s.World.Ticks(UnitStats.BuildTicks(UnitType.Scout) + 1);
		}
		Assert.Equal(UnitStats.ScoutsPerTent, s.World.CountUnits(0, UnitType.Scout));
		Assert.True(UnitStats.IsWorker(UnitType.Scout));
		Assert.Equal(UnitStats.MaxScouts, UnitStats.Cap(UnitType.Scout));
	}

	[Fact]
	public void Scout_LightsFarther_ThanOtherWorkers() =>
		Assert.True(VisionStats.UnitRadius(UnitType.Scout) > VisionStats.UnitRadius(UnitType.Builder));
}

public class ScoutTests
{
	private static Scenario Fog() => Scenario.Match().NoWorkers().Instant().Rich().RealFog();

	private static int SecondsToFindBatteries(Scenario s, int scouts)
	{
		for (int i = 0; i < scouts; i++) s.Spawn(UnitType.Scout, 9, 30 + i);
		return s.World.Until(() => s.World.IsExplored(0, 41, 47), 300, "the battery patch explored") / WorldRunner.TicksPerSecond;
	}

	[Fact]
	public void OneScout_FindsTheBatteryPatch()
	{
		int seconds = SecondsToFindBatteries(Fog(), 1);
		Assert.True(seconds <= 180, $"took {seconds} s"); // the patch is in the far corner: wide before deep finds it late
	}

	[Fact]
	public void Target_IsTheFogEdgeNearestHome_WideBeforeDeep()
	{
		var s = Fog();
		var scout = s.Spawn(UnitType.Scout, 9, 30);
		s.World.Ticks(1);
		Assert.True(scout.ScoutTargetX >= 0);
		Assert.True(s.World.IsFrontier(0, scout.ScoutTargetX, scout.ScoutTargetY));
		var home = s.World.HomeDistance(0);
		int width = s.World.Map.Width;
		int chosen = home[scout.ScoutTargetY * width + scout.ScoutTargetX];
		int nearest = int.MaxValue;
		for (int i = 0; i < home.Length; i++)
			if (home[i] >= 0 && s.World.IsFrontier(0, i % width, i / width))
				nearest = System.Math.Min(nearest, home[i]);
		// Standing at home, the scout's own distance adds little: it goes for (about) the nearest fog edge.
		Assert.InRange(chosen, nearest, nearest + 3);
	}

	[Fact]
	public void TwoScouts_SpreadOut_AndExploreMore()
	{
		var one = Fog();
		one.Spawn(UnitType.Scout, 9, 30);
		var two = Fog();
		var a = two.Spawn(UnitType.Scout, 9, 30);
		var b = two.Spawn(UnitType.Scout, 9, 31);
		two.World.Ticks(1);
		int dx = a.ScoutTargetX - b.ScoutTargetX, dy = a.ScoutTargetY - b.ScoutTargetY;
		Assert.True(dx * dx + dy * dy >= ScoutStats.SpreadTiles * ScoutStats.SpreadTiles, $"targets {dx},{dy} apart");
		one.World.Seconds(40);
		two.World.Seconds(40);
		Assert.True(two.World.ExploredCount(0) > one.World.ExploredCount(0) * 11 / 10,
			$"two scouts {two.World.ExploredCount(0)} tiles, one {one.World.ExploredCount(0)}");
	}

	[Fact]
	public void Lamps_MakeTheSearchFaster()
	{
		int without = SecondsToFindBatteries(Fog(), 1);
		var lit = Fog();
		lit.Place(BuildingType.Lamp, 22, 38);
		lit.Place(BuildingType.Lamp, 30, 42);
		int with = SecondsToFindBatteries(lit, 1);
		Assert.True(with < without, $"with lamps {with} s, without {without} s");
	}

	[Fact]
	public void SeesAnEnemy_RunsTheOtherWay()
	{
		var s = Fog();
		var scout = s.Spawn(UnitType.Scout, 30, 30);
		s.World.Ticks(VisionStats.VisionTicks);
		var enemy = s.Spawn(UnitType.PlasticSoldier, 34, 30, owner: 1);
		s.World.Ticks(VisionStats.VisionTicks + 1);
		int before = scout.X;
		s.World.Seconds(2);
		Assert.True(scout.X < before - UnitStats.SubTile * 2, "ran west, away from the enemy in the east");
		Assert.True(scout.Health > 0);
	}

	[Fact]
	public void NothingLeftToExplore_GoesHome()
	{
		var s = Fog();
		s.World.ExploreAll(0);
		var scout = s.Spawn(UnitType.Scout, 40, 45);
		s.World.Until(() => System.Math.Abs(scout.TileX - 7) <= 2 && System.Math.Abs(scout.TileY - 31) <= 2, 60, "back at the toybox");
		Assert.Equal(-1, scout.ScoutTargetX);
	}
}
