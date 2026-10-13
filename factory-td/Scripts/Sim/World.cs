using System;
using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>
/// Game state: the map, the players and everything built on it. Pure C#.
/// Buildings can cover several tiles; every tile they cover points to the same Building.
/// Split by subsystem: WorldPower, WorldVision, Workers, Fighting, Upkeep, Scouting, Separation.
/// </summary>
public sealed partial class World
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
	private readonly FlowField[] _golemFields; // the same for units that smash through walls
											   // What has to be worked out again before it is used. The power grid changes whenever a node is placed,
											   // removed or finished; a player's flow field only when an ENEMY building appears or goes (own buildings
											   // cost nothing to walk through, and a construction site already counts as a building).
	private bool _powerDirty = true;
	private int _nextBuildingSerial;
	private int _scanMark;
	private bool[] _flowDirty;
	private readonly PowerGrid _power;
	private readonly Vision _vision;
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
		_golemFields = new FlowField[playerCount];
		_power = new PowerGrid(map.Width, map.Height, playerCount);
		_flowDirty = new bool[playerCount];
		System.Array.Fill(_flowDirty, true);
		_vision = new Vision(map.Width, map.Height, playerCount);
		_powerFields = new FlowField[playerCount];
		_powerFieldDirty = new bool[playerCount];
		for (int i = 0; i < playerCount; i++)
		{
			_players[i] = new PlayerState(i);
			_fields[i] = new FlowField(map.Width, map.Height);
			_golemFields[i] = new FlowField(map.Width, map.Height);
			_powerFields[i] = new FlowField(map.Width, map.Height);
		}
	}

	public Core GetCore(int player) => _cores[player];

	public int EnemyOf(int player) => (player + 1) % _players.Length;

	public FlowField GetFlowField(int player, UnitType type = UnitType.PlasticSoldier)
	{
		RebuildFieldsIfDirty();
		return FieldFor(player, type);
	}

	private FlowField FieldFor(int player, UnitType type) =>
		WallStats.BreaksWalls(type) ? _golemFields[player] : _fields[player];

	/// <summary>Whether a unit of <paramref name="player"/> can't stand on (x, y): not floor, or a wall in its way.</summary>
	internal bool BlocksUnit(int player, int x, int y) =>
		!Map.InBounds(x, y) || Map[x, y] != TileType.Floor || WallBlocks(player, x, y);

	/// <summary>Whether a built wall stands on (x, y) that a unit of <paramref name="player"/> can't pass.</summary>
	internal bool WallBlocks(int player, int x, int y) => GetBuilding(x, y) is Wall wall && wall.Blocks(player);

	/// <summary>A new 1v1 match: the default map with each player's core at the back of their room.</summary>
	/// <param name="obstacles">Big toys lying in the rooms (MapLayout.Obstacles).</param>
	public static World CreateMatch(bool obstacles = true, int seed = MapLayout.DefaultSeed, MapTheme theme = MapTheme.Nursery)
	{
		var world = new World(MapLayout.CreateDefault(obstacles, seed, theme), 2);
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
		world._vision.Recompute(world);
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
				// Checked first, so a ghost in the dark doesn't give away whether there is a deposit.
				if (required != ResourceType.None && !IsExplored(owner, cx, cy))
					return PlaceError.Unexplored;
				if (required != ResourceType.None && Map.GetResource(cx, cy) != required)
					return PlaceError.WrongResource;
			}
		}
		return PlaceError.None;
	}

	/// <summary>
	/// Carries out a player's command (the only way players and bots change the world, so it can run in
	/// lockstep). Commands from unknown players or for tiles outside the map are ignored.
	/// </summary>
	public bool Apply(PlayerCommand command)
	{
		if (command.Player < 0 || command.Player >= _players.Length || !Map.InBounds(command.X, command.Y))
			return false;
		return command.Kind switch
		{
			CommandKind.Place => TryPlace(command.Type, command.X, command.Y, command.Facing, command.Player),
			CommandKind.Remove => TryRemove(command.X, command.Y, command.Player),
			CommandKind.Configure => TryConfigure(command.X, command.Y, command.Player),
			_ => false,
		};
	}

	public bool TryPlace(BuildingType type, int x, int y, Direction facing, int owner)
	{
		if (!CanPlace(type, x, y, owner))
			return false;

		_players[owner].TrySpend(BuildingRules.Cost(type));
		var building = BuildingRules.Create(type, x, y, facing, owner, this);
		building.Serial = _nextBuildingSerial++;
		SetFootprint(building, building);
		_buildings.Add(building);
		if (building is Core core)
			_cores[owner] = core;
		MarkChanged(building);
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
		MarkChanged(building);
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
			else if (unit.Type == UnitType.Scout)
				TickScout(unit);
			else
				TickUnit(unit);
		}
		SeparateUnits();
		TickProjectiles();
		TickUpkeep();
		foreach (var unit in _units)
		{
			int direction = Vision.DirectionIndex(unit.MoveX, unit.MoveY);
			if (direction >= 0)
				unit.LightDirection = direction;
		}
		if (TickCount % VisionStats.VisionTicks == 0)
			_vision.Recompute(this);
		_units.RemoveAll(u => u.Health <= 0);
		ReplaceLostBuilders();

		for (int p = 0; p < _cores.Length && Winner < 0; p++)
			if (_cores[p] != null && _cores[p].Health == 0)
				Winner = EnemyOf(p);
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
		_vision.HashInto(ref hash);
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
			hash.Add(unit.Charge); hash.Add((int)unit.PowerState); hash.Add(unit.LightDirection);
			hash.Add(unit.ScoutTargetX); hash.Add(unit.ScoutTargetY); hash.Add(unit.FleeTicks); hash.Add(unit.FleeX); hash.Add(unit.FleeY);
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

	private bool IsStanding(Building building) => GetBuilding(building.X, building.Y) == building;

	/// <summary>A building shot to pieces: gone, no refund.</summary>
	private void Destroy(Building building)
	{
		SetFootprint(building, null);
		_buildings.Remove(building);
		MarkChanged(building);
		BuildingRemoved?.Invoke(building);
	}

	/// <summary>
	/// A building appeared or went: the power grid and the enemies' flow fields are out of date
	/// (everyone's for a wall: walls block their owner's units too).
	/// </summary>
	private void MarkChanged(Building building)
	{
		_powerDirty = true;
		for (int p = 0; p < _flowDirty.Length; p++)
			if (p != building.Owner || building is Wall)
				_flowDirty[p] = true;
	}

	private void RebuildFieldsIfDirty()
	{
		if (_powerDirty)
		{
			_powerDirty = false;
			_power.Rebuild(_buildings);
			for (int p = 0; p < _players.Length; p++)
				_powerFieldDirty[p] = true;
		}
		for (int p = 0; p < _players.Length; p++)
		{
			if (!_flowDirty[p])
				continue;
			_flowDirty[p] = false;
			var target = _cores[EnemyOf(p)];
			if (target != null)
			{
				_fields[p].Build(this, target, p);
				_golemFields[p].Build(this, target, p, WallStats.GolemWallPenalty);
			}
			_powerFieldDirty[p] = true; // the way back to the grid crosses enemy buildings too
		}
	}

	private void SetFootprint(Building building, Building value)
	{
		for (int cy = building.Y; cy < building.Y + building.Height; cy++)
			for (int cx = building.X; cx < building.X + building.Width; cx++)
				_grid[cy * Map.Width + cx] = value;
	}
}
