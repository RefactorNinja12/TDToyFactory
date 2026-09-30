using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>
/// Distance-to-target for every tile, towards one player's core. Units walk downhill.
/// Walls are impassable; buildings are expensive but never blocking (they get smashed later).
/// Integer costs only, so it's identical on every machine.
/// </summary>
public sealed class FlowField
{
	public const int Unreachable = int.MaxValue;

	private const int StraightCost = 10;
	private const int DiagonalCost = 14;
	private const int BuildingPenalty = 60; // a building tile costs 7x a floor tile

	private static readonly (int dx, int dy, int cost)[] Neighbours =
	{
		(1, 0, StraightCost), (0, 1, StraightCost), (-1, 0, StraightCost), (0, -1, StraightCost),
		(1, 1, DiagonalCost), (-1, 1, DiagonalCost), (-1, -1, DiagonalCost), (1, -1, DiagonalCost),
	};

	private readonly int _width;
	private readonly int _height;
	private readonly int[] _distance;

	public FlowField(int width, int height)
	{
		_width = width;
		_height = height;
		_distance = new int[width * height];
	}

	public int Distance(int x, int y) =>
		x >= 0 && y >= 0 && x < _width && y < _height ? _distance[y * _width + x] : Unreachable;

	/// <summary>
	/// Dijkstra outwards from every tile of <paramref name="target"/>, for <paramref name="owner"/>'s units:
	/// enemy buildings cost extra (they have to be shot down), the owner's own buildings don't.
	/// </summary>
	public void Build(World world, Building target, int owner)
	{
		System.Array.Fill(_distance, Unreachable);
		var queue = new PriorityQueue<int, int>();

		for (int y = target.Y; y < target.Y + target.Height; y++)
		{
			for (int x = target.X; x < target.X + target.Width; x++)
			{
				_distance[y * _width + x] = 0;
				queue.Enqueue(y * _width + x, 0);
			}
		}

		while (queue.TryDequeue(out int index, out int distance))
		{
			if (distance > _distance[index])
				continue;
			int x = index % _width, y = index / _width;

			foreach (var (dx, dy, cost) in Neighbours)
			{
				int nx = x + dx, ny = y + dy;
				if (!IsWalkable(world, nx, ny))
					continue;
				// No cutting corners past walls.
				if (dx != 0 && dy != 0 && (!IsWalkable(world, x + dx, y) || !IsWalkable(world, x, y + dy)))
					continue;

				var building = world.GetBuilding(nx, ny);
				int step = cost + (building != null && building.Owner != owner ? BuildingPenalty : 0);
				int next = distance + step;
				int nIndex = ny * _width + nx;
				if (next < _distance[nIndex])
				{
					_distance[nIndex] = next;
					queue.Enqueue(nIndex, next);
				}
			}
		}
	}

	/// <summary>The neighbouring tile with the lowest distance, or the tile itself if none is lower.</summary>
	public (int X, int Y) NextTile(int x, int y)
	{
		int best = Distance(x, y);
		(int X, int Y) result = (x, y);
		foreach (var (dx, dy, _) in Neighbours)
		{
			int d = Distance(x + dx, y + dy);
			if (d < best && (dx == 0 || dy == 0 || (Distance(x + dx, y) != Unreachable && Distance(x, y + dy) != Unreachable)))
			{
				best = d;
				result = (x + dx, y + dy);
			}
		}
		return result;
	}

	private static bool IsWalkable(World world, int x, int y) =>
		world.Map.InBounds(x, y) && world.Map[x, y] == TileType.Floor;
}
