namespace FactoryTD.UI;

/// <summary>
/// The countdown before a match: 3, 2, 1, "Kör!". The simulation stands still until it is over (the camera,
/// the build menu and placing work, so players can look around and plan); meanwhile the camera flies in from
/// an overview of the room to a close look at the player's own toybox.
/// </summary>
public static class Countdown
{
	public const float Seconds = 3f;

	/// <summary>How long "Kör!" stays up after the start.</summary>
	public const float GoSeconds = 0.8f;

	/// <summary>Zoom at the start (the room) and at the end (the toybox up close).</summary>
	public const float WideZoom = 0.35f, CloseZoom = 0.9f;

	/// <summary>Whether the match runs: only once the countdown is over.</summary>
	public static bool Started(float elapsed) => elapsed >= Seconds;

	/// <summary>What to show: "3", "2", "1", then "Kör!" for a moment, then nothing.</summary>
	public static string Text(float elapsed)
	{
		if (elapsed < 0)
			return "";
		if (elapsed < Seconds)
			return ((int)System.MathF.Ceiling(Seconds - elapsed)).ToString();
		return elapsed < Seconds + GoSeconds ? "Kör!" : "";
	}

	/// <summary>0..1 through the current number (for a little pop: big at the start of each number, then settling).</summary>
	public static float Beat(float elapsed) => elapsed < Seconds ? elapsed - (int)elapsed : (elapsed - Seconds) / GoSeconds;

	/// <summary>The camera zoom: from the overview to up close, easing out, done a little before the start.</summary>
	public static float Zoom(float elapsed)
	{
		float t = System.Math.Clamp(elapsed / (Seconds * 0.85f), 0f, 1f);
		float eased = 1 - (1 - t) * (1 - t) * (1 - t);
		return WideZoom + (CloseZoom - WideZoom) * eased;
	}
}
