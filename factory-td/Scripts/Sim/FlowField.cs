using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>
/// Distance-to-target for every tile, towards one player's core. Units walk downhill.
/// Map walls and the owner's own walls are impassable; enemy walls cost <c>wallPenalty</c> (smashed through);
/// other buildings are expensive but never blocking (they get smashed later).
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

	// Neighbours mirrored east-west: player 1's units break ties the mirrored way, so mirrored armies walk mirrored.
	private static readonly (int dx, int dy, int cost)[] MirroredNeighbours =
		System.Array.ConvertAll(Neighbours, n => (-n.dx, n.dy, n.cost));

	private (int dx, int dy, int cost)[] _steps = Neighbours;

	private readonly int _width;
	private readonly int _height;
	private readonly int[] _distance;
	private readonly bool[] _open; // floor with no wall standing on it: diagonal steps may pass its corner

	public FlowField(int width, int height)
	{
		_width = width;
		_height = height;
		_distance = new int[width * height];
		_open = new bool[width * height];
	}

	public int Distance(int x, int y) =>
		x >= 0 && y >= 0 && x < _width && y < _height ? _distance[y * _width + x] : Unreachable;

	/// <summary>
	/// Dijkstra outwards from every tile of <paramref name="target"/>, for <paramref name="owner"/>'s units:
	/// enemy buildings cost extra (they have to be shot down), the owner's own buildings don't.
	/// </summary>
	public void Build(World world, Building target, int owner, int wallPenalty = WallStats.SealedWallPenalty) =>
		Build(world, owner, wallPenalty, (x, y) => x >= target.X && x < target.X + target.Width && y >= target.Y && y < target.Y + target.Height);

	/// <summary>Same, from every tile where <paramref name="isTarget"/> holds (e.g. every powered tile).</summary>
	public void Build(World world, int owner, int wallPenalty, System.Func<int, int, bool> isTarget)
	{
		_steps = owner == 1 ? MirroredNeighbours : Neighbours;
		System.Array.Fill(_distance, Unreachable);
		for (int i = 0; i < _open.Length; i++)
			_open[i] = world.Map[i % _width, i / _width] == TileType.Floor && world.GetBuilding(i % _width, i / _width) is not Wall { IsBuilt: true };
		var queue = new PriorityQueue<int, int>();

		for (int y = 0; y < _height; y++)
		{
			for (int x = 0; x < _width; x++)
			{
				if (!isTarget(x, y))
					continue;
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
				if (!world.Map.InBounds(nx, ny) || world.Map[nx, ny] != TileType.Floor)
					continue;
				// No cutting corners past walls (map walls or built ones).
				if (dx != 0 && dy != 0 && (!_open[y * _width + x + dx] || !_open[(y + dy) * _width + x]))
					continue;

				var building = world.GetBuilding(nx, ny);
				int step = cost;
				if (building is Wall { IsBuilt: true } wall)
				{
					if (wall.Owner == owner && wall.Blocks(owner))
						continue; // never through its own wall
					if (wall.Owner != owner)
						step += wallPenalty;
				}
				else if (building != null && building.Owner != owner && building is not Trap)
				{
					step += BuildingPenalty;
				}
				int next = distance + step;
				int nIndex = ny * _width + nx;
				if (next > distance && next < _distance[nIndex])
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
		foreach (var (dx, dy, _) in _steps)
		{
			int d = Distance(x + dx, y + dy);
			if (d < best && (dx == 0 || dy == 0 || (IsOpen(x + dx, y) && IsOpen(x, y + dy))))
			{
				best = d;
				result = (x + dx, y + dy);
			}
		}
		return result;
	}

	private bool IsOpen(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height && _open[y * _width + x];
}
