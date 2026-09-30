using System;
using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>
/// Game state: the map, the players and everything built on it. Pure C#.
/// Buildings can cover several tiles; every tile they cover points to the same Building.
/// </summary>
public sealed class World
{
	/// <summary>Fixed simulation rate. Everything in the sim counts in ticks, never in seconds.</summary>
	public const int TicksPerSecond = 20;

	private readonly Building[] _grid;
	private readonly List<Building> _buildings = new();
	private readonly PlayerState[] _players;
	private readonly Core[] _cores;
	private readonly List<Unit> _units = new();
	private readonly List<Projectile> _projectiles = new();

	// _fields[p] leads player p's units to the enemy core. Rebuilt lazily when buildings change.
	private readonly FlowField[] _fields;
	private bool _fieldsDirty = true;
	private readonly PowerGrid _power;
	private readonly FlowField[] _powerFields; // per player: towards the nearest tile its grid powers
	private readonly bool[] _powerFieldDirty;    // built only when a unit needs it (most ticks nobody does)
	private int _nextUnitId;

	public MapLayout Map { get; }
	public long TickCount { get; private set; }

	/// <summary>In placement order, so iteration is deterministic.</summary>
	public IReadOnlyList<Building> Buildings => _buildings;

	public IReadOnlyList<PlayerState> Players => _players;

	/// <summary>In spawn order. Dead units are removed at the end of the tick they die in.</summary>
	public IReadOnlyList<Unit> Units => _units;

	public IReadOnlyList<Projectile> Projectiles => _projectiles;

	/// <summary>The winning player, or -1 while the match is on.</summary>
	public int Winner { get; private set; } = -1;

	public event Action<Building> BuildingPlaced;
	public event Action<Building> BuildingRemoved;

	/// <summary>A building's setting changed (sorter filter, assembler recipe).</summary>
	public event Action<Building> BuildingChanged;

	public World(MapLayout map, int playerCount)
	{
		Map = map;
		_grid = new Building[map.Width * map.Height];
		_players = new PlayerState[playerCount];
		_cores = new Core[playerCount];
		_fields = new FlowField[playerCount];
		_power = new PowerGrid(map.Width, map.Height, playerCount);
		_powerFields = new FlowField[playerCount];
		_powerFieldDirty = new bool[playerCount];
		for (int i = 0; i < playerCount; i++)
		{
			_players[i] = new PlayerState(i);
			_fields[i] = new FlowField(map.Width, map.Height);
			_powerFields[i] = new FlowField(map.Width, map.Height);
		}
	}

	public Core GetCore(int player) => _cores[player];

	public int EnemyOf(int player) => (player + 1) % _players.Length;

	/// <summary>Which tiles each player's power grid covers, its networks and cords.</summary>
	public PowerGrid Power
	{
		get
		{
			RebuildFieldsIfDirty();
			return _power;
		}
	}

	/// <summary>
	/// Takes <paramref name="amount"/> energy from the player's network powering this tile, all or nothing.
	/// Whoever asks first in a tick gets served first (buildings in build order, then units by id).
	/// </summary>
	public bool TryDrawPower(int player, int x, int y, int amount) =>
		FreePower || Draw(Power.NetworkAt(player, x, y), amount);

	/// <summary>Like the tile version, from the owner's network that powers any tile of the building.</summary>
	public bool TryDrawPower(Building building, int amount) => FreePower || Draw(NetworkOf(building), amount);

	/// <summary>Whether the building's network has at least this much energy right now.</summary>
	public bool HasPower(Building building, int amount)
	{
		if (FreePower)
			return true;
		var network = NetworkOf(building);
		return network != null && network.Energy >= amount;
	}

	/// <summary>The owner's network powering the building (the first of its tiles that is covered), or null.</summary>
	public PowerNetwork NetworkOf(Building building)
	{
		var grid = Power;
		for (int y = building.Y; y < building.Y + building.Height; y++)
			for (int x = building.X; x < building.X + building.Width; x++)
			{
				var network = grid.NetworkAt(building.Owner, x, y);
				if (network != null)
					return network;
			}
		return null;
	}

	private bool Draw(PowerNetwork network, int amount)
	{
		if (network == null || network.Energy < amount)
			return false;
		_players[network.Owner].EnergyUsed(amount);
		foreach (var charger in network.Chargers)
		{
			int take = System.Math.Min(amount, charger.Energy);
			charger.Energy -= take;
			amount -= take;
			if (amount == 0)
				break;
		}
		return true;
	}

	public FlowField GetFlowField(int player)
	{
		RebuildFieldsIfDirty();
		return _fields[player];
	}

	/// <summary>A new 1v1 match: the default map with each player's core at the back of their room.</summary>
	public static World CreateMatch()
	{
		var world = new World(MapLayout.CreateDefault(), 2);
		var (w, h) = BuildingRules.Size(BuildingType.Core);

		// Player 0 in the left room, player 1 mirrored in the right room, both centred on the door.
		const int coreX = 6;
		int coreY = world.Map.Height / 2 - h / 2;
		world.TryPlace(BuildingType.Core, coreX, coreY, Direction.East, owner: 0);
		world.TryPlace(BuildingType.Core, world.Map.Width - coreX - w, coreY, Direction.West, owner: 1);

		foreach (var player in world._players)
		{
			player.Refund(BuildingRules.StartingStock);
			// Starting builders, lined up on the side of the core that faces the room.
			var core = world._cores[player.Id];
			int x = core.Facing == Direction.East ? core.X + core.Width : core.X - 1;
			for (int i = 0; i < UnitStats.StartingBuilders; i++)
				world.SpawnUnit(UnitType.Builder, player.Id, x, core.Y - 1 + i);
			for (int i = 0; i < UnitStats.StartingFarmers; i++)
				world.SpawnUnit(UnitType.Farmer, player.Id, x, core.Y + 2 + i);
		}
		return world;
	}

	public Building GetBuilding(int x, int y) => Map.InBounds(x, y) ? _grid[y * Map.Width + x] : null;

	public bool CanPlace(BuildingType type, int x, int y, int owner) => CheckPlace(type, x, y, owner) == PlaceError.None;

	/// <summary>Why <paramref name="owner"/> can't build here (location first, then cost), or None if they can.</summary>
	public PlaceError CheckPlace(BuildingType type, int x, int y, int owner)
	{
		var location = CheckLocation(type, x, y, owner);
		if (location != PlaceError.None)
			return location;
		return _players[owner].CanAfford(BuildingRules.Cost(type)) ? PlaceError.None : PlaceError.NotEnoughResources;
	}

	/// <summary>
	/// Whether <paramref name="owner"/> may put the building with its top-left corner on (x, y), ignoring cost:
	/// floor only, in their own room or the hall, not on another building, extractors on their deposit.
	/// </summary>
	public PlaceError CheckLocation(BuildingType type, int x, int y, int owner)
	{
		var (w, h) = BuildingRules.Size(type);
		var required = BuildingRules.RequiredResource(type);
		var home = MapLayout.HomeZone(owner);

		for (int cy = y; cy < y + h; cy++)
		{
			for (int cx = x; cx < x + w; cx++)
			{
				if (!Map.InBounds(cx, cy) || Map[cx, cy] != TileType.Floor)
					return PlaceError.NotFloor;
				var zone = Map.GetZone(cx, cy);
				if (zone != home && zone != Zone.Hall)
					return PlaceError.OutsideZone;
				if (_grid[cy * Map.Width + cx] != null)
					return PlaceError.Occupied;
				if (required != ResourceType.None && Map.GetResource(cx, cy) != required)
					return PlaceError.WrongResource;
			}
		}
		return PlaceError.None;
	}

	public bool TryPlace(BuildingType type, int x, int y, Direction facing, int owner)
	{
		if (!CanPlace(type, x, y, owner))
			return false;

		_players[owner].TrySpend(BuildingRules.Cost(type));
		var building = BuildingRules.Create(type, x, y, facing, owner, this);
		SetFootprint(building, building);
		_buildings.Add(building);
		if (building is Core core)
			_cores[owner] = core;
		_fieldsDirty = true;
		BuildingPlaced?.Invoke(building);
		return true;
	}

	/// <summary>Cycles the setting of <paramref name="owner"/>'s building at (x, y), if it has one.</summary>
	public bool TryConfigure(int x, int y, int owner)
	{
		var building = GetBuilding(x, y);
		if (building == null || building.Owner != owner || !building.CycleSetting())
			return false;
		BuildingChanged?.Invoke(building);
		return true;
	}

	/// <summary>Creates a unit in the middle of tile (tileX, tileY).</summary>
	public Unit SpawnUnit(UnitType type, int owner, int tileX, int tileY)
	{
		const int half = UnitStats.SubTile / 2;
		var unit = new Unit(_nextUnitId++, type, owner, tileX * UnitStats.SubTile + half, tileY * UnitStats.SubTile + half);
		_units.Add(unit);
		return unit;
	}

	/// <summary>
	/// <paramref name="owner"/> tearing down one of their own buildings (full refund). Cores can't be removed.
	/// Destruction by enemies should get its own method without the refund.
	/// </summary>
	public bool TryRemove(int x, int y, int owner)
	{
		var building = GetBuilding(x, y);
		if (building == null || building.Owner != owner || building.Type == BuildingType.Core)
			return false;

		_players[building.Owner].Refund(BuildingRules.Cost(building.Type));
		SetFootprint(building, null);
		_buildings.Remove(building);
		_fieldsDirty = true;
		BuildingRemoved?.Invoke(building);
		return true;
	}

	/// <summary>
	/// Advances the simulation one tick: buildings in placement order (towers fire), units in spawn order,
	/// then projectiles in firing order. Units killed this tick are removed last.
	/// </summary>
	public void Tick()
	{
		TickCount++;
		RebuildFieldsIfDirty();
		UpdateStorage();

		foreach (var building in _buildings)
			if (building.IsBuilt)
				building.Tick(this);
		foreach (var unit in _units)
		{
			if (unit.Type == UnitType.Builder)
				TickBuilder(unit);
			else if (unit.Type == UnitType.Farmer)
				TickFarmer(unit);
			else
				TickUnit(unit);
		}
		TickProjectiles();
		TickUpkeep();
		_units.RemoveAll(u => u.Health <= 0);

		for (int p = 0; p < _cores.Length && Winner < 0; p++)
			if (_cores[p] != null && _cores[p].Health == 0)
				Winner = EnemyOf(p);
	}

	private const int TicksPerMinute = TicksPerSecond * 60;

	/// <summary>Units eat: see PlayerState.Hunger. Starving units lose health every StarveTicks.</summary>
	private void TickUpkeep()
	{
		foreach (var player in _players)
			player.FoodUpkeepPerMinute = 0;
		foreach (var unit in _units)
		{
			if (unit.Health <= 0)
				continue;
			int perMinute = UnitStats.FoodPerMinute(unit.Type);
			_players[unit.Owner].Hunger += perMinute;
			_players[unit.Owner].FoodUpkeepPerMinute += perMinute;
		}
		if (TickCount % TicksPerSecond == 0)
			foreach (var player in _players)
				player.NextSecond();

		var foodOnly = new ItemStack[] { new(ItemType.Food, 1) };
		foreach (var player in _players)
		{
			while (player.Hunger >= TicksPerMinute && player.TrySpend(foodOnly))
			{
				player.Hunger -= TicksPerMinute;
				player.FoodEaten();
			}
			player.Starving = player.Hunger >= TicksPerMinute;
			if (player.Starving)
				player.Hunger = TicksPerMinute; // no debt: back to normal as soon as there is food
		}

		if (TickCount % UnitStats.StarveTicks == 0)
			foreach (var unit in _units)
				if (_players[unit.Owner].Starving)
					unit.Health -= UnitStats.StarveDamage;
	}

	/// <summary>Counts each player's finished warehouses, which set how much they can store.</summary>
	private void UpdateStorage()
	{
		foreach (var player in _players)
			player.Warehouses = 0;
		foreach (var building in _buildings)
			if (building.Type == BuildingType.Warehouse && building.IsBuilt)
				_players[building.Owner].Warehouses++;
	}

	/// <summary>
	/// A hash of the whole game state. Identical inputs must give identical checksums on every machine
	/// and every run; comparing them catches non-determinism (and, later, lockstep desyncs).
	/// </summary>
	public ulong Checksum()
	{
		var hash = StateHash.Start();
		hash.Add(TickCount); hash.Add(Winner); hash.Add(_nextUnitId);
		foreach (var player in _players)
			player.HashInto(ref hash);
		hash.Add(_buildings.Count);
		foreach (var building in _buildings)
			building.HashInto(ref hash);
		hash.Add(_units.Count);
		foreach (var unit in _units)
		{
			hash.Add(unit.Id); hash.Add((int)unit.Type); hash.Add(unit.Owner);
			hash.Add(unit.X); hash.Add(unit.Y); hash.Add(unit.Health); hash.Add(unit.AttackCooldown);
			hash.Add(unit.Job?.X ?? -1); hash.Add(unit.Job?.Y ?? -1);
			hash.Add((int)unit.Carrying); hash.Add(unit.CarryAmount); hash.Add(unit.WorkTimer);
			hash.Add(unit.Charge); hash.Add((int)unit.PowerState);
		}
		hash.Add(_projectiles.Count);
		foreach (var shot in _projectiles)
		{
			hash.Add((int)shot.Kind); hash.Add(shot.Owner); hash.Add(shot.ToX); hash.Add(shot.ToY); hash.Add(shot.TicksLeft);
		}
		return hash.Value;
	}

	/// <summary>Removes every unit (tests start from a clean slate with this).</summary>
	internal void ClearUnits() => _units.Clear();

	/// <summary>Test helper: removes the player's new soldiers (keeps factories busy without crowding).</summary>
	internal void ClearSoldiers() => _units.RemoveAll(u => u.Type == UnitType.PlasticSoldier);

	/// <summary>
	/// Test switch: every consumer counts as powered and golems/cars never lose charge.
	/// Lets tests of other systems ignore the power grid. Always false in real matches.
	/// </summary>
	internal bool FreePower { get; set; }

	public int CountBuildings(int owner, BuildingType type)
	{
		int count = 0;
		foreach (var building in _buildings)
			if (building.Owner == owner && building.Type == type)
				count++;
		return count;
	}

	public int CountUnits(int owner, UnitType type)
	{
		int count = 0;
		foreach (var unit in _units)
			if (unit.Owner == owner && unit.Type == type && unit.Health > 0)
				count++;
		return count;
	}

	/// <summary>How many builders have picked this construction site.</summary>
	public int BuildersOn(Building site)
	{
		int count = 0;
		foreach (var unit in _units)
			if (unit.Job == site && unit.Health > 0)
				count++;
		return count;
	}

	// ---------------------------------------------------------------------------------------------
	// Builders: pick the nearest unfinished site (spreading out over several), walk there, build.

	private const int CrowdPenalty = 12; // a site already being worked on counts as this many tiles further away

	private void TickBuilder(Unit builder)
	{
		builder.PrevX = builder.X;
		builder.PrevY = builder.Y;
		builder.MoveX = builder.MoveY = 0;

		var job = builder.Job;
		if (job != null && (job.IsBuilt || GetBuilding(job.X, job.Y) != job))
			builder.Job = job = null;
		if (job == null)
		{
			builder.Job = job = ChooseSite(builder);
			builder.Path = null;
			if (job == null)
				return; // nothing to build: wait where it is
		}

		switch (WalkTo(builder, job))
		{
			case Walk.Arrived:
				job.AddWork(UnitStats.BuilderWorkPerTick);
				Face(builder, job);
				if (job.IsBuilt)
					_fieldsDirty = true;
				break;
			case Walk.Unreachable:
				builder.Job = null; // try something else next tick
				break;
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Farmers: harvest the nearest ripe field (one farmer per field), carry the crops to the nearest
	// kitchen with room, or else to the toybox / a warehouse, and repeat.

	private const int FieldTakenPenalty = 1000; // a field someone else is already going for

	private void TickFarmer(Unit farmer)
	{
		farmer.PrevX = farmer.X;
		farmer.PrevY = farmer.Y;
		farmer.MoveX = farmer.MoveY = 0;

		if (farmer.CarryAmount > 0)
			DeliverCrops(farmer);
		else
			GoHarvest(farmer);
	}

	private void GoHarvest(Unit farmer)
	{
		// Fetching crops from storage for a kitchen (set up below when nothing is ripe).
		if (farmer.Job is Core or Warehouse)
		{
			FetchStoredCrops(farmer);
			return;
		}

		if (farmer.Job is not CropField field || !IsStanding(field) || !field.IsRipe)
		{
			farmer.Job = field = ChooseField(farmer);
			farmer.WorkTimer = 0;
			if (field == null)
			{
				// Nothing ripe: bring stored crops to a kitchen that has room, if there are any.
				if (_players[farmer.Owner].GetCount(ItemType.Crop) > 0 && KitchenWithRoom(farmer) != null)
					farmer.Job = NearestStorage(farmer);
				return;
			}
		}

		switch (WalkTo(farmer, field))
		{
			case Walk.Arrived:
				Face(farmer, field);
				if (++farmer.WorkTimer < UnitStats.HarvestTicks)
					return;
				farmer.Carrying = ItemType.Crop;
				farmer.CarryAmount = field.Harvest();
				farmer.WorkTimer = 0;
				farmer.Job = null;
				break;
			case Walk.Unreachable:
				farmer.Job = null;
				break;
		}
	}

	private void DeliverCrops(Unit farmer)
	{
		if (farmer.Job == null || !IsStanding(farmer.Job) || !farmer.Job.CanTake(farmer.Carrying))
		{
			farmer.Job = ChooseDropOff(farmer);
			if (farmer.Job == null)
				return; // everything full: wait with the crops
		}

		switch (WalkTo(farmer, farmer.Job))
		{
			case Walk.Arrived:
				Face(farmer, farmer.Job);
				while (farmer.CarryAmount > 0 && farmer.Job.Offer(farmer.Carrying, Direction.East))
					farmer.CarryAmount--;
				if (farmer.CarryAmount == 0)
					farmer.Carrying = ItemType.None;
				farmer.Job = null; // done, or this one is full: look again next tick
				break;
			case Walk.Unreachable:
				farmer.Job = null;
				break;
		}
	}

	/// <summary>Walks to the toybox/warehouse and picks up stored crops (then DeliverCrops takes over).</summary>
	private void FetchStoredCrops(Unit farmer)
	{
		var player = _players[farmer.Owner];
		if (!IsStanding(farmer.Job) || player.GetCount(ItemType.Crop) == 0 || KitchenWithRoom(farmer) == null)
		{
			farmer.Job = null;
			return;
		}
		switch (WalkTo(farmer, farmer.Job))
		{
			case Walk.Arrived:
				int take = System.Math.Min(CropField.Yield, player.GetCount(ItemType.Crop));
				player.TrySpend(new[] { new ItemStack(ItemType.Crop, take) });
				farmer.Carrying = ItemType.Crop;
				farmer.CarryAmount = take;
				farmer.Job = null; // DeliverCrops picks the kitchen
				break;
			case Walk.Unreachable:
				farmer.Job = null;
				break;
		}
	}

	private Building KitchenWithRoom(Unit farmer)
	{
		foreach (var building in _buildings)
			if (building is Kitchen && building.Owner == farmer.Owner && building.CanTake(ItemType.Crop))
				return building;
		return null;
	}

	private Building NearestStorage(Unit farmer)
	{
		Building best = null;
		int bestDistance = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building.Owner != farmer.Owner || !building.IsBuilt || building is not (Core or Warehouse))
				continue;
			int distance = TileDistance(farmer, building);
			if (distance < bestDistance)
				(best, bestDistance) = (building, distance);
		}
		return best;
	}

	private CropField ChooseField(Unit farmer)
	{
		CropField best = null;
		int bestScore = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building is not CropField field || field.Owner != farmer.Owner || !field.IsBuilt || !field.IsRipe)
				continue;
			int score = TileDistance(farmer, field);
			foreach (var other in _units)
				if (other != farmer && other.Job == field && other.Health > 0)
					score += FieldTakenPenalty;
			if (score < bestScore)
				(best, bestScore) = (field, score);
		}
		return best;
	}

	/// <summary>The nearest finished kitchen with room for the load, else the nearest toybox/warehouse with room.</summary>
	private Building ChooseDropOff(Unit farmer)
	{
		Building best = null;
		int bestScore = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building.Owner != farmer.Owner || !building.IsBuilt || !building.CanTake(farmer.Carrying))
				continue;
			bool storage = building is Core or Warehouse;
			int score = TileDistance(farmer, building) + (storage ? 1000 : 0); // kitchens first
			if (score < bestScore)
				(best, bestScore) = (building, score);
		}
		return best;
	}

	// ---------------------------------------------------------------------------------------------
	// Walking for builders and farmers.

	private enum Walk { Walking, Arrived, Unreachable }

	/// <summary>One step along the unit's path to the building; Arrived once it stands next to it.</summary>
	private Walk WalkTo(Unit unit, Building target)
	{
		if (IsAround(target, unit.TileX, unit.TileY))
			return Walk.Arrived;

		if (unit.Path == null || unit.PathTarget != target)
		{
			unit.Path = FindPathTo(unit.TileX, unit.TileY, target);
			unit.PathIndex = 0;
			unit.PathTarget = target;
			if (unit.Path == null)
				return Walk.Unreachable;
		}

		// Walk to the centre of the next tile on the path (a step never overshoots it).
		if (unit.PathIndex < unit.Path.Count)
		{
			const int half = UnitStats.SubTile / 2;
			var (tx, ty) = unit.Path[unit.PathIndex];
			int goalX = tx * UnitStats.SubTile + half, goalY = ty * UnitStats.SubTile + half;
			TryStep(unit, goalX, goalY);
			if (unit.X == goalX && unit.Y == goalY)
				unit.PathIndex++;
		}
		return Walk.Walking;
	}

	private static void Face(Unit unit, Building building)
	{
		unit.MoveX = building.CenterX - unit.X;
		unit.MoveY = building.CenterY - unit.Y;
	}

	private static int TileDistance(Unit unit, Building building) =>
		System.Math.Abs(building.X - unit.TileX) + System.Math.Abs(building.Y - unit.TileY);

	/// <summary>The nearest unfinished site of the builder's owner, preferring ones nobody works on yet.</summary>
	private Building ChooseSite(Unit builder)
	{
		Building best = null;
		int bestScore = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building.Owner != builder.Owner || building.IsBuilt)
				continue;
			int distance = System.Math.Abs(building.X - builder.TileX) + System.Math.Abs(building.Y - builder.TileY);
			int score = distance + CrowdPenalty * BuildersOn(building);
			if (score < bestScore)
			{
				best = building;
				bestScore = score;
			}
		}
		return best;
	}

	/// <summary>Whether tile (x, y) is on the building or next to it (diagonals count).</summary>
	private static bool IsAround(Building building, int x, int y) =>
		x >= building.X - 1 && x <= building.X + building.Width &&
		y >= building.Y - 1 && y <= building.Y + building.Height;

	private static readonly (int dx, int dy)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

	/// <summary>
	/// Breadth-first path over floor (buildings don't block builders) to any tile around the building.
	/// The start tile is not included. Null if unreachable.
	/// </summary>
	private List<(int X, int Y)> FindPathTo(int fromX, int fromY, Building target)
	{
		if (IsAround(target, fromX, fromY))
			return new List<(int X, int Y)>();

		var previous = new Dictionary<(int, int), (int, int)> { [(fromX, fromY)] = (fromX, fromY) };
		var queue = new Queue<(int X, int Y)>();
		queue.Enqueue((fromX, fromY));
		while (queue.Count > 0)
		{
			var tile = queue.Dequeue();
			foreach (var (dx, dy) in Steps)
			{
				var next = (X: tile.X + dx, Y: tile.Y + dy);
				if (previous.ContainsKey(next) || !Map.InBounds(next.X, next.Y) || Map[next.X, next.Y] != TileType.Floor)
					continue;
				previous[next] = tile;
				if (IsAround(target, next.X, next.Y))
				{
					var path = new List<(int X, int Y)>();
					for (var at = next; at != (fromX, fromY); at = previous[at])
						path.Add(at);
					path.Reverse();
					return path;
				}
				queue.Enqueue(next);
			}
		}
		return null;
	}

	private void TickUnit(Unit unit)
	{
		unit.PrevX = unit.X;
		unit.PrevY = unit.Y;
		unit.MoveX = unit.MoveY = 0;
		if (unit.AttackCooldown > 0)
			unit.AttackCooldown--;
		if (Winner >= 0 || _cores[EnemyOf(unit.Owner)] == null)
			return;
		if (PowerStats.IsElectric(unit.Type) && !FreePower && TickCharge(unit))
			return;

		// Enemy buildings in the way are always within range, so they get shot before the unit walks on;
		// its own buildings it just walks over.
		var (targetUnit, targetBuilding) = SelectTarget(unit, UnitStats.Range(unit.Type), includeCore: true);
		if (targetUnit != null || targetBuilding != null)
		{
			// Stand still and shoot.
			int aimX = targetUnit?.X ?? targetBuilding.CenterX;
			int aimY = targetUnit?.Y ?? targetBuilding.CenterY;
			unit.MoveX = aimX - unit.X;
			unit.MoveY = aimY - unit.Y;
			if (unit.AttackCooldown == 0)
			{
				var def = UnitStats.Def(unit.Type);
				int damage = targetBuilding != null ? def.Damage * def.BuildingDamagePercent / 100 : def.Damage;
				var kind = def.TargetsUnits ? DamageKind.Bullet : DamageKind.Punch;
				Fire(new Projectile(kind, unit.Owner, damage, 0, def.ShotTravelTicks, unit.X, unit.Y, targetUnit, targetBuilding));
				unit.AttackCooldown = def.AttackTicks;
			}
			return;
		}

		// Nothing in range: go for anything it can see (except the core) before heading on to the core.
		var (seenUnit, seenBuilding) = SelectTarget(unit, UnitStats.Sight(unit.Type), includeCore: false);
		if (seenUnit != null || seenBuilding != null)
		{
			int goalX = seenUnit?.X ?? seenBuilding.CenterX;
			int goalY = seenUnit?.Y ?? seenBuilding.CenterY;
			if (TryStep(unit, goalX, goalY))
				return;
			// A wall is in the way of the straight line; fall back to the flow field.
		}

		// Head for the centre of the next tile downhill in the flow field.
		var (nextX, nextY) = _fields[unit.Owner].NextTile(unit.TileX, unit.TileY);
		const int half = UnitStats.SubTile / 2;
		TryStep(unit, nextX * UnitStats.SubTile + half, nextY * UnitStats.SubTile + half);
	}

	/// <summary>
	/// Battery upkeep for golems and cars (see <see cref="UnitPower"/>). On its own grid it charges from that
	/// network; off it, it drains. Returns true when the battery decides what the unit does this tick
	/// (returning, charging or empty: no fighting), false when it may fight as usual.
	/// </summary>
	private bool TickCharge(Unit unit)
	{
		int max = PowerStats.MaxCharge(unit.Type);
		var network = Power.NetworkAt(unit.Owner, unit.TileX, unit.TileY);
		if (network != null)
		{
			int want = System.Math.Min(PowerStats.UnitChargePerTick, max - unit.Charge);
			if (want > 0 && Draw(network, want))
				unit.Charge += want;
			// Coming back, or low while passing through: stay and charge up before going out again.
			if (unit.PowerState is UnitPower.Returning or UnitPower.Empty ||
				(unit.PowerState == UnitPower.Normal && unit.Charge * 100L <= max * (long)PowerStats.ReturnPercent))
				unit.PowerState = UnitPower.Charging;
			if (unit.PowerState == UnitPower.Charging && unit.Charge * 100L >= max * (long)PowerStats.ChargedPercent)
				unit.PowerState = UnitPower.Normal;
			return unit.PowerState == UnitPower.Charging;
		}

		unit.Charge = System.Math.Max(0, unit.Charge - PowerStats.DrainPerTick(unit.Type));
		if (unit.Charge == 0)
			unit.PowerState = UnitPower.Empty;
		else if (unit.PowerState == UnitPower.Charging ||
			(unit.PowerState == UnitPower.Normal && unit.Charge * 100L <= max * (long)PowerStats.ReturnPercent))
			unit.PowerState = UnitPower.Returning;
		if (unit.PowerState == UnitPower.Normal)
			return false;

		var (nextX, nextY) = PowerField(unit.Owner).NextTile(unit.TileX, unit.TileY);
		int speed = UnitStats.Speed(unit.Type);
		if (unit.PowerState == UnitPower.Empty)
			speed = speed * PowerStats.EmptySpeedPercent / 100;
		const int half = UnitStats.SubTile / 2;
		TryStep(unit, nextX * UnitStats.SubTile + half, nextY * UnitStats.SubTile + half, speed);
		return true;
	}

	/// <summary>Distance to the nearest tile the player's grid powers (built on first use after a change).</summary>
	internal FlowField PowerField(int player)
	{
		RebuildFieldsIfDirty();
		if (_powerFieldDirty[player])
		{
			_powerFieldDirty[player] = false;
			_powerFields[player].Build(this, player, (x, y) => _power.NetworkAt(player, x, y) != null);
		}
		return _powerFields[player];
	}

	/// <summary>Moves the unit up to its speed towards a point. Refuses (returns false) to step into a wall.</summary>
	private bool TryStep(Unit unit, int goalX, int goalY, int speed = -1)
	{
		int dx = goalX - unit.X;
		int dy = goalY - unit.Y;
		int length = IntMath.Sqrt(dx * dx + dy * dy);
		if (length == 0)
			return true;

		if (speed < 0)
			speed = UnitStats.Speed(unit.Type);
		if (length > speed)
		{
			dx = dx * speed / length;
			dy = dy * speed / length;
		}

		int tileX = (unit.X + dx) / UnitStats.SubTile, tileY = (unit.Y + dy) / UnitStats.SubTile;
		if (!Map.InBounds(tileX, tileY) || Map[tileX, tileY] != TileType.Floor)
			return false;

		unit.X += dx;
		unit.Y += dy;
		unit.MoveX = dx;
		unit.MoveY = dy;
		return true;
	}

	/// <summary>
	/// The closest living unit not owned by <paramref name="owner"/> within <paramref name="range"/>
	/// (sub-tile units) of the point. Ties go to the oldest unit, so the result is deterministic.
	/// </summary>
	public Unit FindNearestEnemy(int owner, int x, int y, int range)
	{
		Unit best = null;
		long bestDistance = (long)range * range;
		foreach (var unit in _units)
		{
			if (unit.Owner == owner || unit.Health <= 0)
				continue;
			long dx = unit.X - x, dy = unit.Y - y;
			long distance = dx * dx + dy * dy;
			if (distance <= bestDistance && (best == null || distance < bestDistance))
			{
				best = unit;
				bestDistance = distance;
			}
		}
		return best;
	}

	/// <summary>
	/// The best target within <paramref name="range"/>, in priority order:
	/// 1. enemy units, 2. enemy towers, 3. any other enemy building, 4. the enemy core last (if included).
	/// So an attack tears down the base around the core first. Nearest wins within a tier;
	/// ties go to the oldest unit / earliest-built building (deterministic).
	/// </summary>
	internal (Unit, Building) SelectTarget(Unit unit, int range, bool includeCore)
	{
		long range2 = (long)range * range;

		if (UnitStats.Def(unit.Type).TargetsUnits)
		{
			var enemyUnit = FindNearestEnemy(unit.Owner, unit.X, unit.Y, range);
			if (enemyUnit != null)
				return (enemyUnit, null);
		}

		Building tower = null, other = null, core = null;
		long towerDistance = range2, otherDistance = range2;
		foreach (var building in _buildings)
		{
			if (building.Owner == unit.Owner)
				continue;
			long distance = DistanceSquaredTo(building, unit.X, unit.Y);
			if (distance > range2)
				continue;

			if (building is Tower)
			{
				if (tower == null || distance < towerDistance)
					(tower, towerDistance) = (building, distance);
			}
			else if (building is Core)
			{
				if (includeCore)
					core = building;
			}
			else if (other == null || distance < otherDistance)
			{
				(other, otherDistance) = (building, distance);
			}
		}
		return (null, tower ?? other ?? core);
	}

	/// <summary>Squared distance from a point to the nearest edge of the building's footprint (0 if inside).</summary>
	private static long DistanceSquaredTo(Building building, int x, int y)
	{
		int left = building.X * UnitStats.SubTile, right = (building.X + building.Width) * UnitStats.SubTile;
		int top = building.Y * UnitStats.SubTile, bottom = (building.Y + building.Height) * UnitStats.SubTile;
		long dx = x < left ? left - x : x > right ? x - right : 0;
		long dy = y < top ? top - y : y > bottom ? y - bottom : 0;
		return dx * dx + dy * dy;
	}

	public void Fire(Projectile projectile) => _projectiles.Add(projectile);

	private void TickProjectiles()
	{
		for (int i = 0; i < _projectiles.Count; i++)
		{
			var shot = _projectiles[i];
			var unit = shot.TargetUnit != null && shot.TargetUnit.Health > 0 ? shot.TargetUnit : null;
			var building = shot.TargetBuilding != null && IsStanding(shot.TargetBuilding) ? shot.TargetBuilding : null;

			// Single-target shots home in on a unit while it lives.
			if (unit != null && shot.Kind != DamageKind.Area)
			{
				shot.ToX = unit.X;
				shot.ToY = unit.Y;
			}

			if (--shot.TicksLeft > 0)
				continue;

			if (shot.Kind == DamageKind.Area)
			{
				long radius2 = (long)shot.AreaRadius * shot.AreaRadius;
				foreach (var victim in _units)
				{
					long dx = victim.X - shot.ToX, dy = victim.Y - shot.ToY;
					if (victim.Owner != shot.Owner && victim.Health > 0 && dx * dx + dy * dy <= radius2)
						Hit(victim, shot);
				}
			}
			else if (unit != null)
			{
				Hit(unit, shot);
			}
			else if (building != null)
			{
				building.TakeDamage(shot.Damage);
				if (building.Health == 0 && building.Type != BuildingType.Core)
					Destroy(building);
			}
		}
		_projectiles.RemoveAll(p => p.TicksLeft <= 0);
	}

	private static void Hit(Unit unit, Projectile shot)
	{
		int percent = Combat.DamagePercent(shot.Kind, UnitStats.Armor(unit.Type));
		unit.Health -= shot.Damage * percent / 100;
	}

	private bool IsStanding(Building building) => GetBuilding(building.X, building.Y) == building;

	/// <summary>A building shot to pieces: gone, no refund.</summary>
	private void Destroy(Building building)
	{
		SetFootprint(building, null);
		_buildings.Remove(building);
		_fieldsDirty = true;
		BuildingRemoved?.Invoke(building);
	}

	private void RebuildFieldsIfDirty()
	{
		if (!_fieldsDirty)
			return;
		_fieldsDirty = false;
		_power.Rebuild(_buildings);
		for (int p = 0; p < _players.Length; p++)
		{
			var target = _cores[EnemyOf(p)];
			if (target != null)
				_fields[p].Build(this, target, p);
			_powerFieldDirty[p] = true;
		}
	}

	private void SetFootprint(Building building, Building value)
	{
		for (int cy = building.Y; cy < building.Y + building.Height; cy++)
			for (int cx = building.X; cx < building.X + building.Width; cx++)
				_grid[cy * Map.Width + cx] = value;
	}
}
