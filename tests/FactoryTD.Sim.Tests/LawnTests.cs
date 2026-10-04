using System.Linq;
using FactoryTD.Sim;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class LawnTests
{
	private static readonly MapLayout Garden = MapLayout.CreateDefault(theme: MapTheme.Garden);

	[Fact]
	public void Clover_GrowsInPatches_ThickInTheMiddle_WithPlainLawnBetween()
	{
		var lawn = Enumerable.Range(0, Garden.Height).SelectMany(y => Enumerable.Range(0, Garden.Width).Select(x => (x, y)))
			.Where(t => Lawn.IsLawn(Garden, t.x, t.y)).ToList();
		var density = lawn.ToDictionary(t => t, t => Lawn.Density(t.x, t.y));
		double withClover = density.Values.Count(d => d > 0) / (double)lawn.Count;
		Assert.InRange(withClover, 0.08, 0.4);                    // some lawn, not a carpet of clover
		Assert.Contains(3, density.Values);
		// patches, not salt and pepper: a thick tile almost always has clover beside it
		var thick = density.Where(p => p.Value == 3).Select(p => p.Key).ToList();
		int lonely = thick.Count(t => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }
			.All(o => Lawn.Density(t.x + o.Item1, t.y + o.Item2) == 0));
		Assert.True(lonely * 10 < thick.Count, $"{lonely} of {thick.Count} thick tiles stand alone");
	}

	[Fact]
	public void Clover_OnlyOnTheGardensLawn_AlwaysTheSame()
	{
		var clovers = Lawn.Clovers(Garden);
		Assert.NotEmpty(clovers);
		Assert.Equal(clovers, Lawn.Clovers(MapLayout.CreateDefault(theme: MapTheme.Garden)));
		Assert.Empty(Lawn.Clovers(MapLayout.CreateDefault(theme: MapTheme.Nursery)));
		Assert.All(clovers, c =>
		{
			Assert.InRange(c.Sprite, 0, Lawn.Leaves + Lawn.Flowers - 1);
			// the tile under it is lawn, or the clover only pokes a few pixels over the lawn's edge
			int x = (c.X + 6) / 64, y = (c.Y + 6) / 64;
			Assert.True(Lawn.IsLawn(Garden, x, y) || Lawn.IsLawn(Garden, (c.X - 6) / 64, (c.Y - 6) / 64), $"clover at {c}");
		});
		Assert.Contains(clovers, c => c.Sprite >= Lawn.Leaves);  // a few flowers among them
		Assert.True(clovers.Zip(clovers.Skip(1)).All(p => p.First.Y <= p.Second.Y), "drawn back to front");
	}
}
