using System;
using System.Linq;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class BoltLookTests
{
	[Fact]
	public void Warning_RunsFromTheCastToTheStrike()
	{
		Assert.Equal(0f, BoltLook.Warning(6, 6, 0));
		Assert.Equal(0.5f, BoltLook.Warning(6, 3, 0));
		Assert.Equal(1f, BoltLook.Warning(6, 0, 0.5f));
	}

	[Fact]
	public void Pillar_FallsFromHighAbove_OntoTheSpot()
	{
		var pillar = BoltLook.Pillar(100, 200, tick: 7, age: 1.5f);
		Assert.Equal((100f, 200f - BoltLook.FallHeight), pillar[0]);
		Assert.Equal((100f, 200f), pillar[^1]);
		Assert.Equal(BoltLook.Segments + 1, pillar.Count);
		Assert.Contains(pillar, p => p.X != 100f); // jagged
		Assert.All(pillar, p => Assert.InRange(p.X, 100 - BoltLook.Jitter, 100 + BoltLook.Jitter));

		var falling = BoltLook.Pillar(100, 200, tick: 7, age: 0.5f);
		Assert.True(falling[^1].Y < 150, "half way down in its first tick");
	}

	[Fact]
	public void Pillar_IsTheSameForTheSameStrike_DifferentNextTick()
	{
		Assert.Equal(BoltLook.Pillar(100, 200, 7, 2), BoltLook.Pillar(100, 200, 7, 2));
		Assert.NotEqual(BoltLook.Pillar(100, 200, 7, 2), BoltLook.Pillar(100, 200, 8, 2));
	}

	[Fact]
	public void Sparks_FlyOutOverASmallRing_ThenStop()
	{
		const float radius = 58;
		var sparks = BoltLook.Sparks(0, 0, age: 4, radius);
		Assert.Equal(BoltLook.SparkCount, sparks.Count);
		Assert.All(sparks, s => Assert.All(s, p => Assert.True(MathF.Sqrt(p.X * p.X + p.Y * p.Y) <= radius * 1.3f)));
		Assert.True(sparks.Select(s => MathF.Atan2(s[2].Y, s[2].X)).Distinct().Count() == BoltLook.SparkCount, "all round");
		Assert.Empty(BoltLook.Sparks(0, 0, BoltLook.StrikeTicks + BoltLook.SparkTicks, radius));
		Assert.True(BoltLook.Striking(0) && !BoltLook.Striking(BoltLook.StrikeTicks));
		Assert.True(BoltLook.SparkFade(1) > BoltLook.SparkFade(6));
	}
}
