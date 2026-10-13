using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class TrapTests
{
	// Farmers of player 1 stand still in player 0's hall (no farm to go to): good dummies for traps.
	private const int OpensAfter = TrapStats.TrapdoorPeriod - TrapStats.TrapdoorOpenTicks;

	private static Scenario Hall() => Scenario.Match().NoWorkers().Instant().Rich();

	private static bool Alive(Scenario s, Unit unit) => s.World.Units.Contains(unit);

	[Fact]
	public void Trapdoor_TakesOnlyEnemiesOnIt_WhileOpen()
	{
		var s = Hall();
		var door = s.Place<Trapdoor>(BuildingType.Trapdoor, 75, 29);
		var on = s.Spawn(UnitType.Farmer, 75, 29, owner: 1);
		var beside = s.Spawn(UnitType.Farmer, 77, 29, owner: 1);
		var own = s.Spawn(UnitType.Farmer, 76, 30);

		s.World.Ticks(OpensAfter - TrapStats.TrapdoorWarnTicks);
		Assert.True(door.IsWarning);
		s.World.Ticks(TrapStats.TrapdoorWarnTicks - 1);
		Assert.False(door.IsOpen);
		Assert.True(Alive(s, on));

		s.World.Ticks(1);
		Assert.True(door.IsOpen);
		Assert.False(Alive(s, on));
		Assert.True(Alive(s, beside));
		Assert.True(Alive(s, own));
		s.World.Ticks(TrapStats.TrapdoorOpenTicks);
		Assert.False(door.IsOpen);
	}

	[Fact]
	public void Trapdoor_WearsOut_AfterTwelveCatches()
	{
		var s = Hall();
		var door = s.Place<Trapdoor>(BuildingType.Trapdoor, 75, 29);
		// Four mice, one per tile, at each of three warnings.
		for (int round = 0; round < WallStats.BrickHealth / TrapStats.TrapdoorWear / 4; round++)
		{
			Assert.NotNull(s.World.GetBuilding(75, 29));
			s.World.Until(() => door.IsWarning, 10, "the warning");
			for (int i = 0; i < 4; i++)
				s.Spawn(UnitType.Farmer, 75 + i % 2, 29 + i / 2, owner: 1);
			s.World.Until(() => door.IsOpen || s.World.GetBuilding(75, 29) == null, 10, "the trapdoor opens");
			s.World.Ticks(TrapStats.TrapdoorOpenTicks);
		}
		Assert.Null(s.World.GetBuilding(75, 29));
	}

	[Fact]
	public void Pendulum_PushesAlongTheSwing_BothWays()
	{
		var s = Hall();
		var pendulum = s.Place<Pendulum>(BuildingType.Pendulum, 74, 30);
		var mouse = s.Spawn(UnitType.Farmer, 75, 30, owner: 1);
		s.World.Ticks(TrapStats.PendulumSwingTicks);
		Assert.Equal(75 + 3, mouse.TileX);
		Assert.Equal(UnitStats.MaxHealth(UnitType.Farmer) - TrapStats.PendulumDamage, mouse.Health);

		s.World.Until(() => pendulum.SwingDirection == -1, 5, "the swing back");
		var back = s.Spawn(UnitType.Farmer, 75, 30, owner: 1);
		s.World.Ticks(TrapStats.PendulumSwingTicks);
		Assert.Equal(75 - 3, back.TileX);
	}

	[Fact]
	public void Pendulum_TurnedNorth_IsOneByFour_AndPushesAlongY()
	{
		Assert.Equal((4, 1), BuildingRules.Size(BuildingType.Pendulum, Direction.East));
		Assert.Equal((1, 4), BuildingRules.Size(BuildingType.Pendulum, Direction.North));
		var s = Hall();
		s.Place(BuildingType.Pendulum, 75, 27, Direction.North);
		Assert.Equal(BuildingType.Pendulum, s.World.GetBuilding(75, 30)?.Type);
		var mouse = s.Spawn(UnitType.Farmer, 75, 28, owner: 1);
		s.World.Ticks(TrapStats.PendulumSwingTicks);
		Assert.Equal((75, 31), (mouse.TileX, mouse.TileY));
	}

	[Fact]
	public void Push_StopsAtAWall_AndGolemsGoHalfAsFar()
	{
		var s = Hall();
		s.Place(BuildingType.Pendulum, 72, 30);
		s.Place(BuildingType.BrickWall, 76, 30);
		var mouse = s.Spawn(UnitType.Farmer, 73, 30, owner: 1);
		s.World.Ticks(TrapStats.PendulumSwingTicks);
		Assert.Equal(75, mouse.TileX);

		Assert.Equal(TrapStats.PushTicks / 2, TrapStats.PushTicksFor(UnitType.BrickGolem));
	}

	[Fact]
	public void Pendulum_ShovesEnemiesIntoATrapdoor()
	{
		var s = Hall();
		s.Place(BuildingType.Pendulum, 72, 30);
		s.Place(BuildingType.Trapdoor, 76, 29);
		var mouse = s.Spawn(UnitType.Farmer, 73, 30, owner: 1);
		s.World.Until(() => !Alive(s, mouse), 10, "the mouse falls through the trapdoor");
	}

	[Fact]
	public void Enemies_DontNoticeTraps()
	{
		var s = Hall();
		s.Place(BuildingType.Trapdoor, 75, 29);
		var soldier = s.Spawn(UnitType.PlasticSoldier, 78, 30, owner: 1);
		var (unit, building) = s.World.SelectTarget(soldier, UnitStats.Sight(soldier.Type), includeCore: false);
		Assert.Null(unit);
		Assert.Null(building);
		var field = s.World.GetFlowField(1);
		Assert.Equal(field.Distance(77, 29) + 10, field.Distance(78, 29)); // over the trap like floor
	}

	[Fact]
	public void Traps_AreDeterministic()
	{
		ulong Run()
		{
			var s = Hall();
			s.Place(BuildingType.Pendulum, 72, 28, Direction.North);
			s.Place(BuildingType.Trapdoor, 70, 30);
			foreach (int y in Enumerable.Range(26, 8))
				s.Spawn(UnitType.PlasticSoldier, 90, y, owner: 1);
			s.World.Seconds(30);
			return s.World.Checksum();
		}
		Assert.Equal(Run(), Run());
	}
}
