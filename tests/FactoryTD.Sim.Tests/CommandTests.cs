using System;
using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

/// <summary>Commands that land <see cref="DelayTicks"/> ticks after they are sent, like over the network.</summary>
internal sealed class DelayedCommands : ICommandSink
{
	private readonly World _world;
	private readonly List<(long Due, PlayerCommand Command)> _queue = new();

	public DelayedCommands(World world, int delay) => (_world, DelayTicks) = (world, delay);

	public int DelayTicks { get; }

	/// <summary>The tick each command was sent on.</summary>
	public List<long> SentAt { get; } = new();

	public void Send(PlayerCommand command)
	{
		SentAt.Add(_world.TickCount);
		_queue.Add((_world.TickCount + DelayTicks, command));
	}

	/// <summary>Applies what is due, then steps the world.</summary>
	public void Tick()
	{
		foreach (var (_, command) in _queue.Where(q => q.Due <= _world.TickCount).ToList())
			_world.Apply(command);
		_queue.RemoveAll(q => q.Due <= _world.TickCount);
		_world.Tick();
	}
}

public class CommandTests
{
	public static IEnumerable<object[]> Every() =>
		from kind in Enum.GetValues<CommandKind>()
		from type in Enum.GetValues<BuildingType>()
		from facing in Enum.GetValues<Direction>()
		select new object[] { new PlayerCommand(kind, 1, 119, -3, type, facing) };

	[Fact]
	public void Codec_RoundTrips_EveryKindTypeAndFacing()
	{
		var bytes = new byte[PlayerCommand.Size];
		foreach (var row in Every())
		{
			var command = (PlayerCommand)row[0];
			command.Write(bytes);
			Assert.Equal(command, PlayerCommand.Read(bytes));
		}
	}

	[Theory]
	[InlineData(new byte[] { 9, 0, 0, 0, 0, 0, 0, 0 })]   // unknown kind
	[InlineData(new byte[] { 0, 0, 250, 0, 0, 0, 0, 0 })] // unknown building
	[InlineData(new byte[] { 0, 0, 0, 7, 0, 0, 0, 0 })]   // unknown facing
	[InlineData(new byte[] { 0, 0, 0 })]                  // too short
	public void Codec_RejectsGarbage(byte[] bytes) => Assert.Null(PlayerCommand.Read(bytes));

	[Fact]
	public void Apply_DoesWhatTheDirectCallsDo_AndIgnoresBadPlayersAndTiles()
	{
		var s = Scenario.Match().NoWorkers().Instant().Rich();
		var w = s.World;
		Assert.True(w.Apply(PlayerCommand.Place(0, BuildingType.Sorter, 20, 20, Direction.East)));
		var sorter = (Sorter)w.GetBuilding(20, 20);
		var filter = sorter.Filter;
		Assert.True(w.Apply(PlayerCommand.Configure(0, 20, 20)));
		Assert.NotEqual(filter, sorter.Filter);
		Assert.False(w.Apply(PlayerCommand.Remove(1, 20, 20)));   // not theirs
		Assert.True(w.Apply(PlayerCommand.Remove(0, 20, 20)));
		Assert.Null(w.GetBuilding(20, 20));
		Assert.False(w.Apply(PlayerCommand.Place(5, BuildingType.Conveyor, 20, 20, Direction.East)));
		Assert.False(w.Apply(PlayerCommand.Place(0, BuildingType.Conveyor, -1, 20, Direction.East)));
		Assert.False(w.Apply(PlayerCommand.Place(0, BuildingType.Conveyor, 20, 9999, Direction.East)));
	}

	[Fact]
	public void Bot_WithDelayedCommands_BuildsAsMuch_NeverOrdersBeforeTheLastOrderLanded()
	{
		static (Scenario, DelayedCommands) Run(int delay)
		{
			var s = Scenario.Match(obstacles: true).Rich(5000).Give(ItemType.Food, 500, 0);
			var bot = new BotPlayer(0);
			var sink = new DelayedCommands(s.World, delay);
			for (int t = 0; t < 20 * 90; t++)
			{
				bot.Tick(s.World, sink);
				sink.Tick();
			}
			return (s, sink);
		}
		var (direct, _) = Run(0);
		var (late, sink) = Run(4);
		int Mine(Scenario s) => s.World.Buildings.Count(b => b.Owner == 0);
		Assert.True(Mine(late) * 10 >= Mine(direct) * 8, $"with delay {Mine(late)} buildings, without {Mine(direct)}");
		Assert.All(sink.SentAt.Zip(sink.SentAt.Skip(1)), pair => Assert.True(pair.Second - pair.First >= 4));
	}
}
