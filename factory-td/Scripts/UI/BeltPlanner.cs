using System;
using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>One tile of a planned belt: a new conveyor, or an existing conveyor of ours that becomes a junction.</summary>
public readonly record struct BeltStep(int X, int Y, Direction Facing, bool Junction);

/// <summary>
/// Plans a belt for the player from one tile to another (click, then click again): the cheapest way over
/// free floor, where every tile costs the same and every turn a little extra, so belts run in straight
/// lines instead of staircases. It never goes over buildings; it can cross the player's own belts that run
/// straight across its way (that conveyor becomes a junction) and pass straight through junctions.
/// Clicking a building starts or ends the belt next to it; a belt that ends at a building points into it.
/// </summary>
public static class BeltPlanner
{
	private const int StepCost = 10, TurnCost = 3, CrossingCost = 6;

	/// <summary>The belt from <paramref name="from"/> to <paramref name="to"/>, or null if there is no way.</summary>
	public static List<BeltStep> Plan(World world, int owner, (int X, int Y) from, (int X, int Y) to)
	{
		var map = world.Map;
		if (!map.InBounds(from.X, from.Y) || !map.InBounds(to.X, to.Y))
			return null;
		int width = map.Width, size = width * map.Height;

		// Free floor for a belt, worked out once per tile (0 = not yet, 1 = free, 2 = not).
		var known = new byte[size];
		bool Free(int x, int y)
		{
			if (!map.InBounds(x, y))
				return false;
			ref byte k = ref known[y * width + x];
			if (k == 0)
				k = world.CheckLocation(BuildingType.Conveyor, x, y, owner) == PlaceError.None ? (byte)1 : (byte)2;
			return k == 1;
		}

		// Where it may start, and where it ends (with the way the last belt points, if a building is the goal).
		var starts = Ends(world, from, Free);
		var goals = new Dictionary<int, Direction?>();
		foreach (var (x, y, into) in Ends(world, to, Free))
			goals[y * width + x] = into;
		if (starts.Count == 0 || goals.Count == 0)
			return null;

		// Dijkstra over (tile, way in): the way matters for turn costs and for crossings.
		var cost = new int[size * 4];
		Array.Fill(cost, int.MaxValue);
		var previous = new int[size * 4];
		var crossed = new Dictionary<int, List<int>>(); // state -> the junction tiles jumped to reach it
		var queue = new PriorityQueue<int, (int Cost, int Order)>();
		int order = 0;
		foreach (var (x, y, _) in starts)
			foreach (var d in Ways)
			{
				int state = (y * width + x) * 4 + (int)d;
				cost[state] = 0;
				previous[state] = -1;
				queue.Enqueue(state, (0, order++));
			}

		int reached = -1;
		while (queue.TryDequeue(out int state, out var priority))
		{
			if (priority.Cost != cost[state])
				continue;
			int tile = state / 4;
			if (goals.ContainsKey(tile))
			{
				reached = state;
				break;
			}
			var way = (Direction)(state % 4);
			int x = tile % width, y = tile / width;
			foreach (var d in Ways)
			{
				int nx = x + d.DX(), ny = y + d.DY();
				int extra = d == way || previous[state] == -1 ? 0 : TurnCost;
				var jumped = new List<int>();
				// Cross our own belts running across (and junctions) straight on, as many as there are in a row.
				while (map.InBounds(nx, ny) && CanCross(world, owner, nx, ny, d))
				{
					jumped.Add(ny * width + nx);
					nx += d.DX();
					ny += d.DY();
				}
				if (!Free(nx, ny))
					continue;
				int next = (ny * width + nx) * 4 + (int)d;
				int newCost = cost[state] + StepCost * (jumped.Count + 1) + extra + CrossingCost * jumped.Count;
				if (newCost >= cost[next])
					continue;
				cost[next] = newCost;
				previous[next] = state;
				if (jumped.Count > 0)
					crossed[next] = jumped;
				else
					crossed.Remove(next);
				queue.Enqueue(next, (newCost, order++));
			}
		}
		if (reached < 0)
			return null;

		// Walk back, putting the crossed tiles in between.
		var tiles = new List<(int Tile, bool Junction)>();
		for (int state = reached; state >= 0; state = previous[state])
		{
			tiles.Add((state / 4, false));
			if (crossed.TryGetValue(state, out var jumped))
				for (int i = jumped.Count - 1; i >= 0; i--)
					tiles.Add((jumped[i], true));
		}
		tiles.Reverse();

		var plan = new List<BeltStep>(tiles.Count);
		for (int i = 0; i < tiles.Count; i++)
		{
			int x = tiles[i].Tile % width, y = tiles[i].Tile / width;
			Direction facing;
			if (i + 1 < tiles.Count)
				facing = Between(x, y, tiles[i + 1].Tile % width, tiles[i + 1].Tile / width);
			else if (goals[tiles[i].Tile] is { } into)
				facing = into;                                 // the last belt points into the goal building
			else
				facing = i > 0 ? Between(tiles[i - 1].Tile % width, tiles[i - 1].Tile / width, x, y) : Direction.East;
			plan.Add(new BeltStep(x, y, facing, tiles[i].Junction));
		}
		return plan;
	}

	/// <summary>The commands that build a plan: new conveyors, and existing conveyors swapped for junctions.</summary>
	public static List<PlayerCommand> Commands(IEnumerable<BeltStep> plan, int owner)
	{
		var commands = new List<PlayerCommand>();
		foreach (var step in plan)
		{
			if (step.Junction)
			{
				commands.Add(PlayerCommand.Remove(owner, step.X, step.Y));
				commands.Add(PlayerCommand.Place(owner, BuildingType.Junction, step.X, step.Y, Direction.East));
			}
			else
				commands.Add(PlayerCommand.Place(owner, BuildingType.Conveyor, step.X, step.Y, step.Facing));
		}
		return commands;
	}

	private static readonly Direction[] Ways = { Direction.East, Direction.South, Direction.West, Direction.North };

	/// <summary>
	/// The tiles a belt may start or end on for a click: the tile itself if it is free floor, or the free tiles
	/// next to the building there (then a belt ending there points into the building).
	/// </summary>
	private static List<(int X, int Y, Direction? Into)> Ends(World world, (int X, int Y) at, Func<int, int, bool> free)
	{
		var ends = new List<(int, int, Direction?)>();
		if (free(at.X, at.Y))
		{
			ends.Add((at.X, at.Y, null));
			return ends;
		}
		var building = world.GetBuilding(at.X, at.Y);
		if (building == null)
			return ends;
		var (w, h) = BuildingRules.Size(building.Type);
		for (int y = building.Y - 1; y <= building.Y + h; y++)
			for (int x = building.X - 1; x <= building.X + w; x++)
			{
				bool inside = x >= building.X && x < building.X + w && y >= building.Y && y < building.Y + h;
				bool corner = (x == building.X - 1 || x == building.X + w) && (y == building.Y - 1 || y == building.Y + h);
				if (inside || corner || !free(x, y))
					continue;
				var into = x < building.X ? Direction.East : x >= building.X + w ? Direction.West : y < building.Y ? Direction.South : Direction.North;
				ends.Add((x, y, into));
			}
		return ends;
	}

	/// <summary>
	/// Whether a belt moving <paramref name="moving"/> can cross the tile straight on: one of the owner's
	/// junctions, or one of its conveyors running across in the middle of a straight line (so the junction
	/// that replaces it doesn't break a turn).
	/// </summary>
	public static bool CanCross(World world, int owner, int x, int y, Direction moving)
	{
		var building = world.GetBuilding(x, y);
		if (building == null || building.Owner != owner || !building.IsBuilt)
			return false;
		if (building.Type == BuildingType.Junction)
			return true;
		if (building.Type != BuildingType.Conveyor || building.Facing == moving || building.Facing == moving.Opposite())
			return false;
		var behind = world.GetBuilding(x - building.Facing.DX(), y - building.Facing.DY());
		return behind != null && (behind.Type == BuildingType.Junction ||
			behind.Type == BuildingType.Conveyor && behind.Facing == building.Facing);
	}

	private static Direction Between(int x0, int y0, int x1, int y1) =>
		x1 > x0 ? Direction.East : x1 < x0 ? Direction.West : y1 > y0 ? Direction.South : Direction.North;
}
