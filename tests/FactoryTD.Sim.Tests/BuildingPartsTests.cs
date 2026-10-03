using System;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class BuildingPartsTests
{
	private static void Near(float expected, float actual, string what) =>
		Assert.True(MathF.Abs(expected - actual) < 0.01f, $"{what}: expected {expected}, got {actual}");

	[Theory]
	[InlineData(Motion.Spin)]
	[InlineData(Motion.Swing)]
	[InlineData(Motion.Bob)]
	[InlineData(Motion.Orbit)]
	[InlineData(Motion.Blink)]
	[InlineData(Motion.Float)]
	public void Motions_LoopWithoutAJump(Motion motion)
	{
		var part = new Part("x", 3, -4, motion, Speed: 0.8f, DX: 10, DY: 6, Amount: 0.4f, Phase: 0.25f);
		float period = 1 / part.Speed;
		foreach (float t in new[] { 0f, 0.3f, 1.7f })
		{
			var a = BuildingParts.PoseAt(part, t);
			var b = BuildingParts.PoseAt(part, t + period);
			Near(a.X, b.X, "x");
			Near(a.Y, b.Y, "y");
			Near(MathF.Sin(a.Angle), MathF.Sin(b.Angle), "angle");
			Near(a.Alpha, b.Alpha, "alpha");
		}
	}

	[Fact]
	public void Motions_DoWhatTheySay()
	{
		var bob = new Part("x", 0, 0, Motion.Bob, DX: 0, DY: -8);
		Near(0, BuildingParts.PoseAt(bob, 0).Y, "bob starts at the anchor");
		Near(-8, BuildingParts.PoseAt(bob, 0.5f).Y, "bob is fully out half way");
		var puff = new Part("x", 0, 0, Motion.Puff, DY: -20);
		Assert.True(BuildingParts.PoseAt(puff, 0.9f).Alpha < BuildingParts.PoseAt(puff, 0.1f).Alpha, "a puff fades as it rises");
		Assert.True(BuildingParts.PoseAt(puff, 0.9f).Y < BuildingParts.PoseAt(puff, 0.1f).Y, "a puff rises");
		var spin = new Part("x", 0, 0, Motion.Spin, Speed: -1);
		Assert.True(BuildingParts.PoseAt(spin, 0.25f).Angle < 0, "negative speed turns the other way");
		var orbit = new Part("x", 0, 0, Motion.Orbit, DX: 20, DY: 10);
		Near(20, BuildingParts.PoseAt(orbit, 0).X, "orbit starts at the right");
		Near(10, BuildingParts.PoseAt(orbit, 0.25f).Y, "and drives down (y grows) first");
	}

	[Fact]
	public void Float_BobsAndRocks_AQuarterBeatApart_AndAlwaysPartsUseTheWallClock()
	{
		var ship = new Part("x", 0, 0, Motion.Float, DY: 3, Amount: 0.05f, Always: true);
		Near(3, BuildingParts.PoseAt(ship, 0.25f).Y, "highest bob a quarter in");
		Near(0, BuildingParts.PoseAt(ship, 0.25f).Angle, "level while at the top");
		Near(0.05f, BuildingParts.PoseAt(ship, 0).Angle, "most tilted at the middle of the bob");
		Assert.Equal(7f, BuildingParts.TimeFor(ship, workTime: 2, wallTime: 7));
		Assert.Equal(2f, BuildingParts.TimeFor(ship with { Always = false }, workTime: 2, wallTime: 7));
		Assert.True(BuildingParts.KeepsUpright(BuildingType.SoldierFactory));
		Assert.False(BuildingParts.KeepsUpright(BuildingType.Assembler));
	}

	[Fact]
	public void EveryPart_StaysOnItsBuilding_AndHasASprite()
	{
		foreach (var type in BuildingParts.Animated)
		{
			var (w, h) = BuildingRules.Size(type);
			float halfW = w * 32, halfH = h * 32;
			foreach (var part in BuildingParts.For(type))
			{
				Assert.True(ArtFiles.Exists($"Parts/{part.Sprite}.png"), $"{type}: no sprite Parts/{part.Sprite}.png");
				for (float t = 0; t < 4; t += 0.05f)
				{
					var pose = BuildingParts.PoseAt(part, t);
					Assert.True(MathF.Abs(pose.X) <= halfW && MathF.Abs(pose.Y) <= halfH,
						$"{type} {part.Sprite} leaves the building at t={t}: ({pose.X}, {pose.Y})");
				}
			}
		}
	}

	[Fact]
	public void Clock_RunsWhileWorking_EasesToAStop_AndIsOffsetPerBuilding()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var a = s.Place(BuildingType.Assembler, 20, 20);
		var b = s.Place(BuildingType.Assembler, 21, 20);
		var clocks = new AnimationClocks();
		float startA = clocks.Advance(a, false, 0), startB = clocks.Advance(b, false, 0);
		Assert.NotEqual(startA, startB);
		float t = startA;
		for (int i = 0; i < 60; i++)
			t = clocks.Advance(a, true, 1 / 60f);
		Assert.InRange(t - startA, 0.75f, 1f); // a second of work, minus the run-up
		float stopping = t;
		for (int i = 0; i < 60; i++)
			t = clocks.Advance(a, false, 1 / 60f);
		Assert.InRange(t - stopping, 0.05f, AnimationClocks.Ease); // coasts, then stands
		Assert.Equal(t, clocks.Advance(a, false, 1));
	}
}
