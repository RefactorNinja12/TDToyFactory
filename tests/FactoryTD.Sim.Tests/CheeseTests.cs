using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class CheeseMelterTests
{
	[Fact]
	public void Melter_OnlyOnCheese_MakesMeltedCheese()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		Assert.Equal(PlaceError.WrongResource, s.World.CheckPlace(BuildingType.CheeseMelter, 11, 23, 0)); // bricks
		Assert.Equal(PlaceError.WrongResource, s.World.CheckPlace(BuildingType.PlasticExtractor, 51, 11, 0)); // cheese
		var melter = s.Place<Extractor>(BuildingType.CheeseMelter, 51, 11);
		Assert.Equal(ItemType.MeltedCheese, melter.Output);
		s.World.Ticks(Extractor.ProductionTicks * 2);
		Assert.Equal(2, melter.Stored);
	}

	[Fact]
	public void CheeseIsFoundUnderTheFog_LikeTheOtherDeposits()
	{
		var s = Scenario.Match().NoWorkers().Rich().RealFog();
		Assert.Equal(PlaceError.Unexplored, s.World.CheckPlace(BuildingType.CheeseMelter, 51, 11, 0));
		s.Spawn(UnitType.PlasticSoldier, 51, 13);
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.Equal(PlaceError.None, s.World.CheckPlace(BuildingType.CheeseMelter, 51, 11, 0));
	}

	[Fact]
	public void BigToys_StayClearOfTheCheese()
	{
		var map = MapLayout.CreateDefault();
		foreach (var o in map.Obstacles)
			for (int y = o.Y - 2; y < o.Y + o.Height + 2; y++)
				for (int x = o.X - 2; x < o.X + o.Width + 2; x++)
					Assert.NotEqual(ResourceType.Cheese, map.GetResource(x, y));
	}
}
