using System;

namespace FactoryTD.UI;

/// <summary>
/// The start menu's background: a bot-against-bot match playing behind the menu, seen from a camera that
/// drifts slowly from one base over the hall to the other and back, breathing its zoom a little.
/// </summary>
public static class MenuPreview
{
	/// <summary>Seconds for the camera to go from one side and back.</summary>
	public const float Period = 80f;

	/// <summary>The match runs faster than normal, so there's something happening.</summary>
	public const float Speed = 2f;

	public const float Zoom = 0.4f, ZoomBreath = 0.05f;

	/// <summary>Seconds of match played straight away, so the bases already stand when the menu opens.</summary>
	public const float HeadStart = 40f;

	/// <summary>Where the camera looks after <paramref name="seconds"/>, in pixels on a map of the given size.</summary>
	public static (float X, float Y, float Zoom) CameraAt(float seconds, float mapWidth, float mapHeight)
	{
		float phase = seconds / Period * MathF.Tau;
		// From over one base (a tenth of the way in from the side) to the other, easing at the ends.
		float x = mapWidth * (0.5f - 0.4f * MathF.Cos(phase));
		float y = mapHeight * (0.5f + 0.08f * MathF.Sin(phase * 2));
		return (x, y, Zoom + ZoomBreath * MathF.Sin(phase * 3));
	}
}
