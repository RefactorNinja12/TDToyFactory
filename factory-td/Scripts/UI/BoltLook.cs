using System;
using System.Collections.Generic;

namespace FactoryTD.UI;

/// <summary>
/// How the wizard's thunderbolt is drawn (pixels, Godot's y-down). While it is on its way: a warning crackling on the
/// floor at the spot. Then a jagged pillar of lightning falls from high above onto it (in the first tick it is still
/// coming down), and short zigzag sparks fly out over a small ring. The jags come from the spot and the tick, never
/// from random numbers, so the same strike always looks the same (and can be tested).
/// </summary>
public static class BoltLook
{
	/// <summary>Ticks the pillar shows after the strike.</summary>
	public const int StrikeTicks = 3;

	/// <summary>Ticks the sparks fly after the strike (shorter than the golem's slam ring).</summary>
	public const int SparkTicks = 8;

	/// <summary>How far above the spot the pillar starts (off the top of the screen at normal zoom).</summary>
	public const float FallHeight = 420f;

	public const int Segments = 9;
	public const float Jitter = 9f;
	public const int SparkCount = 8;

	/// <summary>0 at the cast, 1 when it strikes.</summary>
	public static float Warning(int totalTicks, int ticksLeft, float alpha) =>
		Math.Clamp((totalTicks - ticksLeft + alpha) / Math.Max(1, totalTicks), 0f, 1f);

	/// <summary>Whether the pillar shows <paramref name="age"/> ticks after the strike.</summary>
	public static bool Striking(float age) => age >= 0 && age < StrikeTicks;

	/// <summary>Whether the sparks still fly.</summary>
	public static bool Sparking(float age) => age >= 0 && age < StrikeTicks + SparkTicks;

	/// <summary>
	/// The pillar's points from the sky down to the spot (x, y): a jagged line, different every tick. In the first tick
	/// it only reaches part of the way down (it falls).
	/// </summary>
	public static List<(float X, float Y)> Pillar(float x, float y, long tick, float age)
	{
		float top = y - FallHeight;
		float bottom = top + FallHeight * Math.Clamp(age, 0f, 1f);
		var points = new List<(float X, float Y)>();
		for (int i = 0; i <= Segments; i++)
		{
			float py = top + FallHeight * i / Segments;
			if (py > bottom + 0.001f)
			{
				points.Add((x + Noise(x, y, tick, i) * Jitter, bottom));
				break;
			}
			points.Add((i == 0 || i == Segments ? x : x + Noise(x, y, tick, i) * Jitter, py));
		}
		return points;
	}

	/// <summary>How bright the pillar is: full when it lands, fading over the strike.</summary>
	public static float StrikeFade(float age) => Striking(age) ? 1f - age / StrikeTicks * 0.6f : 0f;

	/// <summary>
	/// The sparks round the spot: each a short zigzag (three points) out from it, reaching further as they fly, never
	/// much past <paramref name="radius"/>.
	/// </summary>
	public static List<(float X, float Y)[]> Sparks(float x, float y, float age, float radius)
	{
		var sparks = new List<(float X, float Y)[]>();
		if (!Sparking(age))
			return sparks;
		float t = age / (StrikeTicks + SparkTicks);
		for (int i = 0; i < SparkCount; i++)
		{
			float angle = MathF.Tau * (i + 0.3f * Noise(x, y, i, 99)) / SparkCount;
			float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
			float near = radius * (0.2f + 0.7f * t), far = radius * (0.45f + 0.75f * t);
			float mid = (near + far) / 2, kink = 5f * Noise(x, y, i, (long)age);
			sparks.Add(new[]
			{
				(x + cos * near, y + sin * near),
				(x + cos * mid - sin * kink, y + sin * mid + cos * kink),
				(x + cos * far, y + sin * far),
			});
		}
		return sparks;
	}

	/// <summary>How bright the sparks and the small ring are.</summary>
	public static float SparkFade(float age) => Sparking(age) ? 1f - age / (StrikeTicks + SparkTicks) : 0f;

	/// <summary>A number in [-1, 1] from the inputs (an integer hash, the same every time).</summary>
	private static float Noise(float x, float y, long a, long b)
	{
		unchecked
		{
			uint h = (uint)(int)x * 73856093u ^ (uint)(int)y * 19349663u ^ (uint)a * 83492791u ^ (uint)b * 2654435761u;
			h ^= h >> 13;
			h *= 1274126177u;
			h ^= h >> 16;
			return (h % 2001) / 1000f - 1f;
		}
	}
}
