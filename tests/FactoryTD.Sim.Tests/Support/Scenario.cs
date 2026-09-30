using System;
using FactoryTD.Sim;
using Xunit;

namespace FactoryTD.Sim.Tests.Support;

/// <summary>
/// Short setup for simulation tests:
/// <code>var s = Scenario.Match().NoWorkers().Instant().Rich();
/// var kitchen = s.Place&lt;Kitchen&gt;(BuildingType.Kitchen, 6, 32, Direction.North);</code>
/// Coordinates are map tiles. The left room (player 0): core (6..7, 30..31), bricks (10..13, 22..25),
/// plastic (10..13, 36..39), batteries (40..42, 46..48), door x = 61 (y 29..32). The hall is x 62..97.
/// </summary>
public sealed class Scenario
{
	private bool _instant;

	public World World { get; }
	public PlayerState P0 => World.Players[0];
	public PlayerState P1 => World.Players[1];

	private Scenario(World world) => World = world;

	/// <summary>
	/// A normal 1v1 match: both cores, starting stock, starting builders and farmer.
	/// Power is free (everything powered, no unit drain) unless the test calls <see cref="RealPower"/>,
	/// and there is no fog of war unless it calls <see cref="RealFog"/>. No big toys on the map unless
	/// <paramref name="obstacles"/> (bot matches use them).
	/// </summary>
	public static Scenario Match(bool obstacles = false) => new(World.CreateMatch(obstacles)) { World = { FreePower = true, FullVision = true } };

	/// <summary>Use the real fog of war: players only know what their light has explored.</summary>
	public Scenario RealFog()
	{
		World.FullVision = false;
		return this;
	}

	/// <summary>Use the real power grid: consumers need a charged network, golems/cars drain off-grid.</summary>
	public Scenario RealPower()
	{
		World.FreePower = false;
		return this;
	}

	/// <summary>Removes all units, including the starting builders and farmers.</summary>
	public Scenario NoWorkers()
	{
		World.ClearUnits();
		return this;
	}

	/// <summary>Buildings placed from now on are finished at once (no builders needed).</summary>
	public Scenario Instant()
	{
		_instant = true;
		foreach (var building in World.Buildings)
			building.CompleteConstruction();
		return this;
	}

	/// <summary>Plenty of every building material for both players (ignores storage capacity).</summary>
	public Scenario Rich(int amount = 2000)
	{
		foreach (var player in World.Players)
		{
			player.Add(ItemType.Brick, amount);
			player.Add(ItemType.Plastic, amount);
			player.Add(ItemType.Battery, amount);
		}
		return this;
	}

	public Pylon Pylon(int x, int y, int owner = 0) => Place<Pylon>(BuildingType.Pylon, x, y, owner: owner);

	/// <summary>A battery charger, optionally already holding some energy.</summary>
	public BatteryCharger Charger(int x, int y, int energy = 0, int owner = 0)
	{
		var charger = Place<BatteryCharger>(BuildingType.BatteryCharger, x, y, owner: owner);
		charger.Energy = energy;
		return charger;
	}

	public Scenario Give(ItemType item, int amount, int owner = 0)
	{
		World.Players[owner].Add(item, amount);
		return this;
	}

	/// <summary>Takes every item of this type away (e.g. to start without food).</summary>
	public Scenario Empty(ItemType item, int owner = 0)
	{
		var player = World.Players[owner];
		player.TrySpend(new[] { new ItemStack(item, player.GetCount(item)) });
		return this;
	}

	/// <summary>Places a building or fails the test with the reason it couldn't be placed.</summary>
	public Building Place(BuildingType type, int x, int y, Direction facing = Direction.East, int owner = 0)
	{
		var error = World.CheckPlace(type, x, y, owner);
		if (error != PlaceError.None || !World.TryPlace(type, x, y, facing, owner))
			Assert.Fail($"Place {type} at ({x},{y}) for player {owner} failed: {error}");
		var building = World.GetBuilding(x, y);
		if (_instant)
			building.CompleteConstruction();
		return building;
	}

	public T Place<T>(BuildingType type, int x, int y, Direction facing = Direction.East, int owner = 0) where T : Building =>
		(T)Place(type, x, y, facing, owner);

	/// <summary>A straight line of conveyors from (x0,y0) to (x1,y1), all facing the way the line runs.</summary>
	public Scenario Belt(int x0, int y0, int x1, int y1, int owner = 0)
	{
		int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
		var facing = dx > 0 ? Direction.East : dx < 0 ? Direction.West : dy > 0 ? Direction.South : Direction.North;
		for (int x = x0, y = y0; ; x += dx, y += dy)
		{
			Place(BuildingType.Conveyor, x, y, facing, owner);
			if (x == x1 && y == y1)
				break;
		}
		return this;
	}

	/// <summary>A single conveyor facing a given way (for turns and odd pieces).</summary>
	public Conveyor Conveyor(int x, int y, Direction facing, int owner = 0) =>
		Place<Conveyor>(BuildingType.Conveyor, x, y, facing, owner);

	public Unit Spawn(UnitType type, int x, int y, int owner = 0) => World.SpawnUnit(type, owner, x, y);

	/// <summary>Offers <paramref name="count"/> items to a building; returns how many it took.</summary>
	public int Feed(Building building, ItemType item, int count = 1, Direction moving = Direction.East)
	{
		int taken = 0;
		for (int i = 0; i < count; i++)
			if (building.Offer(item, moving))
				taken++;
		return taken;
	}
}
