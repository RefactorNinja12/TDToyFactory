using System.Collections.Generic;

namespace FactoryTD.Sim;

// BotPlayer partial: belt routing between planned buildings (breadth-first, junctions over own belts).
public sealed partial class BotPlayer
{
	// ---------------------------------------------------------------------------------------------
	// Belt routing (absolute coordinates).

	private static readonly Direction[] Directions = { Direction.East, Direction.South, Direction.West, Direction.North };

	/// <summary>
	/// Plans a belt from a free tile next to <paramref name="source"/> to a tile next to
	/// <paramref name="target"/>, ending pointing into the target. Breadth-first, so the shortest path.
	/// It never runs over deposits (kept free for extractors) and never passes a tile that some other
	/// building would push items into, so the belt carries only what the source makes and can't jam.
	/// If there is no way through, nothing is planned.
	/// </summary>
	private void Route(World world, Step source, Step target)
	{
		if (_reserving)
			return; // routes are worked out when the module is actually built
		var start = (-1, -1);
		foreach (var d in Directions)
		{
			var tile = (source.X + d.DX(), source.Y + d.DY());
			if (IsRoutable(world, tile, source, target))
			{
				start = tile;
				break;
			}
		}
		if (start.Item1 < 0)
			return;

		var previous = new Dictionary<(int, int), (int, int)> { [start] = start };
		var crossedAt = new Dictionary<(int, int), (int, int)>(); // tile after a crossing -> the crossing tile
		var queue = new Queue<(int X, int Y)>();
		queue.Enqueue(start);
		(int X, int Y) goal = (-1, -1);
		Direction into = Direction.East;

		while (queue.Count > 0 && goal.X < 0)
		{
			var tile = queue.Dequeue();
			foreach (var d in Directions)
			{
				var next = (tile.X + d.DX(), tile.Y + d.DY());
				if (_planned.TryGetValue(next, out var step) && step == target)
				{
					goal = tile;
					into = d;
					break;
				}
			}
			if (goal.X >= 0)
				break;

			foreach (var d in Directions)
			{
				var next = (tile.X + d.DX(), tile.Y + d.DY());
				if (!previous.ContainsKey(next) && IsRoutable(world, next, source, target))
				{
					previous[next] = tile;
					queue.Enqueue(next);
					continue;
				}

				// Can't go there, but if it's one of our straight belts running across, cross it with a
				// junction and carry on straight out the other side.
				var beyond = (next.Item1 + d.DX(), next.Item2 + d.DY());
				if (CanCross(next, d) && !previous.ContainsKey(beyond) && IsRoutable(world, beyond, source, target))
				{
					previous[beyond] = tile;
					crossedAt[beyond] = next;
					queue.Enqueue(beyond);
				}
			}
		}
		if (goal.X < 0)
			return;

		// Walk back from the goal, putting the crossings back in between.
		var path = new List<(int X, int Y)> { goal };
		var junctions = new HashSet<(int, int)>();
		while (path[^1] != start)
		{
			var current = path[^1];
			if (crossedAt.TryGetValue(current, out var crossing))
			{
				path.Add(crossing);
				junctions.Add(crossing);
			}
			path.Add(previous[current]);
		}
		path.Reverse();

		// Each conveyor points at the next tile; crossings become junctions.
		for (int i = 0; i < path.Count; i++)
		{
			if (junctions.Contains(path[i]))
			{
				var old = _planned[path[i]];
				var junction = new Step(BuildingType.Junction, old.X, old.Y, old.Facing, ItemType.None);
				_plan[_plan.IndexOf(old)] = junction;
				_planned[path[i]] = junction;
				continue;
			}
			var facing = i + 1 < path.Count ? DirectionBetween(path[i], path[i + 1]) : into;
			AddStep(new Step(BuildingType.Conveyor, path[i].X, path[i].Y, facing, ItemType.None));
		}
	}

	/// <summary>
	/// Whether a new belt moving in <paramref name="moving"/> can cross the tile with a junction:
	/// it must be one of our planned conveyors running across, in the middle of a straight line.
	/// </summary>
	private bool CanCross((int X, int Y) tile, Direction moving)
	{
		if (!_planned.TryGetValue(tile, out var step) || step.Type != BuildingType.Conveyor)
			return false;
		if (step.Facing == moving || step.Facing == moving.Opposite())
			return false;
		// The item before it on its line must come in straight from behind, or the junction would break a turn.
		var behind = (tile.X - step.Facing.DX(), tile.Y - step.Facing.DY());
		return _planned.TryGetValue(behind, out var feeder) &&
			(feeder.Type == BuildingType.Junction || Pushes(feeder, step.Facing, behind, tile));
	}

	private bool IsRoutable(World world, (int X, int Y) tile, Step source, Step target)
	{
		var (x, y) = tile;
		if (_planned.ContainsKey(tile) || _reserved.ContainsKey(tile))
			return false;
		if (world.CheckLocation(BuildingType.Conveyor, x, y, _player) != PlaceError.None)
			return false;
		if (world.Map.GetResource(x, y) != ResourceType.None)
			return false;

		// Nothing but the source may push into this tile, now or once the rest of the plan is built.
		foreach (var d in Directions)
		{
			var neighbour = (x - d.DX(), y - d.DY()); // the tile an item moving in d would come from
			if (!_planned.TryGetValue(neighbour, out var step) && !_reserved.TryGetValue(neighbour, out step))
			{
				var existing = world.GetBuilding(neighbour.Item1, neighbour.Item2);
				if (existing == null || existing.Owner != _player)
					continue;
				step = new Step(existing.Type, existing.X, existing.Y, existing.Facing, ItemType.None);
			}
			if (step == source || step == target || (step.X == source.X && step.Y == source.Y))
				continue;
			if (Pushes(step, d, neighbour, tile))
				return false;
		}
		return true;
	}

	/// <summary>Whether a building would hand items moving in <paramref name="moving"/> into the tile next to it.</summary>
	private static bool Pushes(Step step, Direction moving, (int X, int Y) from, (int X, int Y) to) => step.Type switch
	{
		BuildingType.BrickExtractor or BuildingType.PlasticExtractor or BuildingType.BatteryExtractor => true,
		BuildingType.Splitter or BuildingType.Sorter => true,
		BuildingType.Conveyor or BuildingType.Assembler => step.Facing == moving,
		// A junction only passes items along its two lines, which already run through their tiles.
		BuildingType.Junction => false,
		_ => false,
	};

	private static Direction DirectionBetween((int X, int Y) a, (int X, int Y) b) =>
		b.X > a.X ? Direction.East : b.X < a.X ? Direction.West : b.Y > a.Y ? Direction.South : Direction.North;
}
