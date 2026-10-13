using System;

namespace FactoryTD.UI;

/// <summary>Where the samurai's slash is drawn: an arc round the samurai, through the unit it hits.</summary>
public static class SlashLook
{
	/// <summary>Half the drawn arc's width, in radians.</summary>
	public const float HalfWidth = 0.45f;

	/// <summary>The arc (radius and start/end angle, radians, Godot's y-down) for a slash from one point to another.</summary>
	public static (float Radius, float Start, float End) Arc(float fromX, float fromY, float toX, float toY)
	{
		float dx = toX - fromX, dy = toY - fromY;
		float angle = MathF.Atan2(dy, dx);
		return (MathF.Sqrt(dx * dx + dy * dy), angle - HalfWidth, angle + HalfWidth);
	}

	/// <summary>How visible the slash is through its short flight: full at once, gone at the end.</summary>
	public static float Fade(float progress) => Math.Clamp(1f - progress * progress, 0f, 1f);
}
