using System;
using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>
/// Breadth-first searches over the tile grid, shared by walking workers, scouts and the map checks.
/// Neighbours are always visited in the same order (east, south, west, north), so paths are the same on
/// every machine. Arrays instead of dictionaries: one allocation per search, no hashing.
/// </summary>
internal static class GridSearch
{
	private static readonly (int DX, int DY)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

	/// <summary>
	/// The shortest walk from (fromX, fromY) to the first tile where <paramref name="isGoal"/> holds, over
	/// tiles where <paramref name="walkable"/> holds. The path leaves out the start and ends on the goal;
	/// empty if the start already is a goal, null if no goal can be reached.
	/// </summary>
	public static List<(int X, int Y)> PathTo(int width, int height, int fromX, int fromY,
		Func<int, int, bool> walkable, Func<int, int, bool> isGoal)
	{
		if (isGoal(fromX, fromY))
			return new List<(int X, int Y)>();
		var previous = new int[width * height];
		Array.Fill(previous, -1);
		int start = fromY * width + fromX;
		previous[start] = start;
		var queue = new Queue<int>();
		queue.Enqueue(start);
		while (queue.Count > 0)
		{
			int tile = queue.Dequeue();
			int x = tile % width, y = tile / width;
			foreach (var (dx, dy) in Steps)
			{
				int nx = x + dx, ny = y + dy;
				if (nx < 0 || ny < 0 || nx >= width || ny >= height)
					continue;
				int next = ny * width + nx;
				if (previous[next] >= 0 || !walkable(nx, ny))
					continue;
				previous[next] = tile;
				if (isGoal(nx, ny))
				{
					var path = new List<(int X, int Y)>();
					for (int at = next; at != start; at = previous[at])
						path.Add((at % width, at / width));
					path.Reverse();
					return path;
				}
				queue.Enqueue(next);
			}
		}
		return null;
	}

	/// <summary>
	/// Steps from the nearest source to every tile over walkable tiles (-1 where there is no way), written
	/// into <paramref name="distance"/>. Returns how many tiles were reached (sources included).
	/// </summary>
	public static int Distances(int width, int height, int[] distance, IEnumerable<int> sources, Func<int, int, bool> walkable)
	{
		Array.Fill(distance, -1);
		var queue = new Queue<int>();
		foreach (int source in sources)
		{
			if (distance[source] >= 0)
				continue;
			distance[source] = 0;
			queue.Enqueue(source);
		}
		int reached = queue.Count;
		while (queue.Count > 0)
		{
			int tile = queue.Dequeue();
			int x = tile % width, y = tile / width;
			foreach (var (dx, dy) in Steps)
			{
				int nx = x + dx, ny = y + dy;
				if (nx < 0 || ny < 0 || nx >= width || ny >= height)
					continue;
				int next = ny * width + nx;
				if (distance[next] >= 0 || !walkable(nx, ny))
					continue;
				distance[next] = distance[tile] + 1;
				reached++;
				queue.Enqueue(next);
			}
		}
		return reached;
	}
}
