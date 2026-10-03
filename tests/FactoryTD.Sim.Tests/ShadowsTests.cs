using System;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using FactoryTD.UI;
using Xunit;

namespace FactoryTD.Sim.Tests;

public class ShadowsTests
{
	private static bool Open(int fx, int fy, int tx, int ty) => true;

	private static Shadow Cast(params LightSource[] lights) =>
		Shadows.Cast(10.5f, 10.5f, 0.6f, lights, Open) ?? throw new Xunit.Sdk.XunitException("no shadow");

	private static LightSource Lamp(float x, float y, float strength = 1) => new(x, y, 9, LightTone.Warm, strength);

	[Fact]
	public void NoLight_NoShadow()
	{
		Assert.Null(Shadows.Cast(10.5f, 10.5f, 0.6f, Array.Empty<LightSource>(), Open));
		Assert.Null(Shadows.Cast(10.5f, 10.5f, 0.6f, new[] { Lamp(30, 10.5f) }, Open));     // out of reach
		Assert.Null(Shadows.Cast(10.5f, 10.5f, 0.6f, new[] { Lamp(10.6f, 10.5f) }, Open));  // its own glow
	}

	[Fact]
	public void PointsAwayFromTheLight()
	{
		var east = Cast(Lamp(7.5f, 10.5f));   // light to the west
		Assert.True(MathF.Cos(east.Angle) > 0.99f, $"angle {east.Angle}");
		Assert.True(east.X > 10.5f, "the shadow sits east of the feet");
		var north = Cast(Lamp(10.5f, 14.5f)); // light to the south
		Assert.True(MathF.Sin(north.Angle) < -0.99f, $"angle {north.Angle}");
	}

	[Fact]
	public void FurtherLight_LongerAndFainterShadow()
	{
		var near = Cast(Lamp(8.5f, 10.5f));
		var far = Cast(Lamp(3.5f, 10.5f));
		Assert.True(far.Length > near.Length, $"far {far.Length}, near {near.Length}");
		Assert.True(far.Alpha < near.Alpha, $"far {far.Alpha}, near {near.Alpha}");
	}

	[Fact]
	public void WallInBetween_TheLightDoesNotCount()
	{
		bool walled(int fx, int fy, int tx, int ty) => false;
		Assert.Null(Shadows.Cast(10.5f, 10.5f, 0.6f, new[] { Lamp(7.5f, 10.5f) }, walled));
	}

	[Fact]
	public void OppositeLights_LeaveAShortRoundShadow()
	{
		var one = Cast(Lamp(6.5f, 10.5f));
		var both = Cast(Lamp(6.5f, 10.5f), Lamp(14.5f, 10.5f));
		Assert.True(both.Length < one.Length, $"both {both.Length}, one {one.Length}");
		Assert.True(MathF.Abs(both.Length - both.Width) < 0.01f, "round");
		Assert.True(both.Alpha >= one.Alpha, "more light, at least as dark");
	}

	[Fact]
	public void InTheGame_ALampCastsShadows_AWallStopsIt_DarknessHasNone()
	{
		var s = Scenario.Match().NoWorkers().Instant().RealFog();
		s.Place(BuildingType.Lamp, 30, 42);
		var lit = s.Place(BuildingType.Assembler, 33, 42);       // east of the lamp
		var unit = s.Spawn(UnitType.Scout, 28, 42);               // west of it (its own torch is ignored)
		s.World.Ticks(4);                                         // the fog sees them
		var caster = new ShadowCaster();
		for (int i = 0; i < 20; i++)
			caster.Update(s.World, 0, 0.05f);
		var building = caster.Buildings.Single(b => b.Building == lit).Shadow;
		Assert.True(MathF.Cos(building.Angle) > 0.9f, "the assembler's shadow points east, away from the lamp");
		var scout = caster.Units.Single(u => u.Unit == unit).Shadow;
		Assert.True(MathF.Cos(scout.Angle) < -0.5f, "the scout's shadow points west, away from the lamp");
		var belt = s.Place(BuildingType.Conveyor, 31, 43);
		caster.Update(s.World, 0, 0.05f);
		Assert.DoesNotContain(caster.Buildings, b => b.Building == belt); // flat on the floor
		Assert.True(Vision.LightReaches(s.World.Map, 30, 42, 33, 42));
		Assert.False(Vision.LightReaches(s.World.Map, 30, 42, 30, 70), "the room's wall is in between");
	}

	[Fact]
	public void Caster_EasesTowardsTheShadow_AndFadesOutWhenTheLightGoes()
	{
		var s = Scenario.Match().NoWorkers().Instant();
		var lamp = s.Place(BuildingType.Lamp, 30, 42);
		s.Place(BuildingType.Assembler, 33, 42);
		var caster = new ShadowCaster();
		caster.Update(s.World, 0, 0.05f);
		float first = caster.Buildings.Single(b => b.Building.Type == BuildingType.Assembler).Shadow.Alpha;
		for (int i = 0; i < 20; i++)
			caster.Update(s.World, 0, 0.05f);
		float settled = caster.Buildings.Single(b => b.Building.Type == BuildingType.Assembler).Shadow.Alpha;
		Assert.True(first < settled, "fades in");
		s.World.TryRemove(lamp.X, lamp.Y, 0);
		caster.Update(s.World, 0, 0.05f);
		Assert.Contains(caster.Buildings, b => b.Building.Type == BuildingType.Assembler); // still fading
		for (int i = 0; i < 40; i++)
			caster.Update(s.World, 0, 0.05f);
		Assert.DoesNotContain(caster.Buildings, b => b.Building.Type == BuildingType.Assembler);
	}
}
