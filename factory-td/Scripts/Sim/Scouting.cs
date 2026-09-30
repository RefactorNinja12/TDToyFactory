using System.Collections.Generic;

namespace FactoryTD.Sim;

public static class ScoutStats
{
	/// <summary>How long a scout runs from an enemy it has seen.</summary>
	public const int FleeTicks = World.TicksPerSecond * 3;
	/// <summary>Scouts don't pick fog-edge tiles this close (in tiles) to another scout's target.</summary>
	public const int SpreadTiles = 6;
}

/// <summary>
/// Scouts search wide, not deep. Each picks the fog-edge tile (an explored floor tile next to an
/// unexplored one) that is the shortest walk from home over explored ground, so the known area grows in
/// rings and scouts walk along the edge of the fog. They keep apart from each other's targets and run
/// from enemy units they can see. With nothing left to explore they go home.
/// </summary>
public sealed partial class World
{
	private int[][] _homeDistance;    // per player: steps from home over explored floor (-1 = no way)
	private long[] _homeDistanceStamp; // the vision recompute it was worked out for

	private void TickScout(Unit scout)
	{
		scout.PrevX = scout.X;
		scout.PrevY = scout.Y;
		scout.MoveX = scout.MoveY = 0;
		if (FullVision || Winner >= 0)
			return;

		if (scout.FleeTicks == 0)
		{
			var enemy = FindNearestEnemy(scout.Owner, scout.X, scout.Y, VisionStats.UnitRadius(UnitType.Scout) * UnitStats.SubTile);
			if (enemy != null && CanSee(scout.Owner, enemy))
			{
				scout.FleeTicks = ScoutStats.FleeTicks;
				scout.FleeX = scout.X - enemy.X;
				scout.FleeY = scout.Y - enemy.Y;
				if (scout.FleeX == 0 && scout.FleeY == 0)
					scout.FleeX = scout.Owner == 0 ? -1 : 1; // right on top of it: run towards home
				ForgetTarget(scout);
			}
		}
		if (scout.FleeTicks > 0)
		{
			scout.FleeTicks--;
			Flee(scout);
			return;
		}

		bool check = TickCount % VisionStats.VisionTicks == 0;
		if (scout.ScoutTargetX < 0 || (check && !IsFrontier(scout.Owner, scout.ScoutTargetX, scout.ScoutTargetY)))
			ChooseTarget(scout);
		if (scout.ScoutTargetX < 0)
		{
			var core = _cores[scout.Owner];
			if (core != null)
				WalkTo(scout, core);
			return;
		}
		WalkToTile(scout, scout.ScoutTargetX, scout.ScoutTargetY);
	}

	private static void ForgetTarget(Unit scout)
	{
		scout.ScoutTargetX = scout.ScoutTargetY = -1;
		scout.Path = null;
		scout.PathTarget = null;
	}

	/// <summary>Straight away from the enemy; if a wall is in the way, the nearest direction that is free.</summary>
	private void Flee(Unit scout)
	{
		int length = System.Math.Max(1, IntMath.Sqrt(scout.FleeX * scout.FleeX + scout.FleeY * scout.FleeY));
		int fx = scout.FleeX * UnitStats.SubTile * 2 / length, fy = scout.FleeY * UnitStats.SubTile * 2 / length;
		// Straight away, then 45° and 90° to either side.
		(int X, int Y)[] tries = { (fx, fy), (fx - fy, fy + fx), (fx + fy, fy - fx), (-fy, fx), (fy, -fx) };
		foreach (var (dx, dy) in tries)
			if (TryStep(scout, scout.X + dx, scout.Y + dy))
				return;
	}

	private void ChooseTarget(Unit scout)
	{
		ForgetTarget(scout);
		var home = HomeDistance(scout.Owner);
		int width = Map.Width;
		int bestIndex = -1, bestScore = int.MaxValue;
		for (int index = 0; index < home.Length; index++)
		{
			int distance = home[index];
			if (distance < 0 || distance >= bestScore)
				continue;
			int x = index % width, y = index / width;
			// Wide before deep: near home counts most. Half the way from the scout is added, so it keeps
			// following the fog edge it is at instead of running back and forth across the base.
			int score = distance + (System.Math.Abs(x - scout.TileX) + System.Math.Abs(y - scout.TileY)) / 2;
			if (score >= bestScore || !IsFrontier(scout.Owner, x, y) || NearOtherScoutsTarget(scout, x, y))
				continue;
			bestIndex = index;
			bestScore = score;
		}
		if (bestIndex >= 0)
		{
			scout.ScoutTargetX = bestIndex % width;
			scout.ScoutTargetY = bestIndex / width;
		}
	}

	private bool NearOtherScoutsTarget(Unit scout, int x, int y)
	{
		const int spread2 = ScoutStats.SpreadTiles * ScoutStats.SpreadTiles;
		foreach (var other in _units)
		{
			if (other == scout || other.Type != UnitType.Scout || other.Owner != scout.Owner || other.ScoutTargetX < 0)
				continue;
			int dx = other.ScoutTargetX - x, dy = other.ScoutTargetY - y;
			if (dx * dx + dy * dy < spread2)
				return true;
		}
		return false;
	}

	/// <summary>An explored floor tile next to an unexplored one: where the fog begins.</summary>
	internal bool IsFrontier(int player, int x, int y)
	{
		if (!Map.InBounds(x, y) || Map[x, y] != TileType.Floor || !IsExplored(player, x, y))
			return false;
		foreach (var (dx, dy) in Steps)
		{
			int nx = x + dx, ny = y + dy;
			if (Map.InBounds(nx, ny) && !IsExplored(player, nx, ny))
				return true;
		}
		return false;
	}

	/// <summary>Walking steps from the player's toybox over explored floor, per tile (-1 = can't get there).</summary>
	internal int[] HomeDistance(int player)
	{
		_homeDistance ??= new int[_players.Length][];
		_homeDistanceStamp ??= new long[_players.Length];
		long stamp = TickCount / VisionStats.VisionTicks + 1;
		if (_homeDistance[player] != null && _homeDistanceStamp[player] == stamp)
			return _homeDistance[player];

		int width = Map.Width;
		var distance = _homeDistance[player] ??= new int[width * Map.Height];
		System.Array.Fill(distance, -1);
		_homeDistanceStamp[player] = stamp;
		var core = _cores[player];
		if (core == null)
			return distance;
		var queue = new Queue<int>();
		for (int y = core.Y; y < core.Y + core.Height; y++)
			for (int x = core.X; x < core.X + core.Width; x++)
			{
				distance[y * width + x] = 0;
				queue.Enqueue(y * width + x);
			}
		while (queue.Count > 0)
		{
			int index = queue.Dequeue();
			int x = index % width, y = index / width;
			foreach (var (dx, dy) in Steps)
			{
				int nx = x + dx, ny = y + dy;
				if (!Map.InBounds(nx, ny) || Map[nx, ny] != TileType.Floor || !IsExplored(player, nx, ny))
					continue;
				int next = ny * width + nx;
				if (distance[next] >= 0)
					continue;
				distance[next] = distance[index] + 1;
				queue.Enqueue(next);
			}
		}
		return distance;
	}

	/// <summary>Like WalkTo for a building, but to one tile.</summary>
	private void WalkToTile(Unit unit, int x, int y)
	{
		if (unit.TileX == x && unit.TileY == y)
			return;
		if (unit.Path == null || unit.Path.Count == 0 || unit.Path[^1] != (x, y))
		{
			unit.Path = FindPathToTile(unit.TileX, unit.TileY, x, y);
			unit.PathIndex = 0;
			unit.PathTarget = null;
			if (unit.Path == null)
			{
				ForgetTarget(unit);
				return;
			}
		}
		if (unit.PathIndex < unit.Path.Count)
		{
			const int half = UnitStats.SubTile / 2;
			var (tx, ty) = unit.Path[unit.PathIndex];
			int goalX = tx * UnitStats.SubTile + half, goalY = ty * UnitStats.SubTile + half;
			TryStep(unit, goalX, goalY);
			if (unit.X == goalX && unit.Y == goalY)
				unit.PathIndex++;
		}
	}

	private List<(int X, int Y)> FindPathToTile(int fromX, int fromY, int toX, int toY)
	{
		var previous = new Dictionary<(int, int), (int, int)> { [(fromX, fromY)] = (fromX, fromY) };
		var queue = new Queue<(int X, int Y)>();
		queue.Enqueue((fromX, fromY));
		while (queue.Count > 0)
		{
			var tile = queue.Dequeue();
			if (tile == (toX, toY))
			{
				var path = new List<(int X, int Y)>();
				for (var at = tile; at != (fromX, fromY); at = previous[at])
					path.Add(at);
				path.Reverse();
				return path;
			}
			foreach (var (dx, dy) in Steps)
			{
				var next = (X: tile.X + dx, Y: tile.Y + dy);
				if (previous.ContainsKey(next) || !Map.InBounds(next.X, next.Y) || Map[next.X, next.Y] != TileType.Floor)
					continue;
				previous[next] = tile;
				queue.Enqueue(next);
			}
		}
		return null;
	}
}
