using System;
using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>
/// A computer opponent. It builds its base as a list of modules (economy, defence, armies), each a set
/// of buildings at fixed spots in its room, plus belts it routes itself. Every half second it:
///   1. rebuilds / builds the first missing building of its plan (plan order = priority), and fixes
///      machine settings (assembler recipes) that are wrong;
///   2. when everything planned is standing, adds the next module whose condition is met
///      (time, or what enemy units it has seen in its room).
/// It only uses the actions a player has (place / configure), so it is deterministic and lockstep-safe.
/// Layouts are written for the left room and mirrored when the bot plays the right one.
/// The modules are in BotModules, belt routing in BotRouting.
/// </summary>
public sealed partial class BotPlayer
{
	private sealed record Step(BuildingType Type, int X, int Y, Direction Facing, ItemType Setting);

	/// <summary>
	/// A piece of the base. It is added when <see cref="Ready"/> holds and everything planned before it
	/// stands, or right away when <see cref="Emergency"/> holds (a threat it has seen): then it jumps the
	/// queue to right after the economy, even in the middle of rebuilding.
	/// </summary>
	private sealed record Module(string Name, Func<World, bool> Ready, Action<World> Build, Func<World, bool> Emergency = null);

	private const int ThinkTicks = 10;           // one action every half second at most
	private const int ObserveTicks = 20;         // look for enemy units once a second
	private const int Minute = World.TicksPerSecond * 60;

	private readonly int _player;
	private readonly int _armyDelayTicks;
	private readonly List<Step> _plan = new();
	private readonly Dictionary<(int X, int Y), Step> _planned = new(); // every tile a planned building covers
	private readonly List<Module> _pending = new();
	private bool _mirror;
	private int _width;
	private int _cooldown;
	private Step _core;
	private Step _blockedStep;     // the step we're saving up for
	private long _blockedSince;
	private readonly HashSet<Step> _gaveUpOn = new(); // waited the full time once: never holds the plan up again

	// Every module's fixed spots are reserved up front, so belts routed early never take a spot a later
	// module needs (or run past a future extractor that would push the wrong items onto them).
	private readonly Dictionary<(int X, int Y), Step> _reserved = new();
	private bool _reserving;
	private int _economyEnd = -1;  // plan index where urgent modules are inserted
	private int _insertAt = -1;

	/// <summary>After waiting this long for one step's resources, build other things meanwhile (no deadlocks).</summary>
	private const int MaxWaitTicks = Minute * 2;

	/// <summary>Enemy units of each kind seen inside the bot's room so far (counted once per sighting second).</summary>
	public int SoldiersSeen { get; private set; }
	public int GolemsSeen { get; private set; }
	public int CarsSeen { get; private set; }

	/// <summary>Names of the modules built so far, for debugging.</summary>
	public List<string> ModulesBuilt { get; } = new();

	public int StepsTotal => _plan.Count;

	/// <summary>The planned buildings in priority order (for tests).</summary>
	internal IEnumerable<(BuildingType Type, int X, int Y, Direction Facing)> PlannedSteps
	{
		get { foreach (var s in _plan) yield return (s.Type, s.X, s.Y, s.Facing); }
	}

	/// <param name="armyDelayTicks">
	/// Extra wait before the bot starts on its army (the later modules are timed from it too).
	/// Default 0: it builds soldiers as soon as its economy and first defences stand. Raise it for an
	/// easier bot.
	/// </param>
	public BotPlayer(int player, int armyDelayTicks = 0)
	{
		_player = player;
		_armyDelayTicks = armyDelayTicks;
		DefineModules();
	}

	/// <summary>
	/// Thinks and acts for one tick; call once per simulation tick, before World.Tick. Commands go to
	/// <paramref name="commands"/> (default: straight into the world). With a delay the bot waits until its
	/// last command has landed before deciding again, so it never repeats an order (a repeated configure
	/// would turn a sorter too far).
	/// </summary>
	public void Tick(World world, ICommandSink commands = null)
	{
		if (world.Winner >= 0)
			return;
		_commands = commands;
		if (world.TickCount < _waitUntil)
			return;
		if (_width == 0)
		{
			_width = world.Map.Width;
			var core = world.GetCore(_player);
			_mirror = core.X > _width / 2;
			// The core is part of the plan so belts can be routed into it (it's always standing).
			_core = AddStep(new Step(BuildingType.Core, core.X, core.Y, core.Facing, ItemType.None));

			_reserving = true;
			foreach (var module in _pending)
				module.Build(world);
			_reserving = false;
		}
		if (world.TickCount % ObserveTicks == 0)
			Observe(world);

		if (_cooldown > 0)
		{
			_cooldown--;
			return;
		}

		// Defences first: they can't wait for the rest of the base to be rebuilt.
		for (int i = 0; i < _pending.Count; i++)
		{
			if (_pending[i].Emergency == null || !_pending[i].Emergency(world))
				continue;
			var module = _pending[i];
			_pending.RemoveAt(i);
			_insertAt = _economyEnd;
			module.Build(world);
			_insertAt = -1;
			ModulesBuilt.Add(module.Name);
			_cooldown = ThinkTicks;
			return;
		}

		switch (MaintainPlan(world))
		{
			case Maintenance.Acted:
				_cooldown = ThinkTicks;
				return;
			case Maintenance.Waiting:
				return;
		}

		// Everything planned is standing: grow.
		for (int i = 0; i < _pending.Count; i++)
		{
			if (!_pending[i].Ready(world))
				continue;
			var module = _pending[i];
			_pending.RemoveAt(i);
			module.Build(world);
			ModulesBuilt.Add(module.Name);
			if (module.Name == EconomyModule)
				_economyEnd = _plan.Count;
			_cooldown = ThinkTicks;
			return;
		}

		// Nothing left on the list: keep growing until the match ends.
		if (Grow(world))
			_cooldown = ThinkTicks;
	}

	private const string EconomyModule = "ekonomi";

	private bool Built(string module) => ModulesBuilt.Contains(module);

	private long _lastFarmTick = -Minute;

	/// <summary>
	/// Eating more than the kitchens bring in, it's been a while since the last new fields, and the
	/// shortage is really crops (if crops are piling up in storage, the kitchens are the bottleneck instead).
	/// </summary>
	private bool NeedsMoreFood(World world)
	{
		var player = world.Players[_player];
		return player.FoodUpkeepPerMinute > player.FoodProducedLastMinute &&
			player.GetCount(ItemType.Crop) < 20 &&
			world.TickCount >= _lastFarmTick + Minute * 3 / 4;
	}

	/// <summary>About to run out (or out already): food jumps the queue.</summary>
	private bool FoodCrisis(World world)
	{
		var player = world.Players[_player];
		return player.Starving || player.GetCount(ItemType.Food) < player.FoodUpkeepPerMinute;
	}

	private bool StorageNearlyFull(World world)
	{
		var player = world.Players[_player];
		foreach (var item in Items.All)
			if (player.GetCount(item) * 100 >= player.Capacity(item) * 85)
				return true;
		return false;
	}

	public int StepsStanding(World world)
	{
		int count = 0;
		foreach (var step in _plan)
			if (IsStanding(world, step))
				count++;
		return count;
	}

	// ---------------------------------------------------------------------------------------------
	// Maintenance: build what's missing, fix settings.

	private enum Maintenance { AllGood, Acted, Waiting }

	private ICommandSink _commands;
	private long _waitUntil;

	private void Send(PlayerCommand command, World world)
	{
		if (_commands == null)
		{
			world.Apply(command);
			return;
		}
		_commands.Send(command);
		_waitUntil = world.TickCount + _commands.DelayTicks;
	}

	private Maintenance MaintainPlan(World world)
	{
		foreach (var step in _plan)
		{
			var existing = world.GetBuilding(step.X, step.Y);
			if (IsStanding(world, step))
			{
				if (step.Setting != ItemType.None && SettingOf(existing) != step.Setting)
				{
					Send(PlayerCommand.Configure(_player, step.X, step.Y), world);
					return Maintenance.Acted;
				}
				continue;
			}

			// A belt that is being turned into a junction: take the old conveyor up first.
			if (step.Type is BuildingType.Junction or BuildingType.Splitter && existing is Conveyor && existing.Owner == _player)
			{
				Send(PlayerCommand.Remove(_player, step.X, step.Y), world);
				return Maintenance.Acted;
			}

			var error = world.CheckPlace(step.Type, step.X, step.Y, _player, step.Facing);
			if (error == PlaceError.NotEnoughResources)
			{
				// The plan order is the priority order, so save up for this step... unless we've
				// waited so long that it's probably waiting on something later in the plan. Two steps
				// short of the same thing would otherwise take turns holding everything up for ever.
				if (_gaveUpOn.Contains(step))
					continue;
				if (_blockedStep != step)
				{
					_blockedStep = step;
					_blockedSince = world.TickCount;
				}
				if (world.TickCount - _blockedSince < MaxWaitTicks)
					return Maintenance.Waiting;
				_gaveUpOn.Add(step);
				continue;
			}
			if (error != PlaceError.None)
				continue; // something else is in the way; try the rest
			Send(PlayerCommand.Place(_player, step.Type, step.X, step.Y, step.Facing), world);
			return Maintenance.Acted;
		}
		return Maintenance.AllGood;
	}

	private bool IsStanding(World world, Step step)
	{
		var b = world.GetBuilding(step.X, step.Y);
		return b != null && b.Type == step.Type && b.X == step.X && b.Y == step.Y && b.Owner == _player;
	}

	private static ItemType SettingOf(Building building) => building switch
	{
		Assembler a => a.Recipe.Output,
		Sorter s => s.Filter,
		_ => ItemType.None,
	};

	/// <summary>Counts enemy units in the bot's room or its half of the hall (i.e. on their way in).</summary>
	private void Observe(World world)
	{
		var home = MapLayout.HomeZone(_player);
		foreach (var unit in world.Units)
		{
			if (unit.Owner == _player || UnitStats.IsWorker(unit.Type) || !world.CanSee(_player, unit))
				continue;
			var zone = world.Map.GetZone(unit.TileX, unit.TileY);
			bool ourHalf = _mirror ? unit.TileX >= _width / 2 : unit.TileX < _width / 2;
			if (zone != home && !(zone == Zone.Hall && ourHalf))
				continue;
			if (unit.Type == UnitType.BrickGolem) GolemsSeen++;
			else if (unit.Type == UnitType.RcCar) CarsSeen++;
			else SoldiersSeen++;
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Plan helpers. Place / Belt take left-room coordinates; the plan stores absolute ones.

	private Step Place(BuildingType type, int x, int y, Direction facing, ItemType setting = ItemType.None)
	{
		var (w, h) = BuildingRules.Size(type, facing);
		int absX = _mirror ? _width - x - w : x;
		var absFacing = _mirror ? Mirror(facing) : facing;
		return AddStep(new Step(type, absX, y, absFacing, setting));
	}

	private void Belt(int x0, int y0, int x1, int y1, Direction facing)
	{
		int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
		for (int x = x0, y = y0; ; x += dx, y += dy)
		{
			Place(BuildingType.Conveyor, x, y, facing);
			if (x == x1 && y == y1)
				break;
		}
	}

	private Step AddStep(Step step)
	{
		var (w, h) = BuildingRules.Size(step.Type, step.Facing);
		var tiles = _reserving ? _reserved : _planned;
		for (int y = step.Y; y < step.Y + h; y++)
			for (int x = step.X; x < step.X + w; x++)
				tiles[(x, y)] = step;
		if (_reserving)
			return step;

		if (_insertAt >= 0)
			_plan.Insert(_insertAt++, step);
		else
			_plan.Add(step);
		return step;
	}

	private static Direction Mirror(Direction d) => d switch
	{
		Direction.East => Direction.West,
		Direction.West => Direction.East,
		_ => d,
	};
}
