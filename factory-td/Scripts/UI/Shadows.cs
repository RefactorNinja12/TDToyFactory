using System;
using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>
/// A soft round shadow on the floor, in tiles: its centre, the way it points (radians, 0 = east), its length
/// along that way and its width across, and how dark it is (0..1). It never turns with the sprite.
/// </summary>
public readonly record struct Shadow(float X, float Y, float Angle, float Length, float Width, float Alpha);

/// <summary>
/// Shadows cast by the light sources (the same ones the fog glows with, UI/LightSources). No light, no shadow.
/// A light counts when the thing is inside its radius, the thing isn't the light itself (its own torch or
/// glow) and no wall is in between. The shadow points away from the light; a near light gives a short dark
/// shadow, a far one a longer, fainter one. Several lights add up by strength, so two lights from opposite
/// sides leave a short round shadow.
/// </summary>
public static class Shadows
{
	/// <summary>Lights closer than this (tiles) are the thing's own glow and cast nothing.</summary>
	public const float OwnLight = 0.4f;

	public const float MaxAlpha = 0.6f;

	/// <summary>The darkest any shadow gets (a big toy right by a lamp).</summary>
	public const float MaxDark = 0.85f;

	/// <summary>Below this summed light the shadow is too faint to draw.</summary>
	public const float MinLight = 0.03f;

	/// <summary>
	/// The shadow of something of <paramref name="size"/> tiles standing at (x, y), or null when no light
	/// reaches it. <paramref name="reaches"/> says whether light gets from a light's tile to the thing's tile.
	/// <paramref name="tall"/>: how far the shadow reaches out for its size (1 = a unit or building, more for the
	/// towering toys and plants); <paramref name="dark"/>: how much darker (up to <see cref="MaxDark"/>).
	/// </summary>
	public static Shadow? Cast(float x, float y, float size, IEnumerable<LightSource> lights, Func<int, int, int, int, bool> reaches,
		float tall = 1, float dark = 1)
	{
		float sumX = 0, sumY = 0, light = 0;
		foreach (var l in lights)
		{
			float dx = x - l.X, dy = y - l.Y;
			float distance = MathF.Sqrt(dx * dx + dy * dy);
			if (distance < OwnLight || distance >= l.Radius)
				continue;
			if (!reaches((int)MathF.Floor(l.X), (int)MathF.Floor(l.Y), (int)MathF.Floor(x), (int)MathF.Floor(y)))
				continue;
			float near = 1 - distance / l.Radius;            // 1 next to the light, 0 at the edge of its reach
			float weight = l.Strength * near;
			float stretch = 0.3f + 0.7f * (1 - near);          // further away: a longer shadow
			sumX += dx / distance * stretch * weight;
			sumY += dy / distance * stretch * weight;
			light += weight;
		}
		if (light < MinLight)
			return null;
		float pushX = sumX / light, pushY = sumY / light;      // average direction, scaled by the stretch
		float push = MathF.Sqrt(pushX * pushX + pushY * pushY);
		float reach = push * tall;
		float width = size * 0.8f;
		float length = width + size * reach;
		float angle = push > 0.001f ? MathF.Atan2(pushY, pushX) : MathF.PI / 2;
		// The far end reaches out from under the thing; the near end stays at its feet.
		float centre = size * reach * 0.5f;
		float alpha = MathF.Min(MaxDark, MaxAlpha * dark * MathF.Min(1, light * 1.5f) * (1 - 0.4f * MathF.Min(1, push)));
		return new Shadow(x + MathF.Cos(angle) * centre, y + MathF.Sin(angle) * centre, angle, length, width, alpha);
	}
}

/// <summary>
/// The shadows of everything the player can see, eased over time so passing shots and walking units don't
/// make them twitch. Units stand on their position; buildings and the big toys and plants (obstacles) on their
/// footprint centre. Obstacles are tall, so their shadows are big; the plants' soil beds lie flat and cast none.
/// </summary>
public sealed class ShadowCaster
{
	/// <summary>Seconds to follow a change.</summary>
	public const float Ease = 0.25f;

	public const float UnitSize = 0.55f;

	/// <summary>An obstacle's shadow, per tile of its longest side (a building's is 0.9): they tower over the floor.</summary>
	public const float ObstacleSize = 1.15f;

	/// <summary>How much further an obstacle's shadow reaches out than a building's (Shadows.Cast tall).</summary>
	public const float ObstacleTall = 2.5f;

	/// <summary>How much darker an obstacle's shadow is than a building's: a deep shade, so it shows through the lamp's glow.</summary>
	public const float ObstacleDark = 1.9f;

	private readonly Dictionary<object, Shadow> _current = new();
	private readonly HashSet<object> _seen = new();

	public List<(Unit Unit, Shadow Shadow)> Units { get; } = new();
	public List<(Building Building, Shadow Shadow)> Buildings { get; } = new();
	public List<(Obstacle Obstacle, Shadow Shadow)> Obstacles { get; } = new();

	public void Update(World world, int player, float delta)
	{
		var lights = LightSources.For(world, player);
		bool Reaches(int fx, int fy, int tx, int ty) => Vision.LightReaches(world.Map, fx, fy, tx, ty);
		float k = Math.Clamp(delta / Ease, 0, 1);
		Units.Clear();
		Buildings.Clear();
		Obstacles.Clear();
		_seen.Clear();
		foreach (var unit in world.Units)
		{
			if (!world.CanSee(player, unit))
				continue;
			float x = unit.X / (float)UnitStats.SubTile, y = unit.Y / (float)UnitStats.SubTile;
			if (Follow(unit, Shadows.Cast(x, y, UnitSize, Near(lights, x, y), Reaches), k) is { } shadow)
				Units.Add((unit, shadow));
		}
		foreach (var building in world.Buildings)
		{
			if (IsFlat(building.Type) || !Knowledge.ShowBuilding(world, player, building))
				continue;
			var (w, h) = BuildingRules.Size(building.Type);
			float x = building.X + w / 2f, y = building.Y + h / 2f;
			if (Follow(building, Shadows.Cast(x, y, Math.Max(w, h) * 0.9f, Near(lights, x, y), Reaches), k) is { } shadow)
				Buildings.Add((building, shadow));
		}
		foreach (var toy in world.Map.Obstacles)
		{
			float x = toy.X + toy.Width / 2f, y = toy.Y + toy.Height / 2f;
			float size = Math.Max(toy.Width, toy.Height) * ObstacleSize;
			if (Follow(toy, Shadows.Cast(x, y, size, Near(lights, x, y), Reaches, ObstacleTall, ObstacleDark), k) is { } shadow)
				Obstacles.Add((toy, shadow));
		}
		if (_current.Count > _seen.Count)
			foreach (var gone in new List<object>(_current.Keys))
				if (!_seen.Contains(gone))
					_current.Remove(gone);
	}

	/// <summary>Belts and junctions lie flat on the floor: no shadow.</summary>
	public static bool IsFlat(BuildingType type) => type is BuildingType.Conveyor or BuildingType.Junction;

	/// <summary>Moves the shown shadow towards the target; with no light it fades out instead of vanishing.</summary>
	private Shadow? Follow(object key, Shadow? target, float k)
	{
		_seen.Add(key);
		if (!_current.TryGetValue(key, out var now))
		{
			if (target is not { } fresh)
				return null;
			now = fresh with { Alpha = 0 };
		}
		var to = target ?? now with { Alpha = 0 };
		var next = new Shadow(Lerp(now.X, to.X, k), Lerp(now.Y, to.Y, k), LerpAngle(now.Angle, to.Angle, k),
			Lerp(now.Length, to.Length, k), Lerp(now.Width, to.Width, k), Lerp(now.Alpha, to.Alpha, k));
		if (target == null && next.Alpha < 0.01f)
		{
			_current.Remove(key);
			return null;
		}
		_current[key] = next;
		return next;
	}

	private static IEnumerable<LightSource> Near(List<LightSource> lights, float x, float y)
	{
		foreach (var l in lights)
			if (MathF.Abs(l.X - x) < l.Radius && MathF.Abs(l.Y - y) < l.Radius)
				yield return l;
	}

	private static float Lerp(float a, float b, float k) => a + (b - a) * k;

	private static float LerpAngle(float a, float b, float k)
	{
		float diff = MathF.IEEERemainder(b - a, MathF.Tau);
		return a + diff * k;
	}
}
