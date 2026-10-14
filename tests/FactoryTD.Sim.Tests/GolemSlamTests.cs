using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class GolemSlamTests
{
	// Player 1's farmers stand still in player 0's hall (no farm to go to): dummies to slam. Plastic, 20 health.
	private static Scenario Hall() => Scenario.Match().NoWorkers().Instant().Rich();

	private const int Windup = GolemStats.SlamWindupTicks;
	private static readonly int PlasticHit = GolemStats.SlamDamage * 2; // area damage: double against plastic

	private static int FullHealth => UnitStats.MaxHealth(UnitType.Farmer);

	[Fact]
	public void EnemyClose_RaisesTheHands_ThenSlams()
	{
		var s = Hall();
		var golem = s.Spawn(UnitType.BrickGolem, 72, 30);
		var farmer = s.Spawn(UnitType.Farmer, 73, 30, owner: 1);
		s.World.Ticks(1);
		Assert.Equal(Windup, golem.SlamTicks);
		int x = golem.X;
		s.World.Ticks(Windup - 1);
		Assert.Equal(FullHealth, farmer.Health); // nothing before the impact
		Assert.Equal(x, golem.X); // stands still with its hands up
		s.World.Ticks(1);
		Assert.Equal(0, golem.SlamTicks);
		Assert.Equal(FullHealth - PlasticHit, farmer.Health);
		Assert.Equal(GolemStats.SlamCooldownTicks, golem.SlamCooldown);
		Assert.Single(s.World.RecentImpacts);
	}

	[Fact]
	public void OwnUnits_AreLeftAlone_AndDontStartASlam()
	{
		var s = Hall();
		var golem = s.Spawn(UnitType.BrickGolem, 72, 30);
		var own = s.Spawn(UnitType.Farmer, 73, 30);
		s.World.Ticks(1);
		Assert.Equal(0, golem.SlamTicks);
		var enemy = s.Spawn(UnitType.Farmer, golem.TileX, 31, owner: 1);
		s.World.Ticks(Windup + 1);
		Assert.True(enemy.Health < FullHealth);
		Assert.Equal(FullHealth, own.Health);
		Assert.Equal(0, own.AirTicks);
	}

	[Fact]
	public void HitsEveryoneWithinTheRadius_NotOutside()
	{
		var s = Hall();
		var golem = s.Spawn(UnitType.BrickGolem, 75, 30);
		var near = s.Spawn(UnitType.Farmer, 76, 30, owner: 1);
		var edge = s.Spawn(UnitType.Farmer, 75, 29, owner: 1);
		var far = s.Spawn(UnitType.Farmer, 78, 30, owner: 1); // 3 tiles: outside the radius
		s.World.Ticks(Windup + 1);
		Assert.Equal(FullHealth - PlasticHit, near.Health);
		Assert.Equal(FullHealth - PlasticHit, edge.Health);
		Assert.Equal(FullHealth, far.Health);
		Assert.True(golem.SlamCooldown > 0);
	}

	[Fact]
	public void ThrowsEveryoneOutwards_InARing()
	{
		var s = Hall();
		var golem = s.Spawn(UnitType.BrickGolem, 75, 30);
		var east = s.Spawn(UnitType.Farmer, 76, 30, owner: 1);
		var west = s.Spawn(UnitType.Farmer, 74, 30, owner: 1);
		var north = s.Spawn(UnitType.Farmer, 75, 29, owner: 1);
		var south = s.Spawn(UnitType.Farmer, 75, 31, owner: 1);
		var start = new[] { east, west, north, south }.Select(u => (u.X, u.Y)).ToArray();
		s.World.Ticks(Windup + 1);
		Assert.All(new[] { east, west, north, south }, u => Assert.Equal(GolemStats.ThrowTicks, u.AirTicks));
		s.World.Ticks(GolemStats.ThrowTicks + 1);
		Assert.True(east.X > start[0].X + UnitStats.SubTile);
		Assert.True(west.X < start[1].X - UnitStats.SubTile);
		Assert.True(north.Y < start[2].Y - UnitStats.SubTile);
		Assert.True(south.Y > start[3].Y + UnitStats.SubTile);
		Assert.All(new[] { east, west, north, south }, u => Assert.Equal(0, u.AirTicks)); // landed
	}

	[Fact]
	public void AWall_CutsTheThrowShort_AndItLandsAtOnce()
	{
		var s = Hall();
		var golem = s.Spawn(UnitType.BrickGolem, 75, 34);
		var farmer = s.Spawn(UnitType.Farmer, 75, 35, owner: 1); // the hall's south wall is right behind it (y 37)
		s.World.Ticks(Windup + 1);
		Assert.True(farmer.AirTicks > 0);
		s.World.Ticks(GolemStats.ThrowTicks);
		Assert.Equal(TileType.Floor, s.World.Map[farmer.TileX, farmer.TileY]);
		Assert.Equal(36, farmer.TileY);
		Assert.Equal(0, farmer.AirTicks);
		Assert.Equal(0, farmer.PushTicks);
	}

	[Fact]
	public void NotThroughAWall()
	{
		var s = Hall();
		s.Place(BuildingType.BrickWall, 76, 30);
		var golem = s.Spawn(UnitType.BrickGolem, 75, 30);
		var behind = s.Spawn(UnitType.Farmer, 77, 30, owner: 1);
		s.World.Ticks(Windup + 1);
		Assert.Equal(0, golem.SlamTicks + golem.SlamCooldown);
		Assert.Equal(FullHealth, behind.Health);
	}

	[Fact]
	public void Golems_FlyHalfAsFar()
	{
		var s = Hall();
		s.Spawn(UnitType.BrickGolem, 75, 30);
		var enemy = s.Spawn(UnitType.BrickGolem, 76, 30, owner: 1);
		s.World.Ticks(Windup + 1);
		Assert.Equal(GolemStats.ThrowTicks / 2, enemy.AirTicks);
	}

	[Fact]
	public void InTheAir_ItFliesOverAnOpenTrapdoor_AndFallsInWhereItLands()
	{
		var s = Hall();
		var door = s.Place<Trapdoor>(BuildingType.Trapdoor, 75, 29);
		s.World.Until(() => door.IsOpen, 20, "the trapdoor opens");
		var thrown = s.Spawn(UnitType.Farmer, 75, 29, owner: 1);
		thrown.PushTicks = thrown.AirTicks = 3; // in the air right above it, not moving
		s.World.Ticks(2);
		Assert.Contains(thrown, s.World.Units);
		s.World.Ticks(2); // landed, the door still open
		Assert.True(door.IsOpen);
		Assert.DoesNotContain(thrown, s.World.Units);
	}

	[Fact]
	public void APendulumShove_SlidesAlongTheFloor()
	{
		var s = Hall();
		s.Place(BuildingType.Pendulum, 72, 30);
		var farmer = s.Spawn(UnitType.Farmer, 73, 30, owner: 1);
		s.World.Until(() => farmer.PushTicks > 0, 10, "the pendulum shoves the farmer");
		Assert.Equal(0, farmer.AirTicks);
	}

	[Fact]
	public void SlamState_IsInTheChecksum()
	{
		ulong Run(int cooldown)
		{
			var s = Hall();
			var golem = s.Spawn(UnitType.BrickGolem, 20, 20);
			golem.SlamCooldown = cooldown;
			return s.World.Checksum();
		}
		Assert.NotEqual(Run(0), Run(5));
	}

	[Fact]
	public void Golem_IsTheBiggestUnit()
	{
		int golem = World.UnitRadius(UnitType.BrickGolem);
		Assert.All(System.Enum.GetValues<UnitType>().Where(t => t != UnitType.BrickGolem), t => Assert.True(World.UnitRadius(t) < golem));
		Assert.True(2 * golem <= UnitStats.SubTile); // fits a 1-tile gap
	}

	[Fact]
	public void Golem_WalksThroughAOneTileGap()
	{
		var s = Hall();
		var walls = Enumerable.Range(25, 12).Where(y => y != 30).Select(y => s.Place(BuildingType.BrickWall, 70, y)).ToList();
		var golem = s.Spawn(UnitType.BrickGolem, 66, 26);
		s.Spawn(UnitType.BrickGolem, 66, 34); // two of them, pushing each other in the gap
		s.World.Until(() => golem.TileX > 72, 40, "the golem gets through the gap");
		Assert.All(walls, w => Assert.Equal(w.MaxHealth, w.Health));
	}

	[Theory]
	[InlineData(5, true)]
	[InlineData(8, false)]
	public void Balance_Golem_SlamsAPackOfCheeseHunters_UpToAPoint(int hunters, bool golemWins)
	{
		var s = Hall();
		var golem = s.Spawn(UnitType.BrickGolem, 66, 30);
		var pack = Enumerable.Range(0, hunters).Select(i => s.Spawn(UnitType.CheeseHunter, 80, 27 + i % 6, owner: 1)).ToList();
		s.World.Until(() => golem.Health <= 0 || pack.All(h => h.Health <= 0), 60, "one side wins");
		Assert.Equal(golemWins, golem.Health > 0);
	}
}
