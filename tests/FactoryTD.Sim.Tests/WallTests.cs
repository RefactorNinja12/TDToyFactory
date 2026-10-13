using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class WallTests
{
	// The hall's interior is x 62..97, y 25..36. Player 1's units walk west through it to player 0's core.
	private const int HallTop = 25, HallBottom = 36, WallX = 70;

	/// <summary>A wall across the hall at <see cref="WallX"/>, leaving out the tiles in <paramref name="gaps"/>.</summary>
	private static List<Building> WallAcrossHall(Scenario s, int owner = 0, int gateAt = -1, params int[] gaps) =>
		LineAcrossHall(s, BuildingType.BrickWall, owner, gateAt, gaps);

	private static List<Building> LineAcrossHall(Scenario s, BuildingType type, int owner = 0, int gateAt = -1, params int[] gaps)
	{
		var walls = new List<Building>();
		for (int y = HallTop; y <= HallBottom; y++)
			if (!gaps.Contains(y))
				walls.Add(s.Place(y == gateAt ? BuildingType.Gate : type, WallX, y, owner: owner));
		return walls;
	}

	/// <summary>A powered fence line across the hall: a pylon behind it and a charged charger.</summary>
	private static (List<Building> Fences, BatteryCharger Charger) PoweredFence(Scenario s)
	{
		var fences = LineAcrossHall(s, BuildingType.ElectricFence);
		s.Pylon(WallX - 5, 30); // out of the attackers' range: shots fly over walls
		return (fences, s.Charger(WallX - 7, 30, energy: 12000));
	}

	private static bool AnyDamaged(List<Building> walls) => walls.Any(w => w.Health < w.MaxHealth);

	[Fact]
	public void Soldiers_WalkRoundThroughTheGap_AndNeverHitTheWall()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var walls = WallAcrossHall(s, gaps: HallBottom);
		var soldier = s.Spawn(UnitType.PlasticSoldier, 80, 26, owner: 1);
		s.World.Until(() => soldier.TileX < WallX - 3, 30, "the soldier gets past the wall");
		Assert.False(AnyDamaged(walls));
	}

	[Fact]
	public void Golem_SmashesThrough_WhenTheWayRoundIsLong()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var walls = WallAcrossHall(s, gaps: HallBottom);
		var golem = s.Spawn(UnitType.BrickGolem, WallX + 2, HallTop, owner: 1);
		s.World.Until(() => golem.TileX < WallX, 60, "the golem gets past the wall");
		Assert.True(walls.Count(w => w.Health == 0 || !s.World.Buildings.Contains(w)) >= 1);
	}

	[Fact]
	public void Golem_WalksRound_WhenTheGapIsClose()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var walls = WallAcrossHall(s, gaps: HallBottom);
		var golem = s.Spawn(UnitType.BrickGolem, WallX + 2, HallBottom - 1, owner: 1);
		s.World.Until(() => golem.TileX < WallX - 2, 30, "the golem gets past the wall");
		Assert.False(AnyDamaged(walls));
	}

	[Fact]
	public void Golem_BreaksABrickInFivePunches()
	{
		Assert.Equal(WallStats.BrickHealth, 5 * UnitStats.Def(UnitType.BrickGolem).Damage * UnitStats.Def(UnitType.BrickGolem).BuildingDamagePercent / 100);
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var walls = WallAcrossHall(s);
		var golem = s.Spawn(UnitType.BrickGolem, WallX + 2, 30, owner: 1);
		int ticks = 0;
		while (walls.All(s.World.Buildings.Contains) && ticks < 400)
		{
			s.World.Ticks(1);
			ticks++;
		}
		int attack = UnitStats.Def(UnitType.BrickGolem).AttackTicks;
		Assert.InRange(ticks, 4 * attack + 1, 5 * attack + 20);
	}

	[Fact]
	public void SealedBase_EvenSoldiersBreakThrough()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var walls = WallAcrossHall(s);
		var soldier = s.Spawn(UnitType.PlasticSoldier, 80, 30, owner: 1);
		s.World.Until(() => soldier.TileX < WallX, 90, "the soldier breaks through");
		Assert.True(walls.Count(w => !s.World.Buildings.Contains(w)) >= 1);
	}

	[Fact]
	public void OwnWalls_BlockOwnUnits_ButTheGateLetsThemThrough()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var walls = WallAcrossHall(s);
		var soldier = s.Spawn(UnitType.PlasticSoldier, WallX - 4, 30);
		s.World.Seconds(10);
		Assert.True(soldier.TileX < WallX);
		Assert.False(AnyDamaged(walls)); // never attacks its own wall

		s.World.TryRemove(WallX, HallBottom, 0);
		s.Place(BuildingType.Gate, WallX, HallBottom);
		s.World.Until(() => soldier.TileX > WallX + 2, 30, "the soldier goes through its own gate");
	}

	[Fact]
	public void EnemyGate_IsAWall()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		WallAcrossHall(s, gateAt: 30);
		Assert.True(s.World.BlocksUnit(1, WallX, 30));
		Assert.False(s.World.BlocksUnit(0, WallX, 30));
		var soldier = s.Spawn(UnitType.PlasticSoldier, WallX + 2, 30, owner: 1);
		s.World.Seconds(5);
		Assert.True(soldier.TileX > WallX);
	}

	[Fact]
	public void WallSite_DoesNotBlock_UntilBuilt()
	{
		var s = Scenario.Match().NoWorkers().Rich();
		s.World.TryPlace(BuildingType.BrickWall, WallX, 30, Direction.East, 0);
		Assert.False(s.World.BlocksUnit(1, WallX, 30));
	}

	[Fact]
	public void FinishedWall_PushesUnitsStandingOnItOff()
	{
		var s = Scenario.Match().Rich();
		var core = s.World.GetCore(0);
		int x = core.X + 4, y = core.Y + 4;
		var mouse = s.Spawn(UnitType.Farmer, x, y, owner: 1);
		s.World.TryPlace(BuildingType.BrickWall, x, y, Direction.East, 0);
		var wall = s.World.GetBuilding(x, y);
		s.World.Until(() => wall.IsBuilt, 60, "the builders finish the wall");
		Assert.False(mouse.TileX == x && mouse.TileY == y);
		Assert.False(s.World.BlocksUnit(mouse.Owner, mouse.TileX, mouse.TileY));
	}

	[Fact]
	public void DiagonalWall_HasNoGapAtTheCorners()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		// A diagonal staircase across the hall, corners touching only.
		for (int i = 0; i <= HallBottom - HallTop; i++)
			s.Place(BuildingType.BrickWall, WallX + i, HallTop + i);
		var field = s.World.GetFlowField(1);
		Assert.True(field.Distance(WallX + 6, HallTop) >= WallStats.SealedWallPenalty);
	}

	[Fact]
	public void Walls_AreDeterministic()
	{
		ulong Run()
		{
			var s = Scenario.Match().NoWorkers().Instant().Rich();
			WallAcrossHall(s, gaps: 31);
			for (int i = 0; i < 4; i++)
			{
				s.Spawn(UnitType.PlasticSoldier, 85, 26 + i * 3, owner: 1);
				s.Spawn(UnitType.BrickGolem, 88, 27 + i * 3, owner: 1);
			}
			s.World.Seconds(40);
			return s.World.Checksum();
		}
		Assert.Equal(Run(), Run());
	}

	[Fact]
	public void Fence_ZapsWhoeverHitsIt_FromTheGrid()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var (fences, charger) = PoweredFence(s);
		var soldier = s.Spawn(UnitType.PlasticSoldier, WallX + 2, 30, owner: 1);
		s.World.Until(() => soldier.Health <= 0, 30, "the fence zaps the soldier to death");
		int zaps = fences.Sum(f => ((Wall)f).Zaps);
		Assert.InRange(zaps, 3, 4); // 30 health, 10 a zap
		Assert.Equal(12000 - zaps * WallStats.ZapEnergy, charger.Energy);
	}

	[Fact]
	public void Fence_WithoutPower_IsJustAWall()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var fences = LineAcrossHall(s, BuildingType.ElectricFence);
		var soldier = s.Spawn(UnitType.PlasticSoldier, WallX + 2, 30, owner: 1);
		s.World.Seconds(10);
		Assert.Equal(UnitStats.MaxHealth(UnitType.PlasticSoldier), soldier.Health);
		Assert.True(AnyDamaged(fences));
		Assert.True(fences[0].NoPower);
	}

	[Fact]
	public void Fence_HurtsElectronicsDouble()
	{
		Assert.Equal(2 * WallStats.ZapDamageTo(UnitType.PlasticSoldier), WallStats.ZapDamageTo(UnitType.RcCar));
	}

	[Fact]
	public void Golem_PaysFiveZapsPerFence()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var (fences, _) = PoweredFence(s);
		var golem = s.Spawn(UnitType.BrickGolem, WallX + 2, 30, owner: 1);
		s.World.Until(() => !fences.All(s.World.Buildings.Contains), 30, "the golem breaks a fence");
		Assert.Equal(UnitStats.MaxHealth(UnitType.BrickGolem) - 5 * WallStats.ZapDamage, golem.Health);
	}

	[Fact]
	public void Factory_WalledInWithoutAGate_IsEnclosed()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var factory = s.Place(BuildingType.SoldierFactory, 20, 20);
		for (int i = 19; i <= 22; i++)
		{
			s.Place(BuildingType.BrickWall, i, 19);
			s.Place(BuildingType.BrickWall, i, 22);
			if (i is 20 or 21)
			{
				s.Place(BuildingType.BrickWall, 19, i);
				s.Place(BuildingType.BrickWall, 22, i);
			}
		}
		Assert.True(s.World.IsEnclosed(factory));
		Assert.Contains(InfoRows.For(s.World, factory, 0), r => r.Text?.StartsWith("Instängd") == true);

		s.World.TryRemove(22, 20, 0);
		s.Place(BuildingType.Gate, 22, 20);
		Assert.False(s.World.IsEnclosed(factory));
	}
}
