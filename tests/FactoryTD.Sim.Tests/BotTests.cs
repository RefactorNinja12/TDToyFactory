using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

/// <summary>
/// Bot runs shared by the read-only tests (simulating minutes of bot play is the slow part):
/// run once per test class, never modified.
/// </summary>
public sealed class BotRuns
{
	private readonly System.Lazy<(Scenario, BotPlayer)> _p0 = new(() => BotTests.RichBot(0, 120));
	private readonly System.Lazy<(Scenario, BotPlayer)> _p1 = new(() => BotTests.RichBot(1, 240));

	/// <summary>Player 0 (left room) after 2 minutes.</summary>
	public (Scenario, BotPlayer) Left => _p0.Value;

	/// <summary>Player 1 (right room, mirrored plan) after 4 minutes.</summary>
	public (Scenario, BotPlayer) Right => _p1.Value;
}

public class BotTests : IClassFixture<BotRuns>
{
	private readonly BotRuns _runs;
	public BotTests(BotRuns runs) => _runs = runs;

	/// <summary>A bot with plenty of resources, run until its plan has grown for a while.</summary>
	internal static (Scenario, BotPlayer) RichBot(int player, int seconds)
	{
		var s = Scenario.Match().RealPower().RealFog().Rich(5000).Give(ItemType.Food, 500, player);
		var bot = new BotPlayer(player);
		s.World.Seconds(seconds, bot);
		return (s, bot);
	}

	private static bool IsBelt(BuildingType t) => t == BuildingType.Conveyor || t == BuildingType.Junction;

	[Fact]
	public void BeltsNeverCoverDeposits()
	{
		var (s, bot) = _runs.Left;
		var onDeposit = bot.PlannedSteps
			.Where(p => IsBelt(p.Type) && s.World.Map.GetResource(p.X, p.Y) != ResourceType.None)
			.ToList();
		Assert.True(onDeposit.Count == 0, "belts on deposits: " + string.Join(" ", onDeposit.Select(p => $"({p.X},{p.Y})")));
	}

	[Fact]
	public void PlannedTiles_DontOverlap()
	{
		var (_, bot) = _runs.Left;
		var seen = new Dictionary<(int, int), BuildingType>();
		foreach (var p in bot.PlannedSteps)
		{
			var (w, h) = BuildingRules.Size(p.Type);
			for (int y = p.Y; y < p.Y + h; y++)
				for (int x = p.X; x < p.X + w; x++)
				{
					if (seen.TryGetValue((x, y), out var other))
						Assert.True(p.Type == BuildingType.Junction && other == BuildingType.Conveyor,
							$"{p.Type} and {other} both planned on ({x},{y})");
					seen[(x, y)] = p.Type;
				}
		}
	}

	[Fact]
	public void Player1_PlanIsTheMirrorOfPlayer0()
	{
		var (s0, bot0) = RichBot(0, 30);
		var (s1, bot1) = RichBot(1, 30);
		int width = s0.World.Map.Width;
		// Module buildings sit on mirrored spots; belts are pathfound per side and may differ.
		var left = bot0.PlannedSteps.Where(p => !IsBelt(p.Type)).ToList();
		var right = bot1.PlannedSteps.Where(p => !IsBelt(p.Type)).ToList();
		Assert.Contains(left, p => BuildingRules.Size(p.Type).Item1 > 1);
		Assert.Equal(left.Count, right.Count);
		for (int i = 0; i < left.Count; i++)
		{
			var (w, _) = BuildingRules.Size(left[i].Type);
			Assert.Equal(left[i].Type, right[i].Type);
			Assert.Equal(left[i].Y, right[i].Y);
			Assert.Equal(width - left[i].X - w, right[i].X);
		}
	}

	[Fact]
	public void BuildsItsPlan_AndRebuildsWhatIsDestroyed()
	{
		var (s, bot) = RichBot(0, 90);
		Assert.True(bot.StepsStanding(s.World) >= 10, $"only {bot.StepsStanding(s.World)} steps standing");
		var extractor = s.World.Buildings.First(b => b.Owner == 0 && b is Extractor);
		var (x, y) = (extractor.X, extractor.Y);
		Assert.True(s.World.TryRemove(x, y, 0));
		s.World.Until(() => s.World.GetBuilding(x, y) is Extractor, 30, "the extractor rebuilt", bot);
	}

	[Fact]
	public void OnlyBuildsInsideItsZones()
	{
		var (s, _) = _runs.Right;
		foreach (var b in s.World.Buildings.Where(b => b.Owner == 1))
			Assert.NotEqual(Zone.LeftRoom, s.World.Map.GetZone(b.X, b.Y));
	}

	private static bool NeedsPower(Building b) =>
		b is Tower or Assembler || (b is UnitFactory f && !UnitStats.IsWorker(f.Produces));

	[Fact]
	public void PlansAPowerGrid()
	{
		var (_, bot) = _runs.Left;
		var steps = bot.PlannedSteps.ToList();
		Assert.Contains(steps, p => p.Type == BuildingType.BatteryCharger);
		Assert.True(steps.Count(p => p.Type == BuildingType.Pylon) >= 4, "pylons over the base");
	}

	[Fact]
	public void AfterFourMinutes_ConsumersAreOnACharged_Grid_ThatReachesIntoTheHall()
	{
		var (s, bot) = _runs.Right;
		var consumers = s.World.Buildings.Where(b => b.Owner == 1 && b.IsBuilt && NeedsPower(b)).ToList();
		Assert.NotEmpty(consumers);
		var outside = consumers.Where(b => s.World.NetworkOf(b) == null).Select(b => $"{b.Type}({b.X},{b.Y})").ToList();
		Assert.True(outside.Count == 0, "outside the grid: " + string.Join(" ", outside));
		Assert.True(s.World.Power.EnergyStored(1) > 0, "no energy stored");
		Assert.Contains(bot.PlannedSteps, p => p.Type == BuildingType.Pylon && s.World.Map.GetZone(p.X, p.Y) == Zone.Hall);
	}

	[Fact]
	public void BuildsATentEarly_AndFindsTheBatteries()
	{
		var (left, leftBot) = _runs.Left;
		Assert.Contains(leftBot.PlannedSteps, p => p.Type == BuildingType.Tent);
		Assert.True(left.World.CountUnits(0, UnitType.Scout) >= 1, "no scout after 2 minutes");
		var (right, _) = _runs.Right;
		Assert.True(right.World.CountBuildings(1, BuildingType.BatteryExtractor) >= 1, "no battery extractor after 4 minutes");
	}
}
