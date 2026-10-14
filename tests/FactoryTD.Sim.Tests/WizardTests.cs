using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class WizardTests
{
	// Player 1's farmers stand still in player 0's hall (no farm to go to): dummies to strike. Plastic, 20 health.
	private static Scenario Hall() => Scenario.Match().NoWorkers().Instant().Rich();

	private static int FullHealth => UnitStats.MaxHealth(UnitType.Farmer);
	private static readonly int PlasticHit = UnitStats.Damage(UnitType.Wizard) * 2; // lightning: double against plastic
	private const int Delay = WizardStats.BoltDelayTicks;

	/// <summary>A wizard at (70, 30) and an enemy farmer 4 tiles east (in range); the first tick casts.</summary>
	private static (Scenario S, Unit Wizard, Unit Target) Duel()
	{
		var s = Hall();
		var wizard = s.Spawn(UnitType.Wizard, 70, 30);
		var target = s.Spawn(UnitType.Farmer, 74, 30, owner: 1);
		return (s, wizard, target);
	}

	[Fact]
	public void Tower_TrainsAWizard_FromBatteriesACircuitAndASpring()
	{
		var s = Hall();
		var tower = s.Place(BuildingType.WizardTower, 20, 20);
		Assert.Equal(UnitType.Wizard, ((UnitFactory)tower).Produces);
		s.Feed(tower, ItemType.Battery, 2);
		s.Feed(tower, ItemType.CircuitBoard, 1);
		s.Feed(tower, ItemType.Spring, 1);
		s.World.Until(() => s.World.CountUnits(0, UnitType.Wizard) == 1, 20, "a wizard comes out");
	}

	[Fact]
	public void Tower_StopsWithoutPower()
	{
		var s = Hall().RealPower();
		var tower = s.Place(BuildingType.WizardTower, 20, 20);
		s.Feed(tower, ItemType.Battery, 2);
		s.Feed(tower, ItemType.CircuitBoard, 1);
		s.Feed(tower, ItemType.Spring, 1);
		s.World.Seconds(20);
		Assert.Equal(0, s.World.CountUnits(0, UnitType.Wizard));
		Assert.True(tower.NoPower);
	}

	[Fact]
	public void Casts_ABoltThatFallsOnTheSpot_AfterADelay()
	{
		var (s, _, target) = Duel();
		s.World.Ticks(1);
		var bolt = Assert.Single(s.World.Projectiles);
		Assert.Equal(DamageKind.Lightning, bolt.Kind);
		Assert.Equal((target.X, target.Y), (bolt.ToX, bolt.ToY));
		s.World.Ticks(Delay - 2);
		Assert.Equal(FullHealth, target.Health); // nothing before it strikes (the cast tick counts)
		s.World.Ticks(1);
		Assert.Equal(FullHealth - PlasticHit, target.Health);
		Assert.Contains(s.World.RecentImpacts, m => m.Bolt);
	}

	[Fact]
	public void ATargetThatStepsAside_IsMissed()
	{
		var (s, _, target) = Duel();
		s.World.Ticks(1);
		target.X += UnitStats.SubTile * 2; // out of the way before it falls
		s.World.Ticks(Delay);
		Assert.Equal(FullHealth, target.Health);
	}

	[Fact]
	public void HitsEveryEnemyWithinTheRadius_NotOutside_NorItsOwn()
	{
		var (s, _, target) = Duel();
		var near = s.Spawn(UnitType.Farmer, 74, 30, owner: 1);
		near.Y += UnitStats.SubTile / 2; // half a tile from the strike
		var far = s.Spawn(UnitType.Farmer, 74, 32, owner: 1); // 2 tiles away
		var own = s.Spawn(UnitType.Farmer, 74, 30);
		own.Y -= UnitStats.SubTile / 2;
		s.World.Ticks(Delay + 1);
		Assert.Equal(FullHealth - PlasticHit, target.Health);
		Assert.Equal(FullHealth - PlasticHit, near.Health);
		Assert.Equal(FullHealth, far.Health);
		Assert.Equal(FullHealth, own.Health);
		Assert.Equal(0, own.AirTicks);
	}

	[Fact]
	public void ThrowsThoseHit_OutFromTheStrike_ShorterThanTheSlam()
	{
		var (s, _, target) = Duel();
		var beside = s.Spawn(UnitType.Farmer, 74, 30, owner: 1);
		beside.Y += UnitStats.SubTile / 2;
		int y = beside.Y;
		s.World.Ticks(Delay + 1);
		Assert.Equal(WizardStats.ThrowTicks, beside.AirTicks);
		Assert.True(WizardStats.ThrowTicks < GolemStats.ThrowTicks && WizardStats.ThrowDistance < GolemStats.ThrowDistance);
		s.World.Ticks(WizardStats.ThrowTicks + 1);
		Assert.True(beside.Y > y + UnitStats.SubTile / 2, "thrown south, away from the strike");
		Assert.Equal(0, beside.AirTicks); // landed
	}

	[Fact]
	public void AgainstABuilding_ASingleBoltAtHalfDamage()
	{
		var s = Hall();
		var belt = s.Place(BuildingType.Conveyor, 66, 30);
		s.Spawn(UnitType.Wizard, 70, 30, owner: 1);
		int health = belt.Health;
		s.World.Ticks(Delay + 1);
		Assert.Equal(health - UnitStats.Damage(UnitType.Wizard) / 2, belt.Health);
		Assert.DoesNotContain(s.World.RecentImpacts, m => m.Bolt); // no area strike at a building
	}

	[Theory]
	[InlineData(ArmorClass.Plastic, 200)]
	[InlineData(ArmorClass.Brick, 100)]
	[InlineData(ArmorClass.Electronic, 100)]
	public void Lightning_IsDoubleAgainstPlastic(ArmorClass armor, int percent) =>
		Assert.Equal(percent, Combat.DamagePercent(DamageKind.Lightning, armor));

	[Fact]
	public void TheOtherCounters_AreUnchanged()
	{
		Assert.Equal(200, Combat.DamagePercent(DamageKind.Area, ArmorClass.Plastic));
		Assert.Equal(200, Combat.DamagePercent(DamageKind.Laser, ArmorClass.Brick));
		Assert.Equal(300, Combat.DamagePercent(DamageKind.Water, ArmorClass.Electronic));
		Assert.Equal(50, Combat.DamagePercent(DamageKind.Slash, ArmorClass.Brick));
	}

	/// <summary>
	/// Player 0 (west, its wizards a few tiles behind) against a clump of player 1 coming down the hall; true if
	/// player 0 is left standing.
	/// </summary>
	private static bool Player0Wins(int wizards, int soldiers0, UnitType enemy, int enemies)
	{
		var s = Hall();
		var mine = Enumerable.Range(0, wizards).Select(i => s.Spawn(UnitType.Wizard, 64, 30 + i * 2))
			.Concat(Enumerable.Range(0, soldiers0).Select(i => s.Spawn(UnitType.PlasticSoldier, 67, 30 + i * 2 % 6))).ToList();
		var theirs = Enumerable.Range(0, enemies).Select(i => s.Spawn(enemy, 80, 30 + i % 2, owner: 1)).ToList();
		s.World.Until(() => mine.All(u => u.Health <= 0) || theirs.All(u => u.Health <= 0), 60, "one side wins");
		return mine.Any(u => u.Health > 0);
	}

	[Fact]
	public void Balance_AWizard_IsWorthMoreThanTwoSoldiersAgainstAClump()
	{
		Assert.False(Player0Wins(0, 5, UnitType.PlasticSoldier, 6));
		Assert.True(Player0Wins(1, 3, UnitType.PlasticSoldier, 6));
	}

	[Fact]
	public void Balance_AWizardAlone_IsFragile()
	{
		Assert.False(Player0Wins(1, 0, UnitType.PlasticSoldier, 2));
		Assert.False(Player0Wins(1, 0, UnitType.RcCar, 3));
	}
}
