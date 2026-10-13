using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class SamuraiTests
{
	private const int Sub = UnitStats.SubTile;

	/// <summary>A samurai of player 0 on open floor, and enemy farmers (they stand still) at offsets from it.</summary>
	private static (Scenario S, Unit Samurai, Unit[] Enemies) Setup(params (int DX, int DY)[] offsets)
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var samurai = s.Spawn(UnitType.Samurai, 40, 30);
		var enemies = offsets.Select(o =>
		{
			var e = s.Spawn(UnitType.Farmer, 40, 30, owner: 1);
			e.X = e.PrevX = samurai.X + o.DX;
			e.Y = e.PrevY = samurai.Y + o.DY;
			return e;
		}).ToArray();
		return (s, samurai, enemies);
	}

	private static bool Hurt(Unit unit) => unit.Health < UnitStats.MaxHealth(unit.Type);

	[Fact]
	public void Arc_IsInFront_WithinReach()
	{
		var (_, samurai, e) = Setup((Sub, 0), (Sub, Sub * 3 / 4), (-Sub, 0), (0, Sub), (Sub * 2, 0));
		Assert.True(SamuraiStats.InArc(samurai, e[0], e[1]));   // 37° off to the side, in reach
		Assert.False(SamuraiStats.InArc(samurai, e[0], e[2]));  // behind
		Assert.False(SamuraiStats.InArc(samurai, e[0], e[3]));  // straight to the side (90°)
		Assert.False(SamuraiStats.InArc(samurai, e[0], e[4]));  // in front but too far
	}

	[Fact]
	public void Sweep_HitsEveryEnemyInTheArc_AndNobodyElse()
	{
		var (s, samurai, e) = Setup((Sub * 9 / 10, 0), (Sub, Sub * 3 / 4), (-Sub, 0), (0, Sub * 5 / 4), (Sub * 2, 0));
		var friend = s.Spawn(UnitType.Farmer, 40, 30);
		friend.X = samurai.X + Sub;
		friend.Y = samurai.Y - Sub / 2;
		s.World.Until(() => samurai.SwingCount == 1, 5, "a swing");
		s.World.Ticks(UnitStats.ShotTravelTicks(UnitType.Samurai) + 1);
		Assert.True(Hurt(e[0]));
		Assert.True(Hurt(e[1]));
		Assert.False(Hurt(e[2]));
		Assert.False(Hurt(e[3]));
		Assert.False(Hurt(e[4]));
		Assert.False(Hurt(friend));
		Assert.Equal(UnitStats.MaxHealth(UnitType.Farmer) - UnitStats.Damage(UnitType.Samurai), e[1].Health);
	}

	[Fact]
	public void Sweep_HitsAtMostFive()
	{
		var offsets = Enumerable.Range(0, 8).Select(i => (Sub * 4 / 5 + i * 8, (i - 4) * 24)).ToArray();
		var (s, samurai, e) = Setup(offsets);
		s.World.Until(() => samurai.SwingCount == 1, 5, "a swing");
		s.World.Ticks(UnitStats.ShotTravelTicks(UnitType.Samurai) + 1);
		Assert.Equal(SamuraiStats.SweepMaxTargets, e.Count(Hurt));
	}

	[Fact]
	public void Slash_IsWeakAgainstGolems_StrongerAgainstCars()
	{
		Assert.Equal(50, Combat.DamagePercent(DamageKind.Slash, ArmorClass.Brick));
		Assert.Equal(125, Combat.DamagePercent(DamageKind.Slash, ArmorClass.Electronic));
		Assert.Equal(100, Combat.DamagePercent(DamageKind.Slash, ArmorClass.Plastic));
	}

	[Fact]
	public void Dojo_TrainsASamurai_FromPlasticSpringsAndGears()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var dojo = s.Place(BuildingType.Dojo, 20, 20);
		s.Feed(dojo, ItemType.Plastic, 3);
		s.Feed(dojo, ItemType.Spring, 1);
		s.Feed(dojo, ItemType.Gear, 1);
		s.World.Until(() => s.World.CountUnits(0, UnitType.Samurai) == 1, 20, "a samurai");
	}

	[Fact]
	public void Dojo_WithoutPower_Waits()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var dojo = s.Place(BuildingType.Dojo, 40, 40);
		s.Feed(dojo, ItemType.Plastic, 3);
		s.Feed(dojo, ItemType.Spring, 1);
		s.Feed(dojo, ItemType.Gear, 1);
		s.World.Seconds(20);
		Assert.Equal(0, s.World.CountUnits(0, UnitType.Samurai));
		Assert.True(dojo.NoPower);
	}

	[Fact]
	public void Samurai_IsDeterministic()
	{
		ulong Run()
		{
			var s = Scenario.Match().NoWorkers().Instant().Rich();
			for (int i = 0; i < 3; i++)
				s.Spawn(UnitType.Samurai, 70, 27 + i * 3);
			for (int i = 0; i < 8; i++)
				s.Spawn(UnitType.PlasticSoldier, 85, 26 + i, owner: 1);
			s.World.Seconds(30);
			return s.World.Checksum();
		}
		Assert.Equal(Run(), Run());
	}
}
