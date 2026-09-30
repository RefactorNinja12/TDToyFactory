using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class FogUiTests
{
	private static Scenario Fog() => Scenario.Match().NoWorkers().Instant().Rich().RealFog();
	private static byte[] Cells(Scenario s) => new byte[s.World.Map.Width * s.World.Map.Height];
	private static int I(Scenario s, int x, int y) => y * s.World.Map.Width + x;

	[Fact]
	public void FogLevels_UnknownExploredLit()
	{
		var s = Fog();
		var soldier = s.Spawn(UnitType.PlasticSoldier, 30, 45);
		s.World.Seconds(10); // it walked off: (30, 45) is explored but dark now
		var fog = Cells(s);
		FogLevels.Build(s.World, 0, fog);
		Assert.Equal(FogLevels.Unknown, fog[I(s, 41, 47)]);
		Assert.Equal(FogLevels.Explored, fog[I(s, 30, 45)]);
		Assert.Equal(FogLevels.Lit, fog[I(s, soldier.TileX, soldier.TileY)]);
	}

	[Fact]
	public void Minimap_TerrainDepositsBuildingsAndUnits_OnlyWhatWasSeen()
	{
		var s = Fog();
		s.Spawn(UnitType.PlasticSoldier, 41, 45); // lights the battery patch
		s.Place(BuildingType.Conveyor, 80, 30, owner: 1);
		s.World.Ticks(VisionStats.VisionTicks);
		var map = Cells(s);
		Minimap.Build(s.World, 0, map);
		Assert.Equal(Minimap.Unknown, map[I(s, 150, 31)]);
		Assert.Equal(Minimap.Bricks, Minimap.Kind(map[I(s, 10, 25)]));
		Assert.Equal(Minimap.Batteries, Minimap.Kind(map[I(s, 41, 47)]));
		Assert.True(Minimap.IsLit(map[I(s, 41, 47)]));
		Assert.Equal(Minimap.OwnBuilding, Minimap.Kind(map[I(s, 6, 30)])); // the toybox
		Assert.Contains(map, c => Minimap.Kind(c) == Minimap.OwnUnit);
		Assert.Equal(Minimap.Wall, Minimap.Kind(map[I(s, 0, 30)]));
		Assert.Equal(Minimap.Unknown, map[I(s, 80, 30)]); // the enemy belt hasn't been seen
	}

	[Fact]
	public void Minimap_EnemyUnitsOnlyInTheLight_BuildingsRemembered()
	{
		var s = Fog();
		s.Spawn(UnitType.Scout, 78, 32, owner: 1); // walks home eastwards, still in the light after one recompute
		s.Place(BuildingType.Conveyor, 80, 30, owner: 1);
		s.Place(BuildingType.BatteryCharger, 78, 30);
		s.World.Ticks(VisionStats.VisionTicks);
		var map = Cells(s);
		Minimap.Build(s.World, 0, map);
		Assert.Equal(Minimap.EnemyBuilding, Minimap.Kind(map[I(s, 80, 30)]));
		Assert.Contains(map, c => Minimap.Kind(c) == Minimap.EnemyUnit);

		Assert.True(s.World.TryRemove(78, 30, 0));
		s.World.Ticks(VisionStats.VisionTicks);
		Minimap.Build(s.World, 0, map);
		Assert.Equal(Minimap.EnemyBuilding, Minimap.Kind(map[I(s, 80, 30)])); // remembered
		Assert.False(Minimap.IsLit(map[I(s, 80, 30)]));
		Assert.DoesNotContain(map, c => Minimap.Kind(c) == Minimap.EnemyUnit);
	}

	[Fact]
	public void Knowledge_ShowsLitEnemies_GhostsTheRest()
	{
		var s = Fog();
		var belt = s.Place(BuildingType.Conveyor, 80, 30, owner: 1);
		Assert.False(Knowledge.ShowBuilding(s.World, 0, belt));
		Assert.True(Knowledge.ShowBuilding(s.World, 0, s.World.GetCore(0)));
		s.Place(BuildingType.BatteryCharger, 78, 30);
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.True(Knowledge.ShowBuilding(s.World, 0, belt));
		Assert.Empty(Knowledge.Ghosts(s.World, 0)); // in sight: drawn for real, not as a ghost
		s.World.TryRemove(78, 30, 0);
		s.World.Ticks(VisionStats.VisionTicks);
		Assert.False(Knowledge.ShowBuilding(s.World, 0, belt));
		var ghost = Assert.Single(Knowledge.Ghosts(s.World, 0));
		Assert.Equal((80, 30), (ghost.X, ghost.Y));
	}

	[Fact]
	public void Minimap_CameraRect_ClampedToTheMap()
	{
		Assert.Equal((0f, 10f, 40f, 20f), Minimap.CameraRect(-5, 5, 20, 15, 160, 62, 2));
		Assert.Equal((10f, 5f), Minimap.ToTiles(20, 10, 2));
	}
}
