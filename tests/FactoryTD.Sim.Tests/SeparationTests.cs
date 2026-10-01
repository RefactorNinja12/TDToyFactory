using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class SeparationTests
{
	private static int ClosestPair(System.Collections.Generic.List<Unit> units, out int needed)
	{
		int best = int.MaxValue;
		needed = 0;
		for (int i = 0; i < units.Count; i++)
			for (int j = i + 1; j < units.Count; j++)
			{
				int dx = units[i].X - units[j].X, dy = units[i].Y - units[j].Y;
				int d = IntMath.Sqrt(dx * dx + dy * dy);
				if (d < best)
				{
					best = d;
					needed = World.UnitRadius(units[i].Type) + World.UnitRadius(units[j].Type);
				}
			}
		return best;
	}

	[Fact]
	public void UnitsSpawnedOnOneSpot_SpreadOut()
	{
		var s = Scenario.Match().NoWorkers();
		for (int i = 0; i < 8; i++)
			s.Spawn(UnitType.Builder, 30, 45); // nothing to build: they stand still
		s.World.Seconds(3);
		int closest = ClosestPair(s.World.Units.ToList(), out int needed);
		Assert.True(closest >= needed * 9 / 10, $"closest pair {closest}, needs {needed}");
	}

	[Fact]
	public void MarchingSoldiers_DontStack()
	{
		var s = Scenario.Match().NoWorkers();
		for (int i = 0; i < 10; i++)
			s.Spawn(UnitType.PlasticSoldier, 70, 30);
		s.World.Seconds(4);
		int closest = ClosestPair(s.World.Units.ToList(), out int needed);
		Assert.True(closest >= needed * 3 / 4, $"closest pair {closest}, needs {needed}");
	}

	[Fact]
	public void NeverPushedIntoAWall()
	{
		var s = Scenario.Match().NoWorkers();
		for (int i = 0; i < 8; i++)
			s.Spawn(UnitType.Builder, 1, 1); // the room's corner
		s.World.Seconds(3);
		foreach (var u in s.World.Units)
			Assert.Equal(TileType.Floor, s.World.Map[u.TileX, u.TileY]);
	}
}
