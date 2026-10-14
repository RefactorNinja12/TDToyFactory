using System.Collections.Generic;

namespace FactoryTD.Sim;

// BotPlayer partial: belt routing between planned buildings (breadth-first, junctions over own belts).
public sealed partial class BotPlayer
{
	// ---------------------------------------------------------------------------------------------
	// Belt routing (absolute coordinates).

	private static readonly Direction[] EastFirst = { Direction.East, Direction.South, Direction.West, Direction.North };
	private static readonly Direction[] WestFirst = { Direction.West, Direction.South, Direction.East, Direction.North };

	/// <summary>The order searches try the sides in, mirrored for the right room so both bots pick mirrored ways.</summary>
	private Direction[] Directions => _mirror ? WestFirst : EastFirst;

	/// <summary>
	/// Plans a belt from a free tile next to <paramref name="source"/> to a tile next to
	/// <paramref name="target"/>, ending pointing into the target. Breadth-first, so the shortest path.
	/// It never runs over deposits (kept free for extractors) and never passes a tile that some other
	/// building would push items into, so the belt carries only what the source makes and can't jam.
	/// If there is no way through, nothing is planned (false). <paramref name="exit"/>: the side the source puts its
	/// items out of (a kitchen's front); extractors push to every side.
	/// </summary>
	private bool Route(World world, Step source, Step target, Direction? exit = null, System.Func<Step, Direction, bool> endsAt = null)
	{
		if (_reserving)
			return true; // routes are worked out when the module is actually built
		var start = (-1, -1);
		foreach (var d in Directions)
		{
			var tile = (source.X + d.DX(), source.Y + d.DY());
			if ((exit == null || exit == d) && IsRoutable(world, tile, source, target))
			{
				start = tile;
				break;
			}
		}
		if (start.Item1 < 0)
			return false;

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
				if (_planned.TryGetValue(next, out var step) && (endsAt != null ? endsAt(step, d) : step == target))
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
			return false;

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
		return true;
	}

	/// <summary>
	/// Like Route to the toybox, but the belt may also end feeding into the side of one of our belts that runs
	/// into the toybox (the tiles round the toybox itself fill up).
	/// </summary>
	private bool RouteToCore(World world, Step source, Direction? exit = null)
	{
		var intoCore = new HashSet<Step>();
		foreach (var step in _plan)
			if (step.Type == BuildingType.Conveyor && ReachesCore(step))
				intoCore.Add(step);
		return Route(world, source, _core, exit, (step, moving) =>
			step == _core || (intoCore.Contains(step) && moving != step.Facing.Opposite()));
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
		BuildingType.BrickExtractor or BuildingType.PlasticExtractor or BuildingType.BatteryExtractor or BuildingType.CheeseMelter => true,
		BuildingType.Splitter or BuildingType.Sorter => true,
		BuildingType.Conveyor or BuildingType.Assembler => step.Facing == moving,
		// A junction only passes items along its two lines, which already run through their tiles.
		BuildingType.Junction => false,
		_ => false,
	};

	private static Direction DirectionBetween((int X, int Y) a, (int X, int Y) b) =>
		b.X > a.X ? Direction.East : b.X < a.X ? Direction.West : b.Y > a.Y ? Direction.South : Direction.North;
}
