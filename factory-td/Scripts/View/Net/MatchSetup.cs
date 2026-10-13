using FactoryTD.Net;

namespace FactoryTD.View.Net;

/// <summary>
/// What the start menu decided, read by Game when the match scene loads: an online session (already
/// started) or local play against the bot. Static because it outlives the scene change.
/// </summary>
public static class MatchSetup
{
	/// <summary>The online match, or null for local play.</summary>
	public static MatchSession Session { get; set; }

	public static ENetTransport Transport { get; set; }

	/// <summary>Local play: which map (online the host's choice comes with the session).</summary>
	public static Sim.MapTheme Theme { get; set; }

	/// <summary>Local play: how long the bot's armies wait (BotPlayer armyDelayTicks).</summary>
	public static int BotArmyDelayTicks { get; set; }

	/// <summary>Let a bot play for the local player too (smoke tests: --bot).</summary>
	public static bool LocalBot { get; set; }

	/// <summary>Smoke test: quit after this many steps and write the checksum (0 = play normally).</summary>
	public static int QuitAfterSteps { get; set; }

	public static string ChecksumFile { get; set; } = "";

	/// <summary>Leaves the online match (if any) and forgets it.</summary>
	public static void EndOnline()
	{
		Session?.Leave();
		Transport?.Close();
		Session = null;
		Transport = null;
	}
}
