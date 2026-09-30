using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class PowerBuildingTests
{
	[Theory]
	[InlineData(BuildingType.Pylon)]
	[InlineData(BuildingType.BatteryCharger)]
	public void Buildable_OneTile_PaysItsCost(BuildingType type)
	{
		var s = Scenario.Match();
		Assert.Contains(type, BuildingRules.Buildable);
		Assert.Equal((1, 1), BuildingRules.Size(type));
		int bricks = s.P0.GetCount(ItemType.Brick), plastic = s.P0.GetCount(ItemType.Plastic);
		s.Place(type, 20, 20);
		Assert.True(s.P0.GetCount(ItemType.Brick) < bricks || s.P0.GetCount(ItemType.Plastic) < plastic);
		Assert.IsType(type == BuildingType.Pylon ? typeof(Pylon) : typeof(BatteryCharger), s.World.GetBuilding(20, 20));
	}

	[Theory]
	[InlineData(BuildingType.Pylon)]
	[InlineData(BuildingType.BatteryCharger)]
	public void OwnRoomAndHall_NotTheEnemyRoom(BuildingType type)
	{
		var w = Scenario.Match().Rich().World;
		Assert.Equal(PlaceError.None, w.CheckPlace(type, 80, 30, 0));
		Assert.Equal(PlaceError.OutsideZone, w.CheckPlace(type, 120, 30, 0));
	}

	[Fact]
	public void PowerBuildings_NeverPushItemsOntoBelts()
	{
		var s = Scenario.Match().Instant();
		var pylon = s.Pylon(20, 20);
		var charger = s.Charger(22, 20);
		foreach (var d in new[] { Direction.East, Direction.South, Direction.West, Direction.North })
		{
			Assert.False(pylon.OutputsToward(d));
			Assert.False(charger.OutputsToward(d));
		}
	}

	[Fact]
	public void Charger_StartsEmpty_TestHelperCanFillIt()
	{
		var s = Scenario.Match().Instant();
		Assert.Equal(0, s.Charger(20, 20).Energy);
		Assert.Equal(500, s.Charger(22, 20, energy: 500).Energy);
	}
}

public class PowerGridTests
{
	private static PowerNetwork At(Scenario s, int x, int y, int player = 0) => s.World.Power.NetworkAt(player, x, y);

	[Fact]
	public void Toybox_IsAGridRoot_WithItsOwnRadius()
	{
		var s = Scenario.Match();
		var core = s.World.GetCore(0);
		var network = At(s, 10, 30); // 3.5 tiles right of the core's centre
		Assert.NotNull(network);
		Assert.Contains(core, network.Nodes);
		Assert.Null(At(s, 11, 30)); // 4.5 tiles: outside CoreRadius 4
	}

	[Fact]
	public void Pylon_CoversItsRadius_EuclideanEdge()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		Assert.NotNull(At(s, 25, 20)); // 5 tiles
		Assert.Null(At(s, 26, 20));
		Assert.NotNull(At(s, 24, 23)); // 4² + 3² = 5²
		Assert.Null(At(s, 24, 24));
	}

	[Fact]
	public void UnfinishedPylon_DoesNothing()
	{
		var s = Scenario.Match().NoWorkers();
		s.Pylon(20, 20);
		Assert.Null(At(s, 20, 20));
	}

	[Fact]
	public void Pylons_LinkWithinLinkRange()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		s.Pylon(20 + PowerStats.LinkRange, 20);
		s.Pylon(20, 40);
		s.Pylon(20 + PowerStats.LinkRange + 1, 40);
		Assert.Same(At(s, 20, 20), At(s, 20 + PowerStats.LinkRange, 20));
		Assert.NotSame(At(s, 20, 40), At(s, 20 + PowerStats.LinkRange + 1, 40));
	}

	[Fact]
	public void PylonNearTheToybox_JoinsItsNetwork()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(14, 30); // centre 7.5 tiles from the core's centre
		Assert.Same(At(s, 7, 30), At(s, 14, 30));
	}

	[Fact]
	public void RemovingTheMiddlePylon_SplitsTheNetwork()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		s.Pylon(27, 20);
		s.Pylon(34, 20);
		Assert.Same(At(s, 20, 20), At(s, 34, 20));
		Assert.True(s.World.TryRemove(27, 20, 0));
		Assert.NotNull(At(s, 20, 20));
		Assert.NotSame(At(s, 20, 20), At(s, 34, 20));
	}

	[Fact]
	public void EnemyPylons_NeverLink()
	{
		var s = Scenario.Match().Instant().Rich();
		s.Pylon(80, 30, owner: 0);
		s.Pylon(82, 30, owner: 1);
		Assert.NotNull(At(s, 81, 30, player: 0));
		Assert.NotSame(At(s, 81, 30, player: 0), At(s, 81, 30, player: 1));
		Assert.Equal(0, At(s, 81, 30, player: 0).Owner);
	}

	[Fact]
	public void Charger_LinksButCoversNothing()
	{
		var s = Scenario.Match().Instant();
		var charger = s.Charger(40, 40);
		Assert.Null(At(s, 41, 40));
		var pylon = s.Pylon(44, 40);
		var network = At(s, 44, 40);
		Assert.Contains(charger, network.Chargers);
		Assert.Contains(pylon, network.Nodes);
	}

	[Fact]
	public void Cords_FormATree()
	{
		var s = Scenario.Match().Instant();
		s.Pylon(20, 20);
		s.Pylon(24, 20);
		s.Pylon(22, 23); // all three within link range of each other
		Assert.Equal(2, s.World.Power.Cords.Count(c => c.A.Owner == 0 && c.A.Y >= 20 && c.A.Y <= 23 && c.A is Pylon));
	}
}

public class PowerSupplyTests
{
	[Fact]
	public void Charger_TakesOnlyBatteries_UpToItsBuffer()
	{
		var s = Scenario.Match().Instant().RealPower();
		var charger = s.Charger(20, 20, energy: PowerStats.ChargerCapacity); // full: keeps the batteries waiting
		Assert.Equal(0, s.Feed(charger, ItemType.Plastic));
		Assert.Equal(PowerStats.ChargerBatteryBuffer, s.Feed(charger, ItemType.Battery, 10));
	}

	[Fact]
	public void OneBattery_GivesEnergyPerBattery_NeverOverCapacity()
	{
		var s = Scenario.Match().Instant().RealPower();
		var charger = s.Charger(20, 20);
		s.Feed(charger, ItemType.Battery, 2);
		s.World.Tick();
		Assert.Equal(PowerStats.EnergyPerBattery, charger.Energy);
		s.World.Tick();
		Assert.Equal(2 * PowerStats.EnergyPerBattery, charger.Energy);

		charger.Energy = PowerStats.ChargerCapacity - PowerStats.EnergyPerBattery + 1;
		s.Feed(charger, ItemType.Battery, 1);
		s.World.Ticks(5);
		Assert.Equal(PowerStats.ChargerCapacity - PowerStats.EnergyPerBattery + 1, charger.Energy); // would overflow: waits
		Assert.Equal(1, charger.Batteries);
	}

	[Fact]
	public void Draw_NeedsACoveredTileAndEnoughEnergy()
	{
		var s = Scenario.Match().Instant().RealPower();
		Assert.False(s.World.TryDrawPower(0, 30, 30, 1)); // no grid there
		s.Pylon(30, 30);
		Assert.False(s.World.TryDrawPower(0, 30, 30, 1)); // grid, but nothing charged
		var charger = s.Charger(33, 30, energy: 100);
		Assert.True(s.World.TryDrawPower(0, 31, 31, 60));
		Assert.Equal(40, charger.Energy);
		Assert.False(s.World.TryDrawPower(0, 31, 31, 60)); // all or nothing
		Assert.Equal(40, charger.Energy);
		Assert.False(s.World.TryDrawPower(1, 30, 30, 1)); // not the enemy's grid
	}

	[Fact]
	public void Draw_UsesSeveralChargersOfTheNetwork()
	{
		var s = Scenario.Match().Instant().RealPower();
		s.Pylon(30, 30);
		var a = s.Charger(32, 30, energy: 50);
		var b = s.Charger(34, 30, energy: 50);
		Assert.True(s.World.TryDrawPower(0, 30, 30, 80));
		Assert.Equal(20, a.Energy + b.Energy);
	}

	[Fact]
	public void Networks_DontShareEnergy()
	{
		var s = Scenario.Match().Instant().RealPower();
		s.Pylon(30, 20);
		s.Charger(31, 20, energy: 500);
		s.Pylon(30, 45); // far away: its own network
		Assert.True(s.World.TryDrawPower(0, 30, 20, 1));
		Assert.False(s.World.TryDrawPower(0, 30, 45, 1));
	}

	[Fact]
	public void FreePower_AlwaysDraws()
	{
		var s = Scenario.Match();
		Assert.True(s.World.TryDrawPower(0, 30, 45, 1000));
	}
}

public class PowerConsumerTests
{
	private static void FeedRecipe(Scenario s, UnitFactory factory)
	{
		foreach (var input in factory.Recipe)
			s.Feed(factory, input.Type, input.Amount);
	}

	[Fact]
	public void Factory_OutsideTheGrid_NeverProduces()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 30, 20);
		FeedRecipe(s, factory);
		s.World.Ticks(UnitStats.BuildTicks(UnitType.PlasticSoldier) * 2);
		Assert.Empty(s.World.Units);
		Assert.True(factory.NoPower);
	}

	[Fact]
	public void Factory_OnAChargedGrid_ProducesAndUsesEnergy()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 30, 20);
		s.Pylon(33, 20);
		var charger = s.Charger(34, 20, energy: 10000);
		FeedRecipe(s, factory);
		int ticks = UnitStats.BuildTicks(UnitType.PlasticSoldier);
		s.World.Ticks(ticks + 1);
		Assert.Single(s.World.Units);
		Assert.False(factory.NoPower);
		Assert.Equal(10000 - ticks * PowerStats.FactoryEnergyPerTick, charger.Energy);
	}

	[Fact]
	public void IdleFactory_UsesNothing()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 30, 20);
		s.Pylon(33, 20);
		var charger = s.Charger(34, 20, energy: 1000);
		s.World.Seconds(5);
		Assert.Equal(1000, charger.Energy);
		Assert.False(factory.NoPower);
	}

	[Fact]
	public void WorkerFactories_DontNeedPower()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var toolbox = s.Place<UnitFactory>(BuildingType.Toolbox, 30, 20);
		FeedRecipe(s, toolbox);
		s.World.Ticks(UnitStats.BuildTicks(UnitType.Builder) + 1);
		Assert.Single(s.World.Units);
	}

	[Fact]
	public void Assembler_NeedsPower()
	{
		var s = Scenario.Match().NoWorkers().Instant().RealPower();
		var assembler = s.Place<Assembler>(BuildingType.Assembler, 30, 20);
		s.Feed(assembler, ItemType.Brick, 2);
		s.Feed(assembler, ItemType.Plastic, 1);
		s.World.Ticks(assembler.Recipe.Ticks + 2);
		Assert.Equal(0, assembler.Finished);
		Assert.True(assembler.NoPower);

		s.Pylon(31, 20);
		s.Charger(32, 20, energy: 1000);
		s.World.Ticks(assembler.Recipe.Ticks + 2);
		Assert.Equal(1, assembler.Finished);
	}

	[Fact]
	public void Tower_WithAmmoButNoPower_StaysSilent_ThenFiresWhenPowered()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var tower = s.Place<Tower>(BuildingType.FoamTower, 30, 20);
		s.Feed(tower, ItemType.Plastic, 2);
		s.Spawn(UnitType.BrickGolem, 33, 20, owner: 1);
		s.World.Ticks(10);
		Assert.Empty(s.World.Projectiles);
		Assert.True(tower.NoPower);

		s.Pylon(30, 22);
		var charger = s.Charger(31, 22, energy: 1000);
		s.World.Ticks(2);
		Assert.NotEmpty(s.World.Projectiles);
		Assert.False(tower.NoPower);
		Assert.Equal(1000 - PowerStats.TowerShotEnergy, charger.Energy);
	}

	[Fact]
	public void Shortage_PausesProgress_ResumesWhenRecharged()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 30, 20);
		s.Pylon(33, 20);
		int ticks = UnitStats.BuildTicks(UnitType.PlasticSoldier);
		var charger = s.Charger(34, 20, energy: ticks / 2 * PowerStats.FactoryEnergyPerTick);
		FeedRecipe(s, factory);
		s.World.Ticks(ticks * 2);
		Assert.Empty(s.World.Units);
		Assert.True(factory.NoPower);
		int progress = factory.Crafter.Progress;
		Assert.InRange(progress, ticks / 2 - 1, ticks / 2 + 1);

		s.Feed(charger, ItemType.Battery, 1);
		s.World.Ticks(ticks - progress + 2);
		Assert.Single(s.World.Units);
	}
}
