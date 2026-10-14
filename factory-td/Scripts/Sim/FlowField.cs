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
	private readonly int[] _enter; // extra cost of stepping onto the tile, or Blocked (worked out once per build)
	private readonly DistanceQueue _queue = new();
	private const int Blocked = -1;

	public FlowField(int width, int height)
	{
		_width = width;
		_height = height;
		_distance = new int[width * height];
		_open = new bool[width * height];
		_enter = new int[width * height];
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
		var queue = _queue;
		queue.Clear();
		for (int y = 0, i = 0; y < _height; y++)
		{
			for (int x = 0; x < _width; x++, i++)
			{
				bool floor = world.Map[x, y] == TileType.Floor;
				var building = floor ? world.GetBuilding(x, y) : null;
				_open[i] = floor && building is not Wall { IsBuilt: true };
				_enter[i] = !floor ? Blocked : EnterCost(building, owner, wallPenalty);
				if (!isTarget(x, y))
					continue;
				_distance[i] = 0;
				queue.Enqueue(i, 0);
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
				if (nx < 0 || ny < 0 || nx >= _width || ny >= _height)
					continue;
				int nIndex = ny * _width + nx;
				int enter = _enter[nIndex];
				if (enter == Blocked)
					continue;
				// No cutting corners past walls (map walls or built ones).
				if (dx != 0 && dy != 0 && (!_open[index + dx] || !_open[index + dy * _width]))
					continue;
				int next = distance + cost + enter;
				if (next < _distance[nIndex])
				{
					_distance[nIndex] = next;
					queue.Enqueue(nIndex, next);
				}
			}
		}
	}

	/// <summary>
	/// What stepping onto a floor tile with this building costs on top of the step: its own standing wall blocks, an enemy
	/// one costs <paramref name="wallPenalty"/>, any other enemy building (not a trap, which it doesn't see) is expensive.
	/// </summary>
	private static int EnterCost(Building building, int owner, int wallPenalty)
	{
		if (building is Wall { IsBuilt: true } wall)
			return wall.Owner != owner ? wallPenalty : wall.Blocks(owner) ? Blocked : 0;
		return building != null && building.Owner != owner && building is not Trap ? BuildingPenalty : 0;
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

/// <summary>
/// A priority queue for Dijkstra with small step costs: buckets for the distances just ahead (a ring), and a heap for
/// the far ones (a step through a sealed wall). Takes out the lowest distance first, like a heap, only much faster.
/// </summary>
internal sealed class DistanceQueue
{
	private const int Window = 256; // more than any step but the wall penalties
	private readonly List<int>[] _buckets = new List<int>[Window];
	private readonly PriorityQueue<int, int> _far = new();
	private int _current, _near;

	public DistanceQueue()
	{
		for (int i = 0; i < Window; i++)
			_buckets[i] = new List<int>();
	}

	public void Clear()
	{
		foreach (var bucket in _buckets)
			bucket.Clear();
		_far.Clear();
		_current = _near = 0;
	}

	public void Enqueue(int item, int distance)
	{
		if (distance - _current < Window)
		{
			_buckets[distance % Window].Add(item);
			_near++;
		}
		else
		{
			_far.Enqueue(item, distance);
		}
	}

	public bool TryDequeue(out int item, out int distance)
	{
		while (true)
		{
			if (_near == 0)
			{
				if (!_far.TryPeek(out _, out int next))
				{
					item = distance = 0;
					return false;
				}
				_current = next;
			}
			// Far ones that have come within the window move into their bucket.
			while (_far.TryPeek(out int far, out int farDistance) && farDistance - _current < Window)
			{
				_far.Dequeue();
				_buckets[farDistance % Window].Add(far);
				_near++;
			}
			var bucket = _buckets[_current % Window];
			if (bucket.Count > 0)
			{
				item = bucket[^1];
				bucket.RemoveAt(bucket.Count - 1);
				_near--;
				distance = _current;
				return true;
			}
			_current++;
		}
	}
}
