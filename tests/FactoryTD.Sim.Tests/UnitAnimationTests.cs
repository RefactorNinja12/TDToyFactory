using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class UnitAnimationTests
{
	/// <summary>Runs the world, observing every tick, and records each frame of <paramref name="unit"/>.</summary>
	private static List<UnitFrame> Watch(Scenario s, Unit unit, int ticks, UnitAnimation animation)
	{
		var frames = new List<UnitFrame>();
		for (int t = 0; t < ticks; t++)
		{
			s.World.Tick();
			animation.Observe(s.World);
			frames.Add(animation.FrameOf(unit));
		}
		return frames;
	}

	[Theory]
	[InlineData(1000, 0, Facing.Right)]
	[InlineData(-1000, 0, Facing.Left)]
	[InlineData(0, 1000, Facing.Toward)]
	[InlineData(0, -1000, Facing.Away)]
	[InlineData(1000, 300, Facing.Right)]
	public void Facing_FollowsTheLongerAxis(int dx, int dy, Facing expected) =>
		Assert.Equal(expected, UnitAnimation.FacingFor(dx, dy, Facing.Toward));

	[Fact]
	public void Facing_NearTheDiagonal_KeepsWhatItHad_SoItDoesNotFlicker()
	{
		Assert.Equal(Facing.Right, UnitAnimation.FacingFor(1000, 1050, Facing.Right));
		Assert.Equal(Facing.Toward, UnitAnimation.FacingFor(1050, 1000, Facing.Toward));
		Assert.Equal(Facing.Toward, UnitAnimation.FacingFor(1000, 1300, Facing.Right)); // clearly more down: turns
		Assert.Equal(Facing.Left, UnitAnimation.FacingFor(-1000, 1000, Facing.Right));  // the old facing no longer fits
		Assert.Equal(Facing.Away, UnitAnimation.FacingFor(0, 0, Facing.Away));          // no direction: unchanged
	}

	[Fact]
	public void WalkingSoldier_FacesItsWay_AndStepsWithTheDistance()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var soldier = s.Spawn(UnitType.PlasticSoldier, 40, 30);                                  // heads east, to the enemy toybox
		var enemy = s.Spawn(UnitType.PlasticSoldier, s.World.Map.Width - 1 - 40, 30, owner: 1); // the mirror image: west
		var animation = new UnitAnimation();
		var frames = Watch(s, soldier, 60, animation);
		var last = frames[^1];
		Assert.Equal(PoseKind.Walk, last.Kind);
		Assert.Equal(Facing.Right, last.Facing);
		Assert.Equal(Facing.Left, animation.FrameOf(enemy).Facing);
		// Every walk step shows up, in order, as the soldier covers ground.
		var steps = frames.Where(f => f.Kind == PoseKind.Walk).Select(f => f.Step).Distinct().OrderBy(x => x);
		Assert.Equal(new[] { 0, 1, 2, 3 }, steps);
	}

	[Fact]
	public void SoldierInRange_ShowsTheAttack_RightAfterEachShot()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var soldier = s.Spawn(UnitType.PlasticSoldier, 60, 30);
		s.Spawn(UnitType.PlasticSoldier, 61, 30, owner: 1);
		var frames = Watch(s, soldier, 40, new UnitAnimation());
		Assert.Contains(frames, f => f.Kind == PoseKind.Act && f.Step == 0);
		Assert.Contains(frames, f => f.Kind == PoseKind.Act && f.Step == 1);
		Assert.Contains(frames, f => f.Kind == PoseKind.Idle); // between shots it stands
		Assert.DoesNotContain(frames, f => f.Kind == PoseKind.Walk);
	}

	[Fact]
	public void Builder_WalksToTheSite_ThenHammers_ThenIdles()
	{
		var s = Scenario.Match().NoWorkers();
		var builder = s.Spawn(UnitType.Builder, 14, 30);
		s.Place(BuildingType.Warehouse, 20, 30);
		var animation = new UnitAnimation();
		var frames = Watch(s, builder, 20 * 40, animation);
		int firstWalk = frames.FindIndex(f => f.Kind == PoseKind.Walk);
		int firstWork = frames.FindIndex(f => f.Kind == PoseKind.Act);
		Assert.InRange(firstWalk, 0, 5);
		Assert.True(firstWork > firstWalk, "walks there first, then works");
		Assert.Equal(new[] { 0, 1 }, frames.Where(f => f.Kind == PoseKind.Act).Select(f => f.Step).Distinct().OrderBy(x => x));
		Assert.Equal(PoseKind.Idle, frames[^1].Kind); // the warehouse is done: nothing left to do
	}

	[Fact]
	public void DeadUnits_AreForgotten_AndUnknownOnesIdle()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var unit = s.Spawn(UnitType.Scout, 20, 30);
		var animation = new UnitAnimation();
		animation.Observe(s.World);
		Assert.Equal(new UnitFrame(Facing.Toward, PoseKind.Idle, 0), animation.FrameOf(new Unit(999, UnitType.Scout, 0, 0, 0)));
		unit.Health = 0;
		s.World.Ticks(2);
		animation.Observe(s.World);
		Assert.Equal(PoseKind.Idle, animation.FrameOf(unit).Kind);
	}

	[Theory]
	[InlineData(Facing.Toward, PoseKind.Idle, 0, 0, 0, false)]
	[InlineData(Facing.Away, PoseKind.Walk, 2, 3, 1, false)]
	[InlineData(Facing.Right, PoseKind.Walk, 3, 4, 2, false)]
	[InlineData(Facing.Left, PoseKind.Walk, 0, 1, 2, true)]
	[InlineData(Facing.Left, PoseKind.Act, 1, 6, 2, true)]
	public void Sheet_CellForEveryFrame(Facing facing, PoseKind kind, int step, int column, int row, bool mirror) =>
		Assert.Equal((column, row, mirror), UnitSheets.Cell(new UnitFrame(facing, kind, step)));
}
