using System;
using FactoryTD.Sim;
using Xunit;

namespace FactoryTD.Sim.Tests.Support;

/// <summary>Moving time forward in tests. All times are in simulation ticks or seconds (20 ticks per second).</summary>
public static class WorldRunner
{
	public const int TicksPerSecond = World.TicksPerSecond;
	public const int TicksPerMinute = World.TicksPerSecond * 60;

	public static World Ticks(this World world, int ticks, params BotPlayer[] bots)
	{
		for (int i = 0; i < ticks && world.Winner < 0; i++)
		{
			foreach (var bot in bots)
				bot.Tick(world);
			world.Tick();
		}
		return world;
	}

	public static World Seconds(this World world, double seconds, params BotPlayer[] bots) =>
		world.Ticks((int)Math.Round(seconds * TicksPerSecond), bots);

	public static World Minutes(this World world, double minutes, params BotPlayer[] bots) =>
		world.Seconds(minutes * 60, bots);

	/// <summary>
	/// Ticks until <paramref name="done"/> holds and returns how many ticks that took; fails the test
	/// (saying what it was waiting for) if it doesn't happen within <paramref name="maxSeconds"/>.
	/// </summary>
	public static int Until(this World world, Func<bool> done, double maxSeconds, string what, params BotPlayer[] bots)
	{
		int limit = (int)(maxSeconds * TicksPerSecond);
		for (int ticks = 0; ticks <= limit; ticks++)
		{
			if (done())
				return ticks;
			foreach (var bot in bots)
				bot.Tick(world);
			world.Tick();
		}
		Assert.Fail($"Timed out after {maxSeconds}s waiting for: {what}");
		return -1;
	}
}
