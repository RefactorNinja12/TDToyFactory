using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>Big toys lying in the rooms.</summary>
public enum ObstacleKind : byte
{
	TeddyBear,
	AbcBlocks,
	RagDoll,
	Sock,
}

/// <summary>A big toy on the map: its footprint in tiles (TileType.Obstacle).</summary>
public readonly record struct Obstacle(ObstacleKind Kind, int X, int Y, int Width, int Height);

/// <summary>
/// Obstacles: big toys (teddy bear, stacked ABC blocks, rag doll, a sock) that block walking and building like
/// walls do (light passes them). Placed at random from a seed in the left room and mirrored to the right,
/// never in the base area, on or next to deposits, in the door lane or the battery corner; each keeps a
/// ring of floor around it and every floor tile has to stay reachable.
/// </summary>
public sealed partial class MapLayout
{
	public const int DefaultSeed = 7;

	private readonly List<Obstacle> _obstacles = new();

	public IReadOnlyList<Obstacle> Obstacles => _obstacles;

	/// <summary>The toys to place in each room, in this order.</summary>
	private static readonly (ObstacleKind Kind, int Width, int Height)[] RoomToys =
	{
		(ObstacleKind.TeddyBear, 4, 4),
		(ObstacleKind.RagDoll, 3, 4),
		(ObstacleKind.AbcBlocks, 3, 3),
		(ObstacleKind.AbcBlocks, 3, 3),
		(ObstacleKind.Sock, 5, 2), // last, so adding it didn't move the others
	};

	/// <summary>Areas of the left room (inclusive, interior x/y 1..60) where no toy may lie.</summary>
	private static readonly (int X0, int Y0, int X1, int Y1)[] KeepOut =
	{
		(1, 14, 27, 46),   // the base around the toybox
		(36, 41, 50, 53),  // the battery patch and the corner next to it
		(20, 26, 60, 35),  // the lane to the door
	};

	private void AddObstacles(int seed)
	{
		uint state = (uint)seed * 2654435761u + 1;
		int Next(int maxExclusive)
		{
			// xorshift32: the same numbers on every machine (lockstep)
			state ^= state << 13;
			state ^= state >> 17;
			state ^= state << 5;
			return (int)(state % (uint)maxExclusive);
		}

		foreach (var (kind, w, h) in RoomToys)
		{
			for (int attempt = 0; attempt < 400; attempt++)
			{
				int x = 2 + Next(RoomSize - w - 2), y = 2 + Next(RoomSize - h - 2);
				if (!CanHold(x, y, w, h))
					continue;
				int mx = Width - 1 - x - (w - 1);
				SetObstacle(x, y, w, h, TileType.Obstacle);
				SetObstacle(mx, y, w, h, TileType.Obstacle);
				if (AllFloorConnected())
				{
					_obstacles.Add(new Obstacle(kind, x, y, w, h));
					_obstacles.Add(new Obstacle(kind, mx, y, w, h));
					break;
				}
				SetObstacle(x, y, w, h, TileType.Floor);
				SetObstacle(mx, y, w, h, TileType.Floor);
			}
		}
	}

	private bool CanHold(int x, int y, int w, int h)
	{
		foreach (var (x0, y0, x1, y1) in KeepOut)
			if (x <= x1 && x + w - 1 >= x0 && y <= y1 && y + h - 1 >= y0)
				return false;
		// A ring of plain floor around it, and no deposit within two tiles.
		for (int ty = y - 2; ty < y + h + 2; ty++)
			for (int tx = x - 2; tx < x + w + 2; tx++)
			{
				if (!InBounds(tx, ty))
					return false;
				if (GetResource(tx, ty) != ResourceType.None)
					return false;
				bool ring = tx >= x - 1 && tx <= x + w && ty >= y - 1 && ty <= y + h;
				if (ring && this[tx, ty] != TileType.Floor)
					return false;
			}
		return true;
	}

	private void SetObstacle(int x, int y, int w, int h, TileType type)
	{
		for (int ty = y; ty < y + h; ty++)
			for (int tx = x; tx < x + w; tx++)
				this[tx, ty] = type;
	}

	/// <summary>Whether every floor tile can be walked to from every other one.</summary>
	internal bool AllFloorConnected()
	{
		int total = 0, start = -1;
		for (int i = 0; i < _tiles.Length; i++)
			if (_tiles[i] == TileType.Floor)
			{
				total++;
				if (start < 0)
					start = i;
			}
		if (start < 0)
			return true;
		var seen = new bool[_tiles.Length];
		var queue = new Queue<int>();
		seen[start] = true;
		queue.Enqueue(start);
		int reached = 0;
		while (queue.Count > 0)
		{
			int i = queue.Dequeue();
			reached++;
			int x = i % Width, y = i / Width;
			foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
			{
				int nx = x + dx, ny = y + dy;
				if (!InBounds(nx, ny))
					continue;
				int n = ny * Width + nx;
				if (!seen[n] && _tiles[n] == TileType.Floor)
				{
					seen[n] = true;
					queue.Enqueue(n);
				}
			}
		}
		return reached == total;
	}
}
