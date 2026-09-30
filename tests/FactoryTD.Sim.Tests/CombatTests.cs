using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class FlowFieldTests
{
	[Fact]
	public void LeadsToTheEnemyCore_AroundWalls()
	{
		var s = Scenario.Match();
		var field = s.World.GetFlowField(0);
		var enemyCore = s.World.GetCore(1);
		Assert.Equal(0, field.Distance(enemyCore.X, enemyCore.Y));
		Assert.Equal(FlowField.Unreachable, field.Distance(0, 0)); // wall
		// Walking downhill from the own room reaches the enemy core.
		int x = 20, y = 10, steps = 0;
		while (field.Distance(x, y) > 0 && steps++ < 1000)
			(x, y) = field.NextTile(x, y);
		Assert.Equal(0, field.Distance(x, y));
	}

	[Fact]
	public void EnemyBuildingsCostExtra_OwnBuildingsDont()
	{
		var s = Scenario.Match().Rich().Instant();
		int before0 = s.World.GetFlowField(0).Distance(40, 30);
		int before1 = s.World.GetFlowField(1).Distance(40, 30);
		s.Place(BuildingType.Conveyor, 40, 30); // player 0's belt in player 0's room
		Assert.Equal(before0, s.World.GetFlowField(0).Distance(40, 30));
		Assert.True(s.World.GetFlowField(1).Distance(40, 30) > before1);
	}
}

public class TargetingTests
{
	[Fact]
	public void Priority_Units_Towers_Buildings_CoreLast()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var attacker = s.Spawn(UnitType.PlasticSoldier, 9, 30, owner: 1); // right next to player 0's core
		int range = UnitStats.Range(attacker.Type);
		Building Target() => s.World.SelectTarget(attacker, range, includeCore: true).Item2;

		Assert.IsType<Core>(Target());
		var belt = s.Place(BuildingType.Conveyor, 10, 31);
		Assert.Same(belt, Target());
		var tower = s.Place(BuildingType.FoamTower, 11, 29);
		Assert.Same(tower, Target());
		var defender = s.Spawn(UnitType.PlasticSoldier, 11, 30);
		Assert.Same(defender, s.World.SelectTarget(attacker, range, includeCore: true).Item1);
	}

	[Fact]
	public void Golem_IgnoresUnits()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var golem = s.Spawn(UnitType.BrickGolem, 20, 20, owner: 1);
		s.Spawn(UnitType.PlasticSoldier, 21, 20);
		var belt = s.Place(BuildingType.Conveyor, 21, 21);
		var (unit, building) = s.World.SelectTarget(golem, UnitStats.Range(golem.Type), includeCore: true);
		Assert.Null(unit);
		Assert.Same(belt, building);
	}

	[Fact]
	public void Soldier_ShootsBuildingsDownBeforeTheCore()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var belt = s.Place(BuildingType.Conveyor, 12, 30); // 5 tiles from the core: in sight, out of range
		s.Spawn(UnitType.PlasticSoldier, 16, 32, owner: 1);
		s.World.Until(() => s.World.GetBuilding(12, 30) == null, 60, "belt destroyed");
		Assert.Equal(s.World.GetCore(0).MaxHealth, s.World.GetCore(0).Health);
	}

	[Fact]
	public void CoreAtZero_EndsTheMatch()
	{
		var s = Scenario.Match().NoWorkers();
		for (int i = 0; i < 20; i++) s.Spawn(UnitType.PlasticSoldier, 9, 22 + i % 18, owner: 1);
		s.World.Until(() => s.World.Winner >= 0, 300, "a winner");
		Assert.Equal(1, s.World.Winner);
	}
}

public class TowerTests
{
	[Theory]
	[InlineData(BuildingType.FoamTower, ItemType.Plastic)]
	[InlineData(BuildingType.Catapult, ItemType.Brick)]
	[InlineData(BuildingType.WaterTower, ItemType.Battery)]
	[InlineData(BuildingType.LaserTower, ItemType.Battery)]
	public void TakesOnlyItsAmmo_UpToTheMagazine(BuildingType type, ItemType ammo)
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var tower = s.Place<Tower>(type, 20, 20);
		Assert.Equal(0, s.Feed(tower, ItemType.Gear));
		int taken = s.Feed(tower, ammo, 1000);
		Assert.Equal(tower.Stats.MaxShots / tower.Stats.ShotsPerItem, taken);
		Assert.Equal(taken * tower.Stats.ShotsPerItem, tower.Shots);
	}

	[Fact]
	public void NoAmmo_NoShots_AndOnlyWithinRange()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var tower = s.Place<Tower>(BuildingType.FoamTower, 20, 20);
		var far = s.Spawn(UnitType.PlasticSoldier, 20 + tower.Stats.RangeTiles + 3, 20, owner: 1);
		s.World.Ticks(5);
		Assert.Empty(s.World.Projectiles);
		s.Feed(tower, ItemType.Plastic, 2);
		s.World.Ticks(5);
		Assert.Empty(s.World.Projectiles.Where(p => p.TargetUnit == far)); // out of range
	}

	[Fact]
	public void Fires_OncePerReload()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var tower = s.Place<Tower>(BuildingType.FoamTower, 20, 20);
		s.Feed(tower, ItemType.Plastic, 10);
		var target = s.Spawn(UnitType.BrickGolem, 23, 20, owner: 1); // tough target that stays in range a while
		int shots = tower.Shots;
		s.World.Ticks(tower.Stats.ReloadTicks * 3 + 1);
		Assert.InRange(shots - tower.Shots, 3, 4);
	}

	[Fact]
	public void Catapult_HitsEveryEnemyInTheArea()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var catapult = s.Place<Tower>(BuildingType.Catapult, 20, 20);
		s.Feed(catapult, ItemType.Brick, 1);
		var a = s.Spawn(UnitType.PlasticSoldier, 24, 20, owner: 1);
		var b = s.Spawn(UnitType.PlasticSoldier, 24, 20, owner: 1);
		s.World.Ticks(catapult.Stats.TravelTicks + 2);
		int hit = UnitStats.MaxHealth(UnitType.PlasticSoldier);
		Assert.True(a.Health < hit && b.Health < hit, $"hp {a.Health}, {b.Health}");
	}

	[Theory]
	[InlineData(DamageKind.Area, ArmorClass.Plastic, 200)]
	[InlineData(DamageKind.Water, ArmorClass.Electronic, 300)]
	[InlineData(DamageKind.Laser, ArmorClass.Brick, 200)]
	[InlineData(DamageKind.Foam, ArmorClass.Brick, 50)]
	[InlineData(DamageKind.Water, ArmorClass.Brick, 50)]
	[InlineData(DamageKind.Bullet, ArmorClass.Brick, 50)]
	[InlineData(DamageKind.Foam, ArmorClass.Plastic, 100)]
	public void Counters(DamageKind kind, ArmorClass armor, int percent) =>
		Assert.Equal(percent, Combat.DamagePercent(kind, armor));
}
