using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FactoryTD.Sim.Tests;

/// <summary>
/// Online play runs the simulation on two machines (maybe different OS/CPU) and only sends commands, so the
/// sim must compute bit-for-bit the same everywhere. The golden checksum proves it on this machine; this
/// guard keeps out what could differ elsewhere: floating point, clocks, randomness, hash codes, threads.
/// (Dictionary/HashSet are fine: they enumerate in insertion order, not by hash. Integer math is the same on
/// x64 and ARM. Square roots go through IntMath.)
/// </summary>
public class DeterminismGuardTests
{
	private static readonly (string Pattern, string Why)[] Forbidden =
	{
		(@"\b(float|double|decimal|Half)\b", "floating point can round differently between machines; use ints (fixed point)"),
		(@"\b(Math|MathF)\.(Sqrt|Pow|Exp|Log\w*|Sin|Cos|Tan|Atan2?|Asin|Acos|Round|Floor|Ceiling|Truncate)\b", "floating point math; use IntMath"),
		(@"\b(Random|RandomNumberGenerator|Guid)\b", "randomness differs per machine; seed an xorshift from the match seed"),
		(@"\b(DateTime|DateTimeOffset|Stopwatch)\b|\bEnvironment\.TickCount", "clocks differ per machine; use World.TickCount"),
		(@"\.GetHashCode\(\)|\bHashCode\.", "hash codes are randomised per process"),
		(@"\b(Parallel|Thread|ThreadPool|Task)\.", "threads finish in any order"),
	};

	private static string SimFolder()
	{
		for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
		{
			var sim = Path.Combine(dir.FullName, "factory-td", "Scripts", "Sim");
			if (Directory.Exists(sim))
				return sim;
		}
		throw new DirectoryNotFoundException("factory-td/Scripts/Sim not found above the test folder");
	}

	[Fact]
	public void Sim_UsesNothingThatDiffersBetweenMachines()
	{
		var problems = (
			from file in Directory.GetFiles(SimFolder(), "*.cs", SearchOption.AllDirectories)
			from line in File.ReadAllLines(file).Select((text, index) => (Text: StripComment(text), Number: index + 1))
			from rule in Forbidden
			where Regex.IsMatch(line.Text, rule.Pattern)
			select $"{Path.GetFileName(file)}:{line.Number} {line.Text.Trim()}  -> {rule.Why}").ToList();
		Assert.True(problems.Count == 0, string.Join("\n", problems.Take(10)));
	}

	[Theory]
	[InlineData("	float speed = 1.5f;")]
	[InlineData("	var r = new System.Random(4);")]
	[InlineData("	long now = DateTime.Now.Ticks;")]
	[InlineData("	int h = name.GetHashCode();")]
	[InlineData("	int d = (int)Math.Sqrt(x);")]
	[InlineData("	int t = Environment.TickCount;")]
	public void Guard_CatchesTheThingsItIsFor(string line) => Assert.True(Breaks(line), $"not caught: {line}");

	[Theory]
	[InlineData("	// a float would be wrong here")]
	[InlineData("	int d = IntMath.Sqrt(x * x); /// <summary>no double</summary>")]
	[InlineData("	int m = Math.Max(a, b);")]
	[InlineData("	TickCount++;")]
	public void Guard_LeavesCommentsAndIntegerMathAlone(string line) => Assert.False(Breaks(line), $"wrongly caught: {line}");

	private static bool Breaks(string line) => Forbidden.Any(rule => Regex.IsMatch(StripComment(line), rule.Pattern));

	private static string StripComment(string line)
	{
		int at = line.IndexOf("//", StringComparison.Ordinal);
		return at < 0 ? line : line[..at];
	}
}
