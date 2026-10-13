namespace FactoryTD.Sim;

// BotPlayer partial: the plan, module by module (what the bot builds, where, and when).
public sealed partial class BotPlayer
{
	// ---------------------------------------------------------------------------------------------
	// The plan. Coordinates are for the left room: core (6..7, 30..31), bricks (10..13, 22..25),
	// plastic (10..13, 36..39), batteries (40..42, 46..48), door at x = 61, y = 29..32.

	private void DefineModules()
	{
		_pending.Add(new Module(EconomyModule, _ => true, _ =>
		{
			Place(BuildingType.BrickExtractor, 13, 25, Direction.South);
			Belt(13, 26, 13, 29, Direction.South);
			Belt(13, 30, 8, 30, Direction.West);
			Place(BuildingType.PlasticExtractor, 13, 36, Direction.North);
			Belt(13, 35, 13, 32, Direction.North);
			Belt(13, 31, 8, 31, Direction.West);
		}));

		_pending.Add(new Module("fler utvinnare", _ => true, _ =>
		{
			Belt(10, 26, 12, 26, Direction.East);
			Place(BuildingType.BrickExtractor, 12, 25, Direction.South);
			Place(BuildingType.BrickExtractor, 11, 25, Direction.South);
			Place(BuildingType.BrickExtractor, 10, 25, Direction.South);
			Belt(10, 35, 12, 35, Direction.East);
			Place(BuildingType.PlasticExtractor, 12, 36, Direction.North);
			Place(BuildingType.PlasticExtractor, 11, 36, Direction.North);
			Place(BuildingType.PlasticExtractor, 10, 36, Direction.North);
		}));

		// Scouts: extractors can only go where someone has been, so a tent early. It stands right next to a
		// plastic melter, which fills it directly (a tent only takes a few pieces, the belt keeps the rest).
		// Extractors on unexplored ground are simply skipped by the plan until the scouts (or the builders
		// laying belts out there) have lit them.
		_pending.Add(new Module("spejare", _ => true, _ => Place(BuildingType.Tent, 14, 36, Direction.East)));

		// Food: a farmhouse (farmers cost bricks, so one brick extractor feeds it), a kitchen pointing
		// straight into the toybox and four fields next to it, west of the core. Farmers carry the
		// crops to the kitchen; the kitchen's food goes straight into the toybox.
		_pending.Add(new Module("mat", _ => true, w =>
		{
			var kitchen = Place(BuildingType.Kitchen, 6, 32, Direction.North);
			for (int x = 2; x <= 5; x++)
				Place(BuildingType.CropField, x, 34, Direction.East);
			var farmhouse = Place(BuildingType.Farmhouse, 4, 24, Direction.East);
			var bricks = Place(BuildingType.BrickExtractor, 10, 23, Direction.West);
			Route(w, bricks, farmhouse);
			_lastFarmTick = w.TickCount;
		},
		Emergency: FoodCrisis));

		// More fields, a row of four at a time, while the army eats more than the kitchen brings in.
		for (int row = 35; row <= 40; row++)
		{
			int y = row;
			_pending.Add(new Module($"åkrar {row - 34}",
				w => Built("mat") && NeedsMoreFood(w),
				w =>
				{
					for (int x = 2; x <= 5; x++)
						Place(BuildingType.CropField, x, y, Direction.East);
					// Every other row of fields gets another kitchen pointing into the toybox (one cooks ~20 food/min).
					if (y == 36) Place(BuildingType.Kitchen, 7, 32, Direction.North);
					if (y == 38) Place(BuildingType.Kitchen, 5, 31, Direction.East);
					if (y == 40) Place(BuildingType.Kitchen, 5, 30, Direction.East);
					_lastFarmTick = w.TickCount;
				},
				Emergency: w => Built("mat") && FoodCrisis(w) && w.TickCount >= _lastFarmTick + Minute / 2));
		}

		// Crops piling up in storage means the kitchens can't keep up: cook more (north side of the toybox).
		bool CropsPilingUp(World w) => Built("mat") && w.Players[_player].GetCount(ItemType.Crop) >= 40;
		_pending.Add(new Module("fler kök 1", CropsPilingUp, _ => Place(BuildingType.Kitchen, 6, 29, Direction.South), Emergency: CropsPilingUp));
		_pending.Add(new Module("fler kök 2", w => Built("fler kök 1") && CropsPilingUp(w),
			_ => Place(BuildingType.Kitchen, 7, 29, Direction.South),
			Emergency: w => Built("fler kök 1") && CropsPilingUp(w) && w.TickCount >= _lastFarmTick + Minute / 2));

		// Warehouses when the stock is nearly full (they add room even without a belt of their own).
		_pending.Add(new Module("lager 1", StorageNearlyFull, _ => Place(BuildingType.Warehouse, 2, 27, Direction.East)));
		_pending.Add(new Module("lager 2", w => Built("lager 1") && StorageNearlyFull(w),
			_ => Place(BuildingType.Warehouse, 2, 21, Direction.East)));

		// More builders: a toolbox fed bricks and plastic by short belts from its own extractors.
		_pending.Add(new Module("verktygslåda", _ => true, w =>
		{
			var toolbox = Place(BuildingType.Toolbox, 16, 29, Direction.East);
			var bricks = Place(BuildingType.BrickExtractor, 12, 22, Direction.North);
			var plastic = Place(BuildingType.PlasticExtractor, 10, 38, Direction.West);
			Route(w, bricks, toolbox);
			Route(w, plastic, toolbox);
		}));

		// Power: a charger fed by a battery belt from the battery patch, and pylons over the base so every
		// factory, assembler and tower is on the grid (the toybox links the west pylons, the rest link
		// in a chain). Before the towers: a tower without power doesn't shoot.
		_pending.Add(new Module("ström", _ => true, w =>
		{
			var charger = Place(BuildingType.BatteryCharger, 18, 31, Direction.East);
			Place(BuildingType.Pylon, 8, 26, Direction.East);   // core defence north, links to the toybox
			Place(BuildingType.Pylon, 8, 36, Direction.East);   // core defence south
			Place(BuildingType.Pylon, 15, 26, Direction.East);  // golem workshop
			Place(BuildingType.Pylon, 15, 34, Direction.East);  // soldier factory
			Place(BuildingType.Pylon, 21, 30, Direction.East);  // front towers, charger
			var battery = Place(BuildingType.BatteryExtractor, 40, 47, Direction.West);
			Route(w, battery, charger);
		},
		Emergency: _ => SoldiersSeen + GolemsSeen + CarsSeen >= 1));

		// The front: attackers come in through the door (x = 61) and head west for the core, shooting
		// whatever buildings they meet first. So the towers stand in front of the base, on the door side
		// of the factories (x = 19), where attackers run into them before they reach anything else, but
		// close enough to home that short belts from the base's own extractors can feed them.
		// Built right after the economy, or at once when attackers are on their way in.
		_pending.Add(new Module("frontförsvar",
			_ => true,
			w =>
			{
				var catapult = Place(BuildingType.Catapult, 19, 27, Direction.East);
				var bricks = Place(BuildingType.BrickExtractor, 10, 22, Direction.West);
				var foam = Place(BuildingType.FoamTower, 19, 33, Direction.East);
				var plastic = Place(BuildingType.PlasticExtractor, 10, 39, Direction.West);
				Route(w, bricks, catapult);
				Route(w, plastic, foam);
			},
			Emergency: _ => SoldiersSeen + GolemsSeen + CarsSeen >= 1));

		// Last line: a catapult and foam tower by the core, fed straight by extractors, for whatever gets
		// through the front. At once when attackers keep coming, or anyway after six minutes.
		_pending.Add(new Module("kärnförsvar",
			w => w.TickCount >= 6 * Minute,
			w =>
			{
				Place(BuildingType.PlasticExtractor, 10, 37, Direction.West);
				Place(BuildingType.FoamTower, 9, 37, Direction.East);
				Place(BuildingType.BrickExtractor, 10, 24, Direction.West);
				Place(BuildingType.Catapult, 9, 24, Direction.East);
			},
			Emergency: _ => SoldiersSeen + GolemsSeen + CarsSeen >= 10));

		// Soldiers: melter -> assembler (springs) -> factory, melter -> belt -> factory (plastic).
		_pending.Add(new Module("soldater", w => w.TickCount >= _armyDelayTicks && !FoodCrisis(w), _ =>
		{
			Place(BuildingType.SoldierFactory, 15, 37, Direction.East);
			Place(BuildingType.Assembler, 14, 38, Direction.East, ItemType.Spring);
			Place(BuildingType.PlasticExtractor, 13, 38, Direction.East);
			Place(BuildingType.Conveyor, 14, 37, Direction.East);
			Place(BuildingType.PlasticExtractor, 13, 37, Direction.East);
		}));

		// Cheese hunters (cheap, early): cheese melters on the cheese patch north of the core fill a treadmill
		// right above them; builders walk over to be trained by themselves.
		_pending.Add(new Module("ostjägare", w => Built("soldater") && !FoodCrisis(w) && w.TickCount >= _armyDelayTicks + 2 * Minute, _ =>
		{
			Place(BuildingType.Treadmill, 6, 20, Direction.East);
			Place(BuildingType.CheeseMelter, 6, 22, Direction.East);
			Place(BuildingType.CheeseMelter, 7, 22, Direction.East);
		}));

		// Batteries into the core: towers and car factories cost batteries to build.
		_pending.Add(new Module("batterier",
			_ => Built("soldater"),
			w =>
			{
				var battery = Place(BuildingType.BatteryExtractor, 41, 46, Direction.North);
				Route(w, battery, _core);
			},
			Emergency: _ => GolemsSeen > 0 || CarsSeen > 0));

		// Golems and cars run flat away from the grid: a chain of pylons through the door and along the
		// hall (every 8 tiles, the cord range) lets them charge on the way to the enemy.
		_pending.Add(new Module("frammaster", w => Built("soldater") && Built("ström") && w.TickCount >= _armyDelayTicks + 2 * Minute, _ =>
		{
			for (int x = 29; x <= 53; x += 8)
				Place(BuildingType.Pylon, x, 30, Direction.East);
			for (int x = 60; x <= 92; x += 8)
				Place(BuildingType.Pylon, x, 30, Direction.East);
		}));

		// Golems by the brick patch: bricks -> gear assembler -> workshop, bricks -> belt -> workshop,
		// and plastic routed from the plastic patch into the gear assembler.
		_pending.Add(new Module("golems", w => Built("soldater") && !FoodCrisis(w) && w.TickCount >= _armyDelayTicks + 3 * Minute, w =>
		{
			Place(BuildingType.GolemWorkshop, 15, 22, Direction.East);
			var gears = Place(BuildingType.Assembler, 14, 23, Direction.East, ItemType.Gear);
			Place(BuildingType.BrickExtractor, 13, 23, Direction.East);
			Place(BuildingType.Conveyor, 14, 22, Direction.East);
			Place(BuildingType.BrickExtractor, 13, 22, Direction.East);
			var plastic = Place(BuildingType.PlasticExtractor, 13, 39, Direction.East);
			Route(w, plastic, gears);
		}));

		// Batteries into lasers once golems show up (or anyway, a bit later).
		_pending.Add(new Module("lasertorn",
			w => Built("batterier") && w.TickCount >= _armyDelayTicks + 4 * Minute,
			w =>
			{
				var laser = Place(BuildingType.LaserTower, 20, 34, Direction.East);
				var battery = Place(BuildingType.BatteryExtractor, 40, 46, Direction.West);
				Route(w, battery, laser);
			},
			Emergency: _ => Built("batterier") && GolemsSeen > 0));

		// Radio cars by the battery patch: batteries -> circuit assembler -> factory, batteries -> belt ->
		// factory, and plastic/bricks routed in for circuits and gears.
		_pending.Add(new Module("radiobilar", w => Built("batterier") && !FoodCrisis(w) && w.TickCount >= _armyDelayTicks + 5 * Minute, w =>
		{
			Place(BuildingType.CarFactory, 44, 46, Direction.East);
			var circuits = Place(BuildingType.Assembler, 43, 46, Direction.East, ItemType.CircuitBoard);
			Place(BuildingType.BatteryExtractor, 42, 46, Direction.East);
			Place(BuildingType.Conveyor, 43, 47, Direction.East);
			Place(BuildingType.BatteryExtractor, 42, 47, Direction.East);
			// Its own little grid: a charger filled straight by the extractor next to it.
			Place(BuildingType.BatteryExtractor, 42, 48, Direction.East);
			Place(BuildingType.BatteryCharger, 43, 48, Direction.East);
			Place(BuildingType.Pylon, 45, 45, Direction.East);
			var gears = Place(BuildingType.Assembler, 46, 46, Direction.West, ItemType.Gear);
			var plasticA = Place(BuildingType.PlasticExtractor, 12, 39, Direction.South);
			var plasticB = Place(BuildingType.PlasticExtractor, 11, 39, Direction.South);
			var bricks = Place(BuildingType.BrickExtractor, 11, 22, Direction.North);
			Route(w, plasticA, circuits);
			Route(w, plasticB, gears);
			Route(w, bricks, gears);
		}));

		// Water pistols once radio cars show up.
		_pending.Add(new Module("vattenpistol",
			_ => false,
			w =>
			{
				var water = Place(BuildingType.WaterTower, 22, 27, Direction.East);
				var battery = Place(BuildingType.BatteryExtractor, 40, 48, Direction.West);
				Route(w, battery, water);
			},
			Emergency: _ => Built("batterier") && CarsSeen > 0));

		// A second laser if golems keep coming.
		_pending.Add(new Module("fler lasertorn",
			_ => false,
			w =>
			{
				var laser = Place(BuildingType.LaserTower, 24, 33, Direction.East);
				var battery = Place(BuildingType.BatteryExtractor, 41, 48, Direction.South);
				Route(w, battery, laser);
			},
			Emergency: _ => Built("lasertorn") && GolemsSeen >= 4));
	}
}
