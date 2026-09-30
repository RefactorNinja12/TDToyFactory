using FactoryTD.Sim;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class SmokeTests
{
	[Fact]
	public void CreateMatch_HasTwoCores()
	{
		var world = World.CreateMatch();
		Assert.NotNull(world.GetCore(0));
		Assert.NotNull(world.GetCore(1));
	}
}
