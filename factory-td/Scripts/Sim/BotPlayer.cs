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
/// </summary>
public sealed class BotPlayer
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

	/// <summary>Call once per simulation tick, before World.Tick.</summary>
	public void Tick(World world)
	{
		if (world.Winner >= 0)
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

	private Maintenance MaintainPlan(World world)
	{
		foreach (var step in _plan)
		{
			var existing = world.GetBuilding(step.X, step.Y);
			if (IsStanding(world, step))
			{
				if (step.Setting != ItemType.None && SettingOf(existing) != step.Setting)
				{
					world.TryConfigure(step.X, step.Y, _player);
					return Maintenance.Acted;
				}
				continue;
			}

			// A belt that is being turned into a junction: take the old conveyor up first.
			if (step.Type == BuildingType.Junction && existing is Conveyor && existing.Owner == _player)
			{
				world.TryRemove(step.X, step.Y, _player);
				return Maintenance.Acted;
			}

			var error = world.CheckPlace(step.Type, step.X, step.Y, _player);
			if (error == PlaceError.NotEnoughResources)
			{
				// The plan order is the priority order, so save up for this step... unless we've
				// waited so long that it's probably waiting on something later in the plan.
				if (_blockedStep != step)
				{
					_blockedStep = step;
					_blockedSince = world.TickCount;
				}
				if (world.TickCount - _blockedSince < MaxWaitTicks)
					return Maintenance.Waiting;
				continue;
			}
			if (error != PlaceError.None)
				continue; // something else is in the way; try the rest
			world.TryPlace(step.Type, step.X, step.Y, step.Facing, _player);
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
			if (unit.Owner == _player || UnitStats.IsWorker(unit.Type))
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
	// The plan. Coordinates are for the left room: core (6..7, 30..31), bricks (10..13, 22..25),
	// plastic (10..13, 36..39), batteries (40..42, 46..48), door at x = 61, y = 29..32.

	private void DefineModules()
	{
		_pending.Add(new Module(EconomyModule, _ => true, _ =>
		{
			Place(BuildingType.BrickExtractor, 13, 25, Direction.South);
			Belt(13, 26, 13, 29, Direction.South);
			Belt(13, 30, 8, 30, Direction.West);
			Place(BuildingType.PlasticExtractor, 13, 36, Direction.North);
			Belt(13, 35, 13, 32, Direction.North);
			Belt(13, 31, 8, 31, Direction.West);
		}));

		_pending.Add(new Module("fler utvinnare", _ => true, _ =>
		{
			Belt(10, 26, 12, 26, Direction.East);
			Place(BuildingType.BrickExtractor, 12, 25, Direction.South);
			Place(BuildingType.BrickExtractor, 11, 25, Direction.South);
			Place(BuildingType.BrickExtractor, 10, 25, Direction.South);
			Belt(10, 35, 12, 35, Direction.East);
			Place(BuildingType.PlasticExtractor, 12, 36, Direction.North);
			Place(BuildingType.PlasticExtractor, 11, 36, Direction.North);
			Place(BuildingType.PlasticExtractor, 10, 36, Direction.North);
		}));

		// Food: a farmhouse (farmers cost bricks, so one brick extractor feeds it), a kitchen pointing
		// straight into the toybox and four fields next to it, west of the core. Farmers carry the
		// crops to the kitchen; the kitchen's food goes straight into the toybox.
		_pending.Add(new Module("mat", _ => true, w =>
		{
			var kitchen = Place(BuildingType.Kitchen, 6, 32, Direction.North);
			for (int x = 2; x <= 5; x++)
				Place(BuildingType.CropField, x, 34, Direction.East);
			var farmhouse = Place(BuildingType.Farmhouse, 4, 24, Direction.East);
			var bricks = Place(BuildingType.BrickExtractor, 10, 23, Direction.West);
			Route(w, bricks, farmhouse);
			_lastFarmTick = w.TickCount;
		},
		Emergency: FoodCrisis));

		// More fields, a row of four at a time, while the army eats more than the kitchen brings in.
		for (int row = 35; row <= 40; row++)
		{
			int y = row;
			_pending.Add(new Module($"åkrar {row - 34}",
				w => Built("mat") && NeedsMoreFood(w),
				w =>
				{
					for (int x = 2; x <= 5; x++)
						Place(BuildingType.CropField, x, y, Direction.East);
					// Every other row of fields gets another kitchen pointing into the toybox (one cooks ~20 food/min).
					if (y == 36) Place(BuildingType.Kitchen, 7, 32, Direction.North);
					if (y == 38) Place(BuildingType.Kitchen, 5, 31, Direction.East);
					if (y == 40) Place(BuildingType.Kitchen, 5, 30, Direction.East);
					_lastFarmTick = w.TickCount;
				},
				Emergency: w => Built("mat") && FoodCrisis(w) && w.TickCount >= _lastFarmTick + Minute / 2));
		}

		// Crops piling up in storage means the kitchens can't keep up: cook more (north side of the toybox).
		bool CropsPilingUp(World w) => Built("mat") && w.Players[_player].GetCount(ItemType.Crop) >= 40;
		_pending.Add(new Module("fler kök 1", CropsPilingUp, _ => Place(BuildingType.Kitchen, 6, 29, Direction.South), Emergency: CropsPilingUp));
		_pending.Add(new Module("fler kök 2", w => Built("fler kök 1") && CropsPilingUp(w),
			_ => Place(BuildingType.Kitchen, 7, 29, Direction.South),
			Emergency: w => Built("fler kök 1") && CropsPilingUp(w) && w.TickCount >= _lastFarmTick + Minute / 2));

		// Warehouses when the stock is nearly full (they add room even without a belt of their own).
		_pending.Add(new Module("lager 1", StorageNearlyFull, _ => Place(BuildingType.Warehouse, 2, 27, Direction.East)));
		_pending.Add(new Module("lager 2", w => Built("lager 1") && StorageNearlyFull(w),
			_ => Place(BuildingType.Warehouse, 2, 21, Direction.East)));

		// More builders: a toolbox fed bricks and plastic by short belts from its own extractors.
		_pending.Add(new Module("verktygslåda", _ => true, w =>
		{
			var toolbox = Place(BuildingType.Toolbox, 16, 29, Direction.East);
			var bricks = Place(BuildingType.BrickExtractor, 12, 22, Direction.North);
			var plastic = Place(BuildingType.PlasticExtractor, 10, 38, Direction.West);
			Route(w, bricks, toolbox);
			Route(w, plastic, toolbox);
		}));

		// Power: a charger fed by a battery belt from the battery patch, and pylons over the base so every
		// factory, assembler and tower is on the grid (the toybox links the west pylons, the rest link
		// in a chain). Before the towers: a tower without power doesn't shoot.
		_pending.Add(new Module("ström", _ => true, w =>
		{
			var charger = Place(BuildingType.BatteryCharger, 18, 31, Direction.East);
			Place(BuildingType.Pylon, 8, 26, Direction.East);   // core defence north, links to the toybox
			Place(BuildingType.Pylon, 8, 36, Direction.East);   // core defence south
			Place(BuildingType.Pylon, 15, 26, Direction.East);  // golem workshop
			Place(BuildingType.Pylon, 15, 34, Direction.East);  // soldier factory
			Place(BuildingType.Pylon, 21, 30, Direction.East);  // front towers, charger
			var battery = Place(BuildingType.BatteryExtractor, 40, 47, Direction.West);
			Route(w, battery, charger);
		},
		Emergency: _ => SoldiersSeen + GolemsSeen + CarsSeen >= 1));

		// The front: attackers come in through the door (x = 61) and head west for the core, shooting
		// whatever buildings they meet first. So the towers stand in front of the base, on the door side
		// of the factories (x = 19), where attackers run into them before they reach anything else, but
		// close enough to home that short belts from the base's own extractors can feed them.
		// Built right after the economy, or at once when attackers are on their way in.
		_pending.Add(new Module("frontförsvar",
			_ => true,
			w =>
			{
				var catapult = Place(BuildingType.Catapult, 19, 27, Direction.East);
				var bricks = Place(BuildingType.BrickExtractor, 10, 22, Direction.West);
				var foam = Place(BuildingType.FoamTower, 19, 33, Direction.East);
				var plastic = Place(BuildingType.PlasticExtractor, 10, 39, Direction.West);
				Route(w, bricks, catapult);
				Route(w, plastic, foam);
			},
			Emergency: _ => SoldiersSeen + GolemsSeen + CarsSeen >= 1));

		// Last line: a catapult and foam tower by the core, fed straight by extractors, for whatever gets
		// through the front. At once when attackers keep coming, or anyway after six minutes.
		_pending.Add(new Module("kärnförsvar",
			w => w.TickCount >= 6 * Minute,
			w =>
			{
				Place(BuildingType.PlasticExtractor, 10, 37, Direction.West);
				Place(BuildingType.FoamTower, 9, 37, Direction.East);
				Place(BuildingType.BrickExtractor, 10, 24, Direction.West);
				Place(BuildingType.Catapult, 9, 24, Direction.East);
			},
			Emergency: _ => SoldiersSeen + GolemsSeen + CarsSeen >= 10));

		// Soldiers: melter -> assembler (springs) -> factory, melter -> belt -> factory (plastic).
		_pending.Add(new Module("soldater", w => w.TickCount >= _armyDelayTicks && !FoodCrisis(w), _ =>
		{
			Place(BuildingType.SoldierFactory, 15, 37, Direction.East);
			Place(BuildingType.Assembler, 14, 38, Direction.East, ItemType.Spring);
			Place(BuildingType.PlasticExtractor, 13, 38, Direction.East);
			Place(BuildingType.Conveyor, 14, 37, Direction.East);
			Place(BuildingType.PlasticExtractor, 13, 37, Direction.East);
		}));

		// Batteries into the core: towers and car factories cost batteries to build.
		_pending.Add(new Module("batterier",
			_ => Built("soldater"),
			w =>
			{
				var battery = Place(BuildingType.BatteryExtractor, 41, 46, Direction.North);
				Route(w, battery, _core);
			},
			Emergency: _ => GolemsSeen > 0 || CarsSeen > 0));

		// Golems and cars run flat away from the grid: a chain of pylons through the door and along the
		// hall (every 8 tiles, the cord range) lets them charge on the way to the enemy.
		_pending.Add(new Module("frammaster", w => Built("soldater") && Built("ström") && w.TickCount >= _armyDelayTicks + 2 * Minute, _ =>
		{
			for (int x = 29; x <= 53; x += 8)
				Place(BuildingType.Pylon, x, 30, Direction.East);
			for (int x = 60; x <= 92; x += 8)
				Place(BuildingType.Pylon, x, 30, Direction.East);
		}));

		// Golems by the brick patch: bricks -> gear assembler -> workshop, bricks -> belt -> workshop,
		// and plastic routed from the plastic patch into the gear assembler.
		_pending.Add(new Module("golems", w => Built("soldater") && !FoodCrisis(w) && w.TickCount >= _armyDelayTicks + 3 * Minute, w =>
		{
			Place(BuildingType.GolemWorkshop, 15, 22, Direction.East);
			var gears = Place(BuildingType.Assembler, 14, 23, Direction.East, ItemType.Gear);
			Place(BuildingType.BrickExtractor, 13, 23, Direction.East);
			Place(BuildingType.Conveyor, 14, 22, Direction.East);
			Place(BuildingType.BrickExtractor, 13, 22, Direction.East);
			var plastic = Place(BuildingType.PlasticExtractor, 13, 39, Direction.East);
			Route(w, plastic, gears);
		}));

		// Batteries into lasers once golems show up (or anyway, a bit later).
		_pending.Add(new Module("lasertorn",
			w => Built("batterier") && w.TickCount >= _armyDelayTicks + 4 * Minute,
			w =>
			{
				var laser = Place(BuildingType.LaserTower, 20, 34, Direction.East);
				var battery = Place(BuildingType.BatteryExtractor, 40, 46, Direction.West);
				Route(w, battery, laser);
			},
			Emergency: _ => Built("batterier") && GolemsSeen > 0));

		// Radio cars by the battery patch: batteries -> circuit assembler -> factory, batteries -> belt ->
		// factory, and plastic/bricks routed in for circuits and gears.
		_pending.Add(new Module("radiobilar", w => Built("batterier") && !FoodCrisis(w) && w.TickCount >= _armyDelayTicks + 5 * Minute, w =>
		{
			Place(BuildingType.CarFactory, 44, 46, Direction.East);
			var circuits = Place(BuildingType.Assembler, 43, 46, Direction.East, ItemType.CircuitBoard);
			Place(BuildingType.BatteryExtractor, 42, 46, Direction.East);
			Place(BuildingType.Conveyor, 43, 47, Direction.East);
			Place(BuildingType.BatteryExtractor, 42, 47, Direction.East);
			// Its own little grid: a charger filled straight by the extractor next to it.
			Place(BuildingType.BatteryExtractor, 42, 48, Direction.East);
			Place(BuildingType.BatteryCharger, 43, 48, Direction.East);
			Place(BuildingType.Pylon, 45, 45, Direction.East);
			var gears = Place(BuildingType.Assembler, 46, 46, Direction.West, ItemType.Gear);
			var plasticA = Place(BuildingType.PlasticExtractor, 12, 39, Direction.South);
			var plasticB = Place(BuildingType.PlasticExtractor, 11, 39, Direction.South);
			var bricks = Place(BuildingType.BrickExtractor, 11, 22, Direction.North);
			Route(w, plasticA, circuits);
			Route(w, plasticB, gears);
			Route(w, bricks, gears);
		}));

		// Water pistols once radio cars show up.
		_pending.Add(new Module("vattenpistol",
			_ => false,
			w =>
			{
				var water = Place(BuildingType.WaterTower, 22, 27, Direction.East);
				var battery = Place(BuildingType.BatteryExtractor, 40, 48, Direction.West);
				Route(w, battery, water);
			},
			Emergency: _ => Built("batterier") && CarsSeen > 0));

		// A second laser if golems keep coming.
		_pending.Add(new Module("fler lasertorn",
			_ => false,
			w =>
			{
				var laser = Place(BuildingType.LaserTower, 24, 33, Direction.East);
				var battery = Place(BuildingType.BatteryExtractor, 41, 48, Direction.South);
				Route(w, battery, laser);
			},
			Emergency: _ => Built("lasertorn") && GolemsSeen >= 4));
	}

	// ---------------------------------------------------------------------------------------------
	// Plan helpers. Place / Belt take left-room coordinates; the plan stores absolute ones.

	private Step Place(BuildingType type, int x, int y, Direction facing, ItemType setting = ItemType.None)
	{
		var (w, h) = BuildingRules.Size(type);
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
		var (w, h) = BuildingRules.Size(step.Type);
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

	// ---------------------------------------------------------------------------------------------
	// Belt routing (absolute coordinates).

	private static readonly Direction[] Directions = { Direction.East, Direction.South, Direction.West, Direction.North };

	/// <summary>
	/// Plans a belt from a free tile next to <paramref name="source"/> to a tile next to
	/// <paramref name="target"/>, ending pointing into the target. Breadth-first, so the shortest path.
	/// It never runs over deposits (kept free for extractors) and never passes a tile that some other
	/// building would push items into, so the belt carries only what the source makes and can't jam.
	/// If there is no way through, nothing is planned.
	/// </summary>
	private void Route(World world, Step source, Step target)
	{
		if (_reserving)
			return; // routes are worked out when the module is actually built
		var start = (-1, -1);
		foreach (var d in Directions)
		{
			var tile = (source.X + d.DX(), source.Y + d.DY());
			if (IsRoutable(world, tile, source, target))
			{
				start = tile;
				break;
			}
		}
		if (start.Item1 < 0)
			return;

		var previous = new Dictionary<(int, int), (int, int)> { [start] = start };
		var crossedAt = new Dictionary<(int, int), (int, int)>(); // tile after a crossing -> the crossing tile
		var queue = new Queue<(int X, int Y)>();
		queue.Enqueue(start);
		(int X, int Y) goal = (-1, -1);
		Direction into = Direction.East;

		while (queue.Count > 0 && goal.X < 0)
		{
			var tile = queue.Dequeue();
			foreach (var d in Directions)
			{
				var next = (tile.X + d.DX(), tile.Y + d.DY());
				if (_planned.TryGetValue(next, out var step) && step == target)
				{
					goal = tile;
					into = d;
					break;
				}
			}
			if (goal.X >= 0)
				break;

			foreach (var d in Directions)
			{
				var next = (tile.X + d.DX(), tile.Y + d.DY());
				if (!previous.ContainsKey(next) && IsRoutable(world, next, source, target))
				{
					previous[next] = tile;
					queue.Enqueue(next);
					continue;
				}

				// Can't go there, but if it's one of our straight belts running across, cross it with a
				// junction and carry on straight out the other side.
				var beyond = (next.Item1 + d.DX(), next.Item2 + d.DY());
				if (CanCross(next, d) && !previous.ContainsKey(beyond) && IsRoutable(world, beyond, source, target))
				{
					previous[beyond] = tile;
					crossedAt[beyond] = next;
					queue.Enqueue(beyond);
				}
			}
		}
		if (goal.X < 0)
			return;

		// Walk back from the goal, putting the crossings back in between.
		var path = new List<(int X, int Y)> { goal };
		var junctions = new HashSet<(int, int)>();
		while (path[^1] != start)
		{
			var current = path[^1];
			if (crossedAt.TryGetValue(current, out var crossing))
			{
				path.Add(crossing);
				junctions.Add(crossing);
			}
			path.Add(previous[current]);
		}
		path.Reverse();

		// Each conveyor points at the next tile; crossings become junctions.
		for (int i = 0; i < path.Count; i++)
		{
			if (junctions.Contains(path[i]))
			{
				var old = _planned[path[i]];
				var junction = new Step(BuildingType.Junction, old.X, old.Y, old.Facing, ItemType.None);
				_plan[_plan.IndexOf(old)] = junction;
				_planned[path[i]] = junction;
				continue;
			}
			var facing = i + 1 < path.Count ? DirectionBetween(path[i], path[i + 1]) : into;
			AddStep(new Step(BuildingType.Conveyor, path[i].X, path[i].Y, facing, ItemType.None));
		}
	}

	/// <summary>
	/// Whether a new belt moving in <paramref name="moving"/> can cross the tile with a junction:
	/// it must be one of our planned conveyors running across, in the middle of a straight line.
	/// </summary>
	private bool CanCross((int X, int Y) tile, Direction moving)
	{
		if (!_planned.TryGetValue(tile, out var step) || step.Type != BuildingType.Conveyor)
			return false;
		if (step.Facing == moving || step.Facing == moving.Opposite())
			return false;
		// The item before it on its line must come in straight from behind, or the junction would break a turn.
		var behind = (tile.X - step.Facing.DX(), tile.Y - step.Facing.DY());
		return _planned.TryGetValue(behind, out var feeder) &&
			(feeder.Type == BuildingType.Junction || Pushes(feeder, step.Facing, behind, tile));
	}

	private bool IsRoutable(World world, (int X, int Y) tile, Step source, Step target)
	{
		var (x, y) = tile;
		if (_planned.ContainsKey(tile) || _reserved.ContainsKey(tile))
			return false;
		if (world.CheckLocation(BuildingType.Conveyor, x, y, _player) != PlaceError.None)
			return false;
		if (world.Map.GetResource(x, y) != ResourceType.None)
			return false;

		// Nothing but the source may push into this tile, now or once the rest of the plan is built.
		foreach (var d in Directions)
		{
			var neighbour = (x - d.DX(), y - d.DY()); // the tile an item moving in d would come from
			if (!_planned.TryGetValue(neighbour, out var step) && !_reserved.TryGetValue(neighbour, out step))
			{
				var existing = world.GetBuilding(neighbour.Item1, neighbour.Item2);
				if (existing == null || existing.Owner != _player)
					continue;
				step = new Step(existing.Type, existing.X, existing.Y, existing.Facing, ItemType.None);
			}
			if (step == source || step == target || (step.X == source.X && step.Y == source.Y))
				continue;
			if (Pushes(step, d, neighbour, tile))
				return false;
		}
		return true;
	}

	/// <summary>Whether a building would hand items moving in <paramref name="moving"/> into the tile next to it.</summary>
	private static bool Pushes(Step step, Direction moving, (int X, int Y) from, (int X, int Y) to) => step.Type switch
	{
		BuildingType.BrickExtractor or BuildingType.PlasticExtractor or BuildingType.BatteryExtractor => true,
		BuildingType.Splitter or BuildingType.Sorter => true,
		BuildingType.Conveyor or BuildingType.Assembler => step.Facing == moving,
		// A junction only passes items along its two lines, which already run through their tiles.
		BuildingType.Junction => false,
		_ => false,
	};

	private static Direction DirectionBetween((int X, int Y) a, (int X, int Y) b) =>
		b.X > a.X ? Direction.East : b.X < a.X ? Direction.West : b.Y > a.Y ? Direction.South : Direction.North;
}
