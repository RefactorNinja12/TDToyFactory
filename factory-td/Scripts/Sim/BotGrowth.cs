using System.Collections.Generic;

namespace FactoryTD.Sim;

// BotPlayer partial: growth once the fixed modules are done. Spots are searched (absolute coordinates), so the
// bot keeps finding something to build until the match ends: extractors, army outposts, towers by the door,
// wall layers across the door and hall, cheese kitchens.
public sealed partial class BotPlayer
{
	private const int GrowthTicks = World.TicksPerSecond * 20;
	private const int MaxWallLayers = 4;
	private const int WallLine = 70;       // left-room x of the first wall layer (the hall's own deposits end at 69)
	private const int BatteryReserve = 20; // battery towers/fences only with this many in stock

	private enum Growth { Economy, Army, Tower, Wall }

	private static readonly Growth[] Rotation = { Growth.Economy, Growth.Army, Growth.Tower, Growth.Army, Growth.Wall };

	private long _nextGrowth;
	private int _growthIndex;
	private int _armyOutposts;
	private int _towersAdded;
	private int _wallLayers;

	/// <summary>What each growth attempt did (minute: kind ok/-), for tests and debugging.</summary>
	internal List<string> GrowthLog { get; } = new();

	/// <summary>Plans one growth step (food first when short); false if it's not time yet or nothing fits.</summary>
	private bool Grow(World world)
	{
		if (world.TickCount < _nextGrowth)
			return false;
		_nextGrowth = world.TickCount + GrowthTicks;
		bool foodShort = FoodShort(world);
		if (foodShort)
		{
			bool fed = GrowFood(world);
			GrowthLog.Add($"{world.TickCount / Minute}:food{(fed ? "" : "-")}");
			if (fed)
				return true;
		}
		for (int i = 0; i < Rotation.Length; i++)
		{
			var kind = Rotation[_growthIndex++ % Rotation.Length];
			if (kind == Growth.Army && foodShort)
				continue;
			bool planned = kind switch
			{
				Growth.Economy => GrowEconomy(world),
				Growth.Army => GrowArmy(world),
				Growth.Tower => GrowTower(world),
				_ => GrowWalls(world),
			};
			GrowthLog.Add($"{world.TickCount / Minute}:{kind}{(planned ? "" : "-")}");
			if (planned)
				return true;
		}
		return false;
	}

	private bool FoodShort(World world)
	{
		var player = world.Players[_player];
		return FoodCrisis(world) || player.FoodUpkeepPerMinute > player.FoodProducedLastMinute;
	}

	private int Stock(World world, ItemType item) => world.Players[_player].GetCount(item);

	private bool BatteriesToSpare(World world) => Stock(world, ItemType.Battery) >= BatteryReserve;

	// ---------------------------------------------------------------------------------------------
	// Growth kinds. Each plans a whole piece or nothing (Undo when a belt can't be routed).

	/// <summary>Another extractor of whatever is scarcest (under half full), belted to the toybox.</summary>
	private bool GrowEconomy(World world)
	{
		var player = world.Players[_player];
		var scarce = ItemType.None;
		foreach (var item in new[] { ItemType.Brick, ItemType.Plastic, ItemType.Battery })
			if (player.GetCount(item) * 2 < player.Capacity(item) && (scarce == ItemType.None || player.GetCount(item) < player.GetCount(scarce)))
				scarce = item;
		// Nothing short: a line anyway (rotating), while deposits are free: later towers and outposts tap it.
		if (scarce == ItemType.None)
			scarce = _growthIndex % 2 == 0 ? ItemType.Brick : ItemType.Plastic;
		return Feed(world, ResourceOf(scarce), _core, tap: false);
	}

	/// <summary>A soldier or golem outpost: assembler into a factory, extractors belted in, on the grid.</summary>
	private bool GrowArmy(World world)
	{
		bool golems = _armyOutposts % 2 == 1;
		var (ax, ay) = Absolute(30, 30);
		var spot = FindSpot(world, 3, 2, ax, ay);
		if (spot == null)
			return false;
		var (sx, sy) = spot.Value;
		int mark = _plan.Count;
		var factoryType = golems ? BuildingType.GolemWorkshop : BuildingType.SoldierFactory;
		// The assembler on the home side, facing the factory; the factory's units spawn towards the enemy.
		var assembler = _mirror
			? AddStep(new Step(BuildingType.Assembler, sx + 2, sy, Direction.West, golems ? ItemType.Gear : ItemType.Spring))
			: AddStep(new Step(BuildingType.Assembler, sx, sy, Direction.East, golems ? ItemType.Gear : ItemType.Spring));
		var factory = AddStep(new Step(factoryType, _mirror ? sx : sx + 1, sy, Direction.East, ItemType.None));

		bool ok = Feed(world, ResourceType.Plastic, assembler) &&
			Feed(world, golems ? ResourceType.Brick : ResourceType.Plastic, golems ? assembler : factory) &&
			(!golems || Feed(world, ResourceType.Brick, factory));
		if (!ok)
		{
			Undo(mark);
			return false;
		}
		EnsurePower(world, assembler);
		EnsurePower(world, factory);
		_armyOutposts++;
		return true;
	}

	/// <summary>A tower just inside the door, fed by a belt from its own extractor.</summary>
	private bool GrowTower(World world)
	{
		var type = (_towersAdded % 3) switch
		{
			0 => BuildingType.FoamTower,
			1 => BuildingType.Catapult,
			_ => BuildingType.LaserTower,
		};
		if (CarsSeen > 0 && _towersAdded % 4 == 3)
			type = BuildingType.WaterTower;
		if (type is BuildingType.LaserTower or BuildingType.WaterTower && !BatteriesToSpare(world))
			type = BuildingType.FoamTower;
		var (ax, ay) = Absolute(WallLine - 3, 30); // just behind the walls
		var spot = FindSpot(world, 1, 1, ax, ay);
		if (spot == null)
			return false;
		int mark = _plan.Count;
		var tower = AddStep(new Step(type, spot.Value.X, spot.Value.Y, Direction.East, ItemType.None));
		var ammo = Tower.StatsFor(type).Ammo;
		if (!Feed(world, ResourceOf(ammo), tower))
		{
			Undo(mark);
			return false;
		}
		EnsurePower(world, tower);
		_towersAdded++;
		return true;
	}

	/// <summary>
	/// The next wall layer across the hall, past the bot's own hall deposits (they stay inside). Gates on the middle
	/// rows let its own army out; electric fence once there are batteries to spare. Taken tiles are left out.
	/// </summary>
	private bool GrowWalls(World world)
	{
		if (_wallLayers >= MaxWallLayers)
			return false;
		int layer = _wallLayers++;
		int x = WallLine + layer;
		const int top = 25, bottom = 36; // the hall's inside
		var wallType = layer >= 2 && BatteriesToSpare(world) ? BuildingType.ElectricFence : BuildingType.BrickWall;
		int placed = 0;
		Step any = null;
		for (int y = top; y <= bottom; y++)
		{
			var (tx, ty) = Absolute(x, y);
			if (!IsFree(world, tx, ty))
				continue;
			var type = y is 30 or 31 ? BuildingType.Gate : wallType;
			any = AddStep(new Step(type, tx, ty, Direction.East, ItemType.None));
			placed++;
		}
		if (wallType == BuildingType.ElectricFence && any != null)
			EnsurePower(world, any);
		return placed > 0;
	}

	/// <summary>A cheese melter with a kitchen beside it, the kitchen's food belted to the toybox.</summary>
	private bool GrowFood(World world)
	{
		foreach (var (x, y) in FreeDeposits(world, ResourceType.Cheese, _core.X, _core.Y))
		{
			foreach (var d in Directions)
			{
				int kx = x + d.DX(), ky = y + d.DY();
				if (!IsFree(world, kx, ky) || world.Map.GetResource(kx, ky) != ResourceType.None)
					continue;
				int mark = _plan.Count;
				AddStep(new Step(BuildingType.CheeseMelter, x, y, Direction.East, ItemType.None));
				var kitchen = AddStep(new Step(BuildingType.Kitchen, kx, ky, d, ItemType.None));
				if (RouteToCore(world, kitchen, d))
					return true;
				Undo(mark);
			}
		}
		return GrowFarm(world);
	}

	/// <summary>Six crop fields with a kitchen at the end, its food belted to the toybox (farmers do the rest).</summary>
	private bool GrowFarm(World world)
	{
		var spot = FindSpot(world, 4, 2, _core.X, _core.Y);
		if (spot == null)
			return false;
		var (sx, sy) = spot.Value;
		int mark = _plan.Count;
		int fieldsX = _mirror ? sx + 1 : sx;
		for (int y = sy; y < sy + 2; y++)
			for (int x = fieldsX; x < fieldsX + 3; x++)
				AddStep(new Step(BuildingType.CropField, x, y, Direction.East, ItemType.None));
		var exit = _mirror ? Direction.West : Direction.East;
		var kitchen = AddStep(new Step(BuildingType.Kitchen, _mirror ? sx : sx + 3, sy, exit, ItemType.None));
		if (RouteToCore(world, kitchen, exit))
			return true;
		Undo(mark);
		return false;
	}

	// ---------------------------------------------------------------------------------------------
	// Search helpers (absolute coordinates).

	private (int X, int Y) Absolute(int x, int y) => (_mirror ? _width - 1 - x : x, y);

	private static ResourceType ResourceOf(ItemType item) => item switch
	{
		ItemType.Brick => ResourceType.Brick,
		ItemType.Plastic => ResourceType.Plastic,
		ItemType.Battery => ResourceType.Battery,
		_ => ResourceType.None,
	};

	private static BuildingType ExtractorFor(ResourceType resource) => resource switch
	{
		ResourceType.Brick => BuildingType.BrickExtractor,
		ResourceType.Plastic => BuildingType.PlasticExtractor,
		ResourceType.Battery => BuildingType.BatteryExtractor,
		_ => BuildingType.CheeseMelter,
	};

	private const int FeedTries = 6;

	/// <summary>
	/// An extractor on one of the free deposits nearest the target that a belt can run from (a tile in the
	/// middle of a field can't), belted into it.
	/// </summary>
	private bool Feed(World world, ResourceType resource, Step target, bool tap = true)
	{
		int tries = 0;
		foreach (var (x, y) in FreeDeposits(world, resource, target.X, target.Y))
		{
			if (tries++ == FeedTries)
				break;
			int mark = _plan.Count;
			var extractor = AddStep(new Step(ExtractorFor(resource), x, y, Direction.East, ItemType.None));
			if (target == _core ? RouteToCore(world, extractor) : Route(world, extractor, target))
				return true;
			Undo(mark);
		}
		return tap && Tap(world, resource, target);
	}

	/// <summary>
	/// No deposit left: a splitter on a straight piece of a belt that carries only this resource to the toybox,
	/// and a branch from its side to the target. A full target just lets the items run on to the toybox.
	/// </summary>
	private bool Tap(World world, ResourceType resource, Step target)
	{
		var candidates = new List<(int Index, long D)>();
		for (int i = 0; i < _plan.Count; i++)
		{
			var c = _plan[i];
			if (c.Type != BuildingType.Conveyor || !Straight(c) || LineResource(c) != resource || !ReachesCore(c))
				continue;
			long dx = c.X - target.X, dy = c.Y - target.Y;
			candidates.Add((i, dx * dx + dy * dy));
		}
		candidates.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.Index.CompareTo(b.Index));
		int tries = 0;
		foreach (var (index, _) in candidates)
		{
			if (tries++ == FeedTries)
				break;
			var belt = _plan[index];
			foreach (var side in new[] { belt.Facing.RotatedClockwise(), belt.Facing.RotatedCounterClockwise() })
			{
				var other = side.Opposite();
				if (!IsFree(world, belt.X + other.DX(), belt.Y + other.DY()))
					continue; // the splitter would push into whatever is there
				var splitter = new Step(BuildingType.Splitter, belt.X, belt.Y, belt.Facing, ItemType.None);
				_plan[index] = splitter;
				_planned[(belt.X, belt.Y)] = splitter;
				if (Route(world, splitter, target, side))
					return true;
				_plan[index] = belt;
				_planned[(belt.X, belt.Y)] = belt;
			}
		}
		return false;
	}

	/// <summary>Whether the belt before it and the one after it both run the same way (a splitter fits there).</summary>
	private bool Straight(Step belt)
	{
		var behind = (belt.X - belt.Facing.DX(), belt.Y - belt.Facing.DY());
		var ahead = (belt.X + belt.Facing.DX(), belt.Y + belt.Facing.DY());
		return _planned.TryGetValue(behind, out var b) && b.Type == BuildingType.Conveyor && b.Facing == belt.Facing &&
			_planned.TryGetValue(ahead, out var a) && a.Type == BuildingType.Conveyor && a.Facing == belt.Facing;
	}

	/// <summary>
	/// The one resource every item on this belt comes from: traced back through the belts and extractors that
	/// push into it. None when anything else feeds it (a junction, splitter, assembler, two resources).
	/// </summary>
	private ResourceType LineResource(Step start)
	{
		var found = ResourceType.None;
		var seen = new HashSet<Step>();
		var stack = new Stack<Step>();
		stack.Push(start);
		while (stack.Count > 0)
		{
			var belt = stack.Pop();
			if (!seen.Add(belt) || seen.Count > 200)
				return ResourceType.None;
			foreach (var d in Directions)
			{
				var from = (belt.X - d.DX(), belt.Y - d.DY());
				if (!_planned.TryGetValue(from, out var feeder) || !Pushes(feeder, d, from, (belt.X, belt.Y)))
					continue;
				if (feeder.Type == BuildingType.Conveyor)
				{
					stack.Push(feeder);
					continue;
				}
				var resource = feeder.Type switch
				{
					BuildingType.BrickExtractor => ResourceType.Brick,
					BuildingType.PlasticExtractor => ResourceType.Plastic,
					BuildingType.BatteryExtractor => ResourceType.Battery,
					_ => ResourceType.None,
				};
				if (resource == ResourceType.None || (found != ResourceType.None && found != resource))
					return ResourceType.None;
				found = resource;
			}
		}
		return found;
	}

	/// <summary>Whether following the belts (straight through junctions) from here ends in the toybox.</summary>
	private bool ReachesCore(Step belt)
	{
		var step = belt;
		for (int guard = 0; guard < 200; guard++)
		{
			var next = (step.X + step.Facing.DX(), step.Y + step.Facing.DY());
			if (!_planned.TryGetValue(next, out var ahead))
				return false;
			if (ahead == _core)
				return true;
			if (ahead.Type == BuildingType.Junction)
			{
				var beyond = (next.Item1 + step.Facing.DX(), next.Item2 + step.Facing.DY());
				if (!_planned.TryGetValue(beyond, out ahead))
					return false;
				if (ahead == _core)
					return true;
			}
			if (ahead.Type != BuildingType.Conveyor)
				return false;
			step = ahead;
		}
		return false;
	}

	/// <summary>The bot's room, and its end of the hall up to its wall line.</summary>
	private bool OurGround(World world, int x, int y)
	{
		var zone = world.Map.GetZone(x, y);
		if (zone == MapLayout.HomeZone(_player))
			return true;
		int leftX = _mirror ? _width - 1 - x : x;
		return zone == Zone.Hall && leftX < WallLine;
	}

	/// <summary>A tile nothing is planned, reserved or built on.</summary>
	private bool IsFree(World world, int x, int y) =>
		world.Map.InBounds(x, y) && !_planned.ContainsKey((x, y)) && !_reserved.ContainsKey((x, y)) &&
		world.GetBuilding(x, y) == null && world.CheckLocation(BuildingType.Conveyor, x, y, _player) == PlaceError.None;

	/// <summary>
	/// Free, explored deposit tiles of the resource in the bot's room, nearest the point first. Tiles next to a
	/// belt are left out (an extractor would push its items onto it).
	/// </summary>
	private List<(int X, int Y)> FreeDeposits(World world, ResourceType resource, int nearX, int nearY)
	{
		var found = new List<(int X, int Y, long D)>();
		var type = ExtractorFor(resource);
		for (int y = 0; y < world.Map.Height; y++)
			for (int x = 0; x < world.Map.Width; x++)
			{
				if (world.Map.GetResource(x, y) != resource || !OurGround(world, x, y) || !world.IsExplored(_player, x, y))
					continue;
				if (_planned.ContainsKey((x, y)) || _reserved.ContainsKey((x, y)) || world.GetBuilding(x, y) != null)
					continue;
				if (world.CheckLocation(type, x, y, _player) != PlaceError.None || NextToBelt(world, x, y))
					continue;
				long dx = x - nearX, dy = y - nearY;
				found.Add((x, y, dx * dx + dy * dy));
			}
		found.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
		return found.ConvertAll(f => (f.X, f.Y));
	}

	private bool NextToBelt(World world, int x, int y)
	{
		foreach (var d in Directions)
		{
			var tile = (x + d.DX(), y + d.DY());
			var type = _planned.TryGetValue(tile, out var step) || _reserved.TryGetValue(tile, out step)
				? step.Type
				: world.GetBuilding(tile.Item1, tile.Item2)?.Type;
			if (type is BuildingType.Conveyor or BuildingType.Junction or BuildingType.Splitter or BuildingType.Sorter)
				return true;
		}
		return false;
	}

	/// <summary>
	/// The free w x h rectangle (off deposits, with a free ring round it so belts can reach it) whose centre is
	/// nearest the point, in the bot's room. Null if there is none.
	/// </summary>
	private (int X, int Y)? FindSpot(World world, int w, int h, int nearX, int nearY)
	{
		(int X, int Y)? best = null;
		long bestDistance = long.MaxValue;
		for (int y = 1; y + h < world.Map.Height; y++)
			for (int x = 1; x + w < world.Map.Width; x++)
			{
				if (!OurGround(world, x, y))
					continue;
				long dx = 2 * x + w - 2 * nearX, dy = 2 * y + h - 2 * nearY;
				long distance = dx * dx + dy * dy;
				if (distance >= bestDistance || !SpotFits(world, x, y, w, h))
					continue;
				best = (x, y);
				bestDistance = distance;
			}
		return best;
	}

	private bool SpotFits(World world, int x0, int y0, int w, int h)
	{
		for (int y = y0 - 1; y <= y0 + h; y++)
			for (int x = x0 - 1; x <= x0 + w; x++)
			{
				bool inside = x >= x0 && x < x0 + w && y >= y0 && y < y0 + h;
				if (_planned.ContainsKey((x, y)) || _reserved.ContainsKey((x, y)) || world.GetBuilding(x, y) != null)
					return false;
				if (inside && (!OurGround(world, x, y) || world.Map.GetResource(x, y) != ResourceType.None ||
					world.CheckLocation(BuildingType.Conveyor, x, y, _player) != PlaceError.None))
					return false;
			}
		return true;
	}

	/// <summary>
	/// Pylons from the nearest planned power node towards the step until it is covered: each new pylon within
	/// cord range of the last node, as close to the step as a free tile allows.
	/// </summary>
	private void EnsurePower(World world, Step step)
	{
		var (w, h) = BuildingRules.Size(step.Type, step.Facing);
		int cx2 = 2 * step.X + w, cy2 = 2 * step.Y + h; // centre, in half tiles
		for (int guard = 0; guard < 10; guard++)
		{
			Step node = null;
			long nodeDistance = long.MaxValue;
			foreach (var s in _plan)
			{
				if (s.Type is not (BuildingType.Pylon or BuildingType.Core))
					continue;
				var (nw, nh) = BuildingRules.Size(s.Type, s.Facing);
				long dx = 2 * s.X + nw - cx2, dy = 2 * s.Y + nh - cy2;
				if (dx * dx + dy * dy < nodeDistance)
					(node, nodeDistance) = (s, dx * dx + dy * dy);
			}
			if (node == null)
				return;
			int radius = node.Type == BuildingType.Core ? PowerStats.CoreRadius : PowerStats.PylonRadius;
			if (nodeDistance <= 4L * radius * radius)
				return;

			var (nodeW, nodeH) = BuildingRules.Size(node.Type, node.Facing);
			int nx2 = 2 * node.X + nodeW, ny2 = 2 * node.Y + nodeH;
			(int X, int Y)? best = null;
			long bestDistance = nodeDistance;
			int reach = PowerStats.LinkRange;
			for (int y = node.Y - reach; y <= node.Y + reach; y++)
				for (int x = node.X - reach; x <= node.X + reach; x++)
				{
					long lx = 2 * x + 1 - nx2, ly = 2 * y + 1 - ny2;
					if (lx * lx + ly * ly > 4L * reach * reach || !IsFree(world, x, y) || world.Map.GetResource(x, y) != ResourceType.None)
						continue;
					long dx = 2 * x + 1 - cx2, dy = 2 * y + 1 - cy2;
					if (dx * dx + dy * dy < bestDistance)
						(best, bestDistance) = ((x, y), dx * dx + dy * dy);
				}
			if (best == null)
				return;
			AddStep(new Step(BuildingType.Pylon, best.Value.X, best.Value.Y, Direction.East, ItemType.None));
		}
	}

	/// <summary>Takes back every step planned from <paramref name="mark"/> on (a piece that didn't fit).</summary>
	private void Undo(int mark)
	{
		for (int i = _plan.Count - 1; i >= mark; i--)
		{
			var step = _plan[i];
			var (w, h) = BuildingRules.Size(step.Type, step.Facing);
			for (int y = step.Y; y < step.Y + h; y++)
				for (int x = step.X; x < step.X + w; x++)
					if (_planned.TryGetValue((x, y), out var at) && at == step)
						_planned.Remove((x, y));
			_plan.RemoveAt(i);
		}
	}
}
