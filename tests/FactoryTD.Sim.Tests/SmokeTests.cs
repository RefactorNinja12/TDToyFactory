using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

/// <summary>The test helpers themselves, and that a match starts as expected.</summary>
public class SmokeTests
{
	[Fact]
	public void CreateMatch_HasTwoCoresStartingStockAndWorkers()
	{
		var s = Scenario.Match();
		Assert.NotNull(s.World.GetCore(0));
		Assert.NotNull(s.World.GetCore(1));
		Assert.Equal(UnitStats.StartingBuilders, s.World.CountUnits(0, UnitType.Builder));
		Assert.Equal(UnitStats.StartingFarmers, s.World.CountUnits(1, UnitType.Farmer));
		foreach (var stack in BuildingRules.StartingStock)
			Assert.Equal(stack.Amount, s.P0.GetCount(stack.Type));
	}

	[Fact]
	public void Instant_FinishesPlacedBuildings()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var factory = s.Place(BuildingType.SoldierFactory, 20, 20);
		Assert.True(factory.IsBuilt);
		Assert.Empty(s.World.Units);
	}

	[Fact]
	public void Place_FailsWithReason()
	{
		var s = Scenario.Match();
		var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => s.Place(BuildingType.Conveyor, 0, 0)); // a wall
		Assert.Contains("NotFloor", error.Message);
	}

	[Fact]
	public void Until_ReturnsTicksAndFailsWithReason()
	{
		var s = Scenario.Match();
		Assert.Equal(20, s.World.Until(() => s.World.TickCount >= 20, 5, "one second"));
		var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => s.World.Until(() => false, 0.5, "never"));
		Assert.Contains("never", error.Message);
	}

	[Fact]
	public void Scenario_FreePowerByDefault_RealPowerOptIn()
	{
		Assert.False(World.CreateMatch().FreePower);
		Assert.True(Scenario.Match().World.FreePower);
		Assert.False(Scenario.Match().RealPower().World.FreePower);
	}

	[Fact]
	public void Scenario_FullVisionByDefault_RealFogOptIn()
	{
		Assert.False(World.CreateMatch().FullVision);
		Assert.True(Scenario.Match().World.FullVision);
		Assert.False(Scenario.Match().RealFog().World.FullVision);
	}
}
