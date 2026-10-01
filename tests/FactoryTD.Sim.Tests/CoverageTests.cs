using System;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

/// <summary>Table-driven checks over every building, item, unit and message: cheap, and they reach the
/// small branches the scenario tests don't (hover rows per type, texts, outputs, checksums).</summary>
public class EveryTypeTests
{
	/// <summary>The first spot in the left room where this building may go (deposits for extractors).</summary>
	private static Building PlaceAnywhere(Scenario s, BuildingType type)
	{
		for (int y = 2; y < 60; y++)
			for (int x = 2; x < 60; x++)
				if (s.World.CheckPlace(type, x, y, 0) == PlaceError.None)
					return s.Place(type, x, y);
		throw new InvalidOperationException($"no room for {type}");
	}

	public static TheoryData<BuildingType> Buildable()
	{
		var data = new TheoryData<BuildingType>();
		foreach (var type in BuildingRules.Buildable)
			data.Add(type);
		return data;
	}

	[Theory]
	[MemberData(nameof(Buildable))]
	public void HoverRows_ForEveryBuilding_SiteAndFinished(BuildingType type)
	{
		var s = Scenario.Match().NoWorkers().Rich();
		var building = PlaceAnywhere(s, type);
		var site = InfoRows.For(s.World, building, 0);
		Assert.Equal(Texts.DisplayName(type), site[0].Text);
		Assert.Contains(site, r => r.Text != null && r.Text.StartsWith("Byggs:"));

		building.CompleteConstruction();
		s.World.Ticks(2);
		var rows = InfoRows.For(s.World, building, 0);
		Assert.Equal(Texts.DisplayName(type), rows[0].Text);
		Assert.True(rows.Count >= 2, $"{type}: only a title");
		Assert.DoesNotContain(rows, r => r.Text != null && r.Text.StartsWith("Byggs:"));
		Assert.Contains(InfoRows.For(s.World, building, 1), r => r.Text != null && r.Text.EndsWith("(fiende)"));
	}

	[Theory]
	[MemberData(nameof(Buildable))]
	public void Checksum_SeesEveryBuilding(BuildingType type)
	{
		var a = Scenario.Match().NoWorkers().Rich();
		var b = Scenario.Match().NoWorkers().Rich();
		PlaceAnywhere(b, type).CompleteConstruction();
		foreach (var stack in BuildingRules.Cost(type))
			b.Give(stack.Type, stack.Amount); // same stock as a: only the building differs
		Assert.NotEqual(a.World.Checksum(), b.World.Checksum());
	}

	[Fact]
	public void Texts_ForEverything()
	{
		foreach (var type in BuildingRules.Buildable)
			Assert.False(string.IsNullOrWhiteSpace(Texts.Description(type)), $"{type} has no description");
		foreach (PlaceError error in Enum.GetValues<PlaceError>())
			if (error != PlaceError.None)
				Assert.False(string.IsNullOrWhiteSpace(Texts.ErrorText(BuildingType.BrickExtractor, error)), $"{error} has no message");
		foreach (ResourceType resource in Enum.GetValues<ResourceType>())
			if (resource != ResourceType.None)
				Assert.False(string.IsNullOrWhiteSpace(Texts.ResourceName(resource)));
		foreach (UnitType unit in Enum.GetValues<UnitType>())
			Assert.NotEqual(unit.ToString(), Texts.UnitName(unit));
		foreach (var item in Items.All)
		{
			Assert.StartsWith("1 ", Texts.ItemCount(item, 1));
			Assert.StartsWith("3 ", Texts.ItemCount(item, 3));
		}
	}

	[Fact]
	public void SplitterSorterJunction_OutputEverySide_TreadmillAndPowerNone()
	{
		var s = Scenario.Match().NoWorkers().Rich().Instant();
		var any = new[] { BuildingType.Splitter, BuildingType.Sorter, BuildingType.Junction }.Select(t => PlaceAnywhere(s, t)).ToList();
		var none = new[] { BuildingType.Treadmill, BuildingType.Pylon, BuildingType.Lamp }.Select(t => PlaceAnywhere(s, t)).ToList();
		foreach (Direction d in Enum.GetValues<Direction>())
		{
			Assert.All(any, b => Assert.True(b.OutputsToward(d)));
			Assert.All(none, b => Assert.False(b.OutputsToward(d)));
		}
	}

	[Fact]
	public void Sorter_CyclesThroughEveryItem_AndTheChecksumSeesIt()
	{
		var s = Scenario.Match().NoWorkers().Rich().Instant();
		var sorter = (Sorter)PlaceAnywhere(s, BuildingType.Sorter);
		var seen = new System.Collections.Generic.HashSet<ItemType>();
		ulong before = s.World.Checksum();
		for (int i = 0; i < Items.All.Length; i++)
		{
			Assert.True(sorter.CycleSetting());
			seen.Add(sorter.Filter);
		}
		Assert.Equal(Items.All.Length, seen.Count);
		sorter.CycleSetting();
		Assert.NotEqual(before, s.World.Checksum());
	}
}
