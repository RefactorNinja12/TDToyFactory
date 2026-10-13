using System.Linq;
using FactoryTD.Sim;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class FloorDecorTests
{
	[Theory]
	[InlineData(MapTheme.Garden, DecorKind.Dirt, FloorDecor.DirtPatches)]
	[InlineData(MapTheme.Nursery, DecorKind.PlayMat, 3)]
	public void EachRoom_GetsItsDecor_OnPlainFloor_Mirrored(MapTheme theme, DecorKind kind, int perRoom)
	{
		var map = MapLayout.CreateDefault(theme: theme);
		var decor = FloorDecor.For(map);
		Assert.Equal(perRoom * 2, decor.Count);
		Assert.Contains(decor, d => d.Kind == kind);
		Assert.Equal(decor, FloorDecor.For(MapLayout.CreateDefault(theme: theme)));    // always the same
		foreach (var d in decor)
		{
			// on plain floor in a room: no wall, deposit, toy or hall under it
			for (int y = d.Y; y < d.Y + d.Height; y++)
				for (int x = d.X; x < d.X + d.Width; x++)
				{
					Assert.Equal(TileType.Floor, map[x, y]);
					Assert.Equal(ResourceType.None, map.GetResource(x, y));
					Assert.NotEqual(Zone.Hall, map.GetZone(x, y));
				}
			Assert.Contains(decor, m => m with { X = map.Width - m.X - m.Width } == d);    // its twin in the other room
		}
		var left = decor.Where(d => d.X < map.Width / 2).ToList();
		Assert.Equal(perRoom, left.Count);
		Assert.All(left, a => Assert.DoesNotContain(left, b => b != a && a.X < b.X + b.Width && b.X < a.X + a.Width
			&& a.Y < b.Y + b.Height && b.Y < a.Y + a.Height));                          // never on top of each other
	}

	[Fact]
	public void TheGarden_HasNoRugs_TheNurseryNoDirt()
	{
		Assert.All(FloorDecor.For(MapLayout.CreateDefault(theme: MapTheme.Garden)), d => Assert.Equal(DecorKind.Dirt, d.Kind));
		Assert.DoesNotContain(FloorDecor.For(MapLayout.CreateDefault(theme: MapTheme.Nursery)), d => d.Kind == DecorKind.Dirt);
	}
}
