using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>Light radii (in tiles) and fog timing. Everything a player owns lights the tiles around it.</summary>
public static class VisionStats
{
	/// <summary>Visibility is worked out again every this many ticks (5 times a second).</summary>
	public const int VisionTicks = 4;

	/// <summary>The toybox lights both of its nearby deposits from the start.</summary>
	public const int CoreRadius = 10;
	public const int SmallBuildingRadius = 3;
	public const int LargeBuildingRadius = 5;
	public const int ConstructionSiteRadius = 1;
	public const int LampRadius = 9;

	/// <summary>Car headlights: a cone this many tiles ahead of it...</summary>
	public const int HeadlightRange = 10;
	/// <summary>...where cos²(angle to the driving direction) ≥ this / 100 (≈ ±35°).</summary>
	public const int HeadlightCos2Percent = 67;

	/// <summary>Light around a unit, by how big it looks (cars also get their headlight cone).</summary>
	public static int UnitRadius(UnitType type) => type switch
	{
		UnitType.PlasticSoldier => 5,
		UnitType.BrickGolem => 5,
		UnitType.RcCar => 3,
		UnitType.Scout => 7,
		_ => 3, // builders, farmers
	};

	public static int BuildingRadius(Building building)
	{
		if (!building.IsBuilt)
			return ConstructionSiteRadius;
		return building.Type switch
		{
			BuildingType.Core => CoreRadius,
			BuildingType.Lamp => LampRadius,
			_ => building.Width > 1 ? LargeBuildingRadius : SmallBuildingRadius,
		};
	}
}

/// <summary>Toy night light: a cheap building whose only job is to light up the map around it.</summary>
public sealed class Lamp : Building
{
	public Lamp(int x, int y, Direction facing, int owner)
		: base(BuildingType.Lamp, x, y, facing, owner)
	{
	}

	public override bool OutputsToward(Direction direction) => false;
}

/// <summary>What a player last saw of an enemy building (it may have changed or gone since).</summary>
public readonly record struct RememberedBuilding(BuildingType Type, int X, int Y, int Width, int Height, int Owner, Direction Facing);

/// <summary>
/// Fog of war. For each player and tile: Explored (lit at some point, never forgotten) and Visible (lit
/// right now). Light goes out in straight lines from its source and stops at walls (the wall itself is lit).
/// Shapes are precomputed as lists of tiles, each with the ray from the source, so working it out again is
/// only table walks and integer checks.
/// </summary>
public sealed class Vision
{
	/// <summary>One lit tile of a light shape and the tiles its ray passes on the way (excluding both ends).</summary>
	private readonly record struct Ray(int DX, int DY, (int DX, int DY)[] Between);

	private static readonly Dictionary<int, Ray[]> Discs = new();
	private static readonly Ray[][] Cones = new Ray[8][];

	/// <summary>The eight driving directions, clockwise from east (y down).</summary>
	private static readonly (int X, int Y)[] Directions =
	{
		(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1),
	};

	private readonly int _width;
	private readonly int _height;
	private readonly bool[][] _explored;
	private readonly bool[][] _visible;
	private readonly List<RememberedBuilding>[] _remembered;

	public Vision(int width, int height, int players)
	{
		_width = width;
		_height = height;
		_explored = new bool[players][];
		_visible = new bool[players][];
		for (int p = 0; p < players; p++)
		{
			_explored[p] = new bool[width * height];
			_visible[p] = new bool[width * height];
		}
		_remembered = new List<RememberedBuilding>[players];
		for (int p = 0; p < players; p++)
			_remembered[p] = new List<RememberedBuilding>();
	}

	/// <summary>Enemy buildings as the player last saw them, in the order they were seen.</summary>
	public IReadOnlyList<RememberedBuilding> Remembered(int player) => _remembered[player];

	public bool IsExplored(int player, int x, int y) =>
		x >= 0 && y >= 0 && x < _width && y < _height && _explored[player][y * _width + x];

	public bool IsVisible(int player, int x, int y) =>
		x >= 0 && y >= 0 && x < _width && y < _height && _visible[player][y * _width + x];

	/// <summary>Direction index (0 = east, clockwise) closest to a movement vector; -1 when not moving.</summary>
	public static int DirectionIndex(int dx, int dy)
	{
		if (dx == 0 && dy == 0)
			return -1;
		int ax = System.Math.Abs(dx), ay = System.Math.Abs(dy);
		int sx = System.Math.Sign(dx), sy = System.Math.Sign(dy);
		// Within ~22.5° of an axis: straight; otherwise diagonal (tan 22.5° ≈ 0.414 ≈ 5/12).
		if (ay * 12 <= ax * 5)
			sy = 0;
		else if (ax * 12 <= ay * 5)
			sx = 0;
		for (int i = 0; i < Directions.Length; i++)
			if (Directions[i] == (sx, sy))
				return i;
		return -1;
	}

	internal void Recompute(World world)
	{
		var map = world.Map;
		for (int p = 0; p < _visible.Length; p++)
			System.Array.Clear(_visible[p]);

		foreach (var building in world.Buildings)
		{
			int cx = building.X + building.Width / 2, cy = building.Y + building.Height / 2;
			Light(map, building.Owner, cx, cy, Disc(VisionStats.BuildingRadius(building)));
		}
		foreach (var unit in world.Units)
		{
			Light(map, unit.Owner, unit.TileX, unit.TileY, Disc(VisionStats.UnitRadius(unit.Type)));
			if (unit.Type == UnitType.RcCar && unit.LightDirection >= 0)
				Light(map, unit.Owner, unit.TileX, unit.TileY, Cone(unit.LightDirection));
		}

		for (int p = 0; p < _visible.Length; p++)
		{
			var visible = _visible[p];
			var explored = _explored[p];
			for (int i = 0; i < visible.Length; i++)
				if (visible[i])
					explored[i] = true;

			// Memories of what is in sight now are replaced by what is really there.
			_remembered[p].RemoveAll(r => AnyVisible(p, r.X, r.Y, r.Width, r.Height));
			foreach (var b in world.Buildings)
				if (b.Owner != p && AnyVisible(p, b.X, b.Y, b.Width, b.Height))
					_remembered[p].Add(new RememberedBuilding(b.Type, b.X, b.Y, b.Width, b.Height, b.Owner, b.Facing));
		}
	}

	private bool AnyVisible(int player, int x, int y, int width, int height)
	{
		for (int ty = y; ty < y + height; ty++)
			for (int tx = x; tx < x + width; tx++)
				if (IsVisible(player, tx, ty))
					return true;
		return false;
	}

	private void Light(MapLayout map, int player, int x, int y, Ray[] shape)
	{
		var visible = _visible[player];
		foreach (var ray in shape)
		{
			int tx = x + ray.DX, ty = y + ray.DY;
			if (!map.InBounds(tx, ty))
				continue;
			bool blocked = false;
			foreach (var (bx, by) in ray.Between)
			{
				int wx = x + bx, wy = y + by;
				if (!map.InBounds(wx, wy) || map[wx, wy] == TileType.Wall)
				{
					blocked = true;
					break;
				}
			}
			if (!blocked)
				visible[ty * _width + tx] = true;
		}
	}

	private static Ray[] Disc(int radius)
	{
		lock (Discs)
		{
			if (Discs.TryGetValue(radius, out var disc))
				return disc;
			var rays = new List<Ray>();
			for (int dy = -radius; dy <= radius; dy++)
				for (int dx = -radius; dx <= radius; dx++)
					if (dx * dx + dy * dy <= radius * radius)
						rays.Add(new Ray(dx, dy, Line(dx, dy)));
			return Discs[radius] = rays.ToArray();
		}
	}

	private static Ray[] Cone(int direction)
	{
		lock (Cones)
		{
			if (Cones[direction] != null)
				return Cones[direction];
			var (ux, uy) = Directions[direction];
			int range = VisionStats.HeadlightRange;
			var rays = new List<Ray>();
			for (int dy = -range; dy <= range; dy++)
				for (int dx = -range; dx <= range; dx++)
				{
					int length2 = dx * dx + dy * dy, dot = dx * ux + dy * uy;
					if (length2 == 0 || length2 > range * range || dot <= 0)
						continue;
					if (100L * dot * dot >= (long)VisionStats.HeadlightCos2Percent * length2 * (ux * ux + uy * uy))
						rays.Add(new Ray(dx, dy, Line(dx, dy)));
				}
			return Cones[direction] = rays.ToArray();
		}
	}

	/// <summary>Bresenham from (0,0) to (dx,dy): the tiles strictly between.</summary>
	private static (int DX, int DY)[] Line(int dx, int dy)
	{
		var tiles = new List<(int, int)>();
		int x = 0, y = 0;
		int ax = System.Math.Abs(dx), ay = -System.Math.Abs(dy);
		int sx = System.Math.Sign(dx), sy = System.Math.Sign(dy);
		int error = ax + ay;
		while (true)
		{
			if (x == dx && y == dy)
				break;
			int e2 = 2 * error;
			if (e2 >= ay) { error += ay; x += sx; }
			if (e2 <= ax) { error += ax; y += sy; }
			if (x != dx || y != dy)
				tiles.Add((x, y));
		}
		return tiles.ToArray();
	}
}
