namespace FactoryTD.Sim;

public enum TileType : byte
{
	Empty,
	Floor,
	Wall,
	/// <summary>A big toy lying on the floor: blocks walking and building like a wall, but not light.</summary>
	Obstacle,
}

public enum ResourceType : byte
{
	None,
	Brick,
	Plastic,
	Battery,
}

/// <summary>Who may build where. Each player builds in their own room and in the shared hall.</summary>
public enum Zone : byte
{
	None,
	LeftRoom,   // player 0
	RightRoom,  // player 1
	Hall,       // both
}

/// <summary>
/// The tile grid of the map. Pure C# with no Godot dependencies, so the
/// simulation (flow fields, building placement) can use it directly.
/// Layout: [room A] - hall - [room B], left to right, both rooms with one door into the hall.
/// The map is mirrored left/right so both players get identical rooms and deposits.
/// </summary>
public sealed partial class MapLayout
{
	public const int RoomSize = 60;   // interior tiles per side
	public const int HallLength = 36;
	public const int HallWidth = 12;
	public const int DoorWidth = 4;

	private readonly TileType[] _tiles;
	private readonly ResourceType[] _resources;
	private readonly Zone[] _zones;
	private readonly (int X0, int Y0, int X1, int Y1)[] _zoneBounds = new (int, int, int, int)[4];

	public int Width { get; }
	public int Height { get; }

	public MapLayout(int width, int height)
	{
		Width = width;
		Height = height;
		_tiles = new TileType[width * height];
		_resources = new ResourceType[width * height];
		_zones = new Zone[width * height];
	}

	public TileType this[int x, int y]
	{
		get => _tiles[y * Width + x];
		private set => _tiles[y * Width + x] = value;
	}

	public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

	/// <summary>The resource deposit on a tile, or None.</summary>
	public ResourceType GetResource(int x, int y) => _resources[y * Width + x];

	public Zone GetZone(int x, int y) => InBounds(x, y) ? _zones[y * Width + x] : Zone.None;

	/// <summary>The tile rectangle (inclusive) a zone covers, walls included.</summary>
	public (int X0, int Y0, int X1, int Y1) ZoneBounds(Zone zone) => _zoneBounds[(int)zone];

	/// <summary>The room a player builds in: player 0 has the left room, player 1 the right.</summary>
	public static Zone HomeZone(int player) => player == 0 ? Zone.LeftRoom : Zone.RightRoom;

	/// <param name="obstacles">Big toys in the rooms (off in most tests, so they can't get in the way).</param>
	/// <param name="seed">Where the toys lie; the same seed gives the same map everywhere.</param>
	public static MapLayout CreateDefault(bool obstacles = true, int seed = DefaultSeed)
	{
		const int roomOuter = RoomSize + 2; // including walls
		var map = new MapLayout(roomOuter * 2 + HallLength, roomOuter);

		int roomBX = roomOuter + HallLength;
		map.AddRoom(0, 0);
		map.AddRoom(roomBX, 0);

		// Hall between the rooms, vertically centered on them.
		int hallTop = (roomOuter - HallWidth) / 2;
		int hallBottom = hallTop + HallWidth - 1;
		map.Fill(roomOuter, hallTop - 1, roomBX - 1, hallBottom + 1, TileType.Wall);
		map.Fill(roomOuter, hallTop, roomBX - 1, hallBottom, TileType.Floor);

		// Doors: openings in each room's wall facing the hall.
		int doorTop = (roomOuter - DoorWidth) / 2;
		int doorBottom = doorTop + DoorWidth - 1;
		map.Fill(roomOuter - 1, doorTop, roomOuter - 1, doorBottom, TileType.Floor);
		map.Fill(roomBX, doorTop, roomBX, doorBottom, TileType.Floor);

		// Zones: each room (with its walls and door) belongs to one player, the hall between is shared.
		map.SetZone(Zone.LeftRoom, 0, 0, roomOuter - 1, roomOuter - 1);
		map.SetZone(Zone.RightRoom, roomBX, 0, map.Width - 1, roomOuter - 1);
		map.SetZone(Zone.Hall, roomOuter, hallTop - 1, roomBX - 1, hallBottom + 1);

		map.AddDeposits();
		if (obstacles)
			map.AddObstacles(seed);
		return map;
	}

	/// <summary>
	/// Deposits are placed in room A / the left half of the hall and mirrored to the right.
	/// Starter deposits in the rooms are small; the rich ones are in the hall.
	/// </summary>
	private void AddDeposits()
	{
		// Room A starter deposits (interior is x 1..60, y 1..60, door on the right wall).
		// Bricks and plastic sit a few tiles above/below the core (x 6..7, y 30..31) so the first belts are short.
		AddMirroredDeposit(10, 22, 13, 25, ResourceType.Brick);
		AddMirroredDeposit(10, 36, 13, 39, ResourceType.Plastic);
		AddMirroredDeposit(40, 46, 42, 48, ResourceType.Battery);

		// Hall (interior is x 62..97, y 25..36, doors at y 29..32).
		// Side deposits stay clear of the door rows so the path through stays open.
		AddMirroredDeposit(66, 26, 69, 27, ResourceType.Brick);
		AddMirroredDeposit(66, 34, 69, 35, ResourceType.Plastic);
		// The big battery field in the middle, the one worth fighting over.
		AddMirroredDeposit(77, 28, 79, 33, ResourceType.Battery);
	}

	private void AddMirroredDeposit(int x0, int y0, int x1, int y1, ResourceType type)
	{
		FillResource(x0, y0, x1, y1, type);
		FillResource(Width - 1 - x1, y0, Width - 1 - x0, y1, type);
	}

	private void AddRoom(int x, int y)
	{
		int last = RoomSize + 1;
		Fill(x, y, x + last, y + last, TileType.Wall);
		Fill(x + 1, y + 1, x + last - 1, y + last - 1, TileType.Floor);
	}

	/// <summary>Fills an inclusive rectangle.</summary>
	private void Fill(int x0, int y0, int x1, int y1, TileType type)
	{
		for (int y = y0; y <= y1; y++)
			for (int x = x0; x <= x1; x++)
				this[x, y] = type;
	}

	private void SetZone(Zone zone, int x0, int y0, int x1, int y1)
	{
		_zoneBounds[(int)zone] = (x0, y0, x1, y1);
		for (int y = y0; y <= y1; y++)
			for (int x = x0; x <= x1; x++)
				_zones[y * Width + x] = zone;
	}

	private void FillResource(int x0, int y0, int x1, int y1, ResourceType type)
	{
		for (int y = y0; y <= y1; y++)
			for (int x = x0; x <= x1; x++)
				_resources[y * Width + x] = type;
	}
}
