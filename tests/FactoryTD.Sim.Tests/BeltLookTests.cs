using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class BeltLookTests
{
	[Fact]
	public void Treads_MoveAsFastAsTheItems()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var belt = s.Conveyor(20, 40, Direction.East);
		s.Conveyor(21, 40, Direction.East);
		Assert.Equal(1, s.Feed(belt, ItemType.Brick));
		int start = belt.Items[0].Progress;
		s.World.Ticks(10);
		float pixelsPerTick = (belt.Items[0].Progress - start) * (float)BeltLook.TileSize / Conveyor.Length / 10;
		Assert.Equal(BeltLook.PixelsPerSecond, pixelsPerTick * World.TicksPerSecond, 3);
	}

	[Fact]
	public void Phase_ShiftsAlongTheBelt_AsTimeGoes()
	{
		Assert.Equal(0f, BeltLook.Phase(0, 0), 3);
		// After the time it takes to move one spacing, the pattern is back where it was.
		float oneSpacing = BeltLook.TreadSpacing / BeltLook.PixelsPerSecond;
		Assert.Equal(BeltLook.Phase(5, 0.1f), BeltLook.Phase(5, 0.1f + oneSpacing), 3);
		// A ridge that was at x moves forwards (east) with time.
		Assert.Equal(BeltLook.Phase(10, 0), BeltLook.Phase(10 + BeltLook.PixelsPerSecond * 0.05f, 0.05f), 3);
	}
}
