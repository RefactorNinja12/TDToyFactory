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
