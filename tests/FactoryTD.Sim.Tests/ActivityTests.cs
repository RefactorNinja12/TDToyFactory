using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class ActivityTests
{
	/// <summary>Runs the world, observing every tick like the view does every frame.</summary>
	private static Activity Watch(Scenario s, int ticks, Activity activity = null)
	{
		activity ??= new Activity();
		for (int t = 0; t < ticks; t++)
		{
			s.World.Tick();
			activity.Observe(s.World);
		}
		return activity;
	}

	[Fact]
	public void Assembler_WorksWhileItHasInput_ThenStops()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var assembler = s.Place<Assembler>(BuildingType.Assembler, 20, 20);
		var activity = Watch(s, 20);
		Assert.False(activity.IsWorking(assembler)); // nothing to make
		s.Feed(assembler, ItemType.Brick, 2);
		s.Feed(assembler, ItemType.Plastic, 1);
		Watch(s, 5, activity);
		Assert.True(activity.IsWorking(assembler));
		Watch(s, assembler.Recipe.Ticks + Activity.WorkingTicks + 5, activity);
		Assert.False(activity.IsWorking(assembler)); // made its gear, nothing more to do
	}

	[Fact]
	public void Extractor_WorksUntilItsStoreIsFull()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var extractor = s.Place<Extractor>(BuildingType.BrickExtractor, 11, 23);
		var activity = Watch(s, 5);
		Assert.True(activity.IsWorking(extractor));
		Watch(s, World.TicksPerSecond * 60, activity); // no belt: the store fills up
		Assert.Equal(Extractor.MaxStored, extractor.Stored);
		Assert.False(activity.IsWorking(extractor));
	}

	[Fact]
	public void Factory_WithoutPower_StandsStill()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich().RealPower();
		var factory = s.Place<UnitFactory>(BuildingType.SoldierFactory, 30, 20);
		foreach (var input in factory.Recipe)
			s.Feed(factory, input.Type, input.Amount);
		var activity = Watch(s, 20);
		Assert.True(factory.NoPower);
		Assert.False(activity.IsWorking(factory));
	}

	[Fact]
	public void Kitchen_WorksWhileCooking_AndConstructionSitesNever()
	{
		var (s, kitchen) = CraftingTests.KitchenIntoToybox();
		s.Feed(kitchen, ItemType.Crop, 2);
		var activity = Watch(s, 5);
		Assert.True(activity.IsWorking(kitchen));

		var site = Scenario.Match().NoWorkers(); // not instant: stays a construction site
		var assembler = site.Place<Assembler>(BuildingType.Assembler, 20, 20);
		Assert.False(Watch(site, 20).IsWorking(assembler));
	}

	[Fact]
	public void OnlyBuildingsWithProgress_HaveASignature()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		Assert.Null(Activity.Signature(s.Place(BuildingType.Conveyor, 20, 20)));
		Assert.Null(Activity.Signature(s.World.GetCore(0)));
		Assert.NotNull(Activity.Signature(s.Place(BuildingType.Toolbox, 24, 20)));
	}
}
