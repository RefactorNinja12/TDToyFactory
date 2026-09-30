using System;

namespace FactoryTD.Sim;

/// <summary>
/// Per-player state: the items stored in the player's toybox and warehouses (one shared pool),
/// and how much room there is for each item type.
/// </summary>
public sealed class PlayerState
{
	// ---- Balance: storage ----
	/// <summary>Room for each item type in the toybox.</summary>
	public const int CoreCapacity = 400;

	/// <summary>Extra room for each item type per finished warehouse.</summary>
	public const int WarehouseCapacity = 250;

	private static readonly int ItemTypeCount = Enum.GetValues<ItemType>().Length;

	private readonly int[] _items = new int[ItemTypeCount];

	public int Id { get; }

	/// <summary>Finished warehouses; kept up to date by World every tick.</summary>
	public int Warehouses { get; internal set; }

	/// <summary>
	/// Upkeep counter: every tick each unit adds its food-per-minute; each TicksPerMinute points eat one
	/// food. Integer-only, so the same on every machine.
	/// </summary>
	internal int Hunger { get; set; }

	/// <summary>Out of food with mouths to feed: army factories pause and units lose health.</summary>
	public bool Starving { get; internal set; }

	public PlayerState(int id) => Id = id;

	public int GetCount(ItemType type) => _items[(int)type];

	/// <summary>How many of this item type the toybox and warehouses can hold together.</summary>
	public int Capacity(ItemType type) => CoreCapacity + Warehouses * WarehouseCapacity;

	public bool HasRoom(ItemType type) => _items[(int)type] < Capacity(type);

	/// <summary>Stores one item if there is room. What the toybox and warehouses call.</summary>
	public bool TryStore(ItemType type)
	{
		if (!HasRoom(type))
			return false;
		_items[(int)type]++;
		if (type == ItemType.Food)
		{
			FoodStoredTotal++;
			_foodInPerSecond[_second]++;
			FoodProducedLastMinute++;
		}
		return true;
	}

	/// <summary>All food ever stored / eaten.</summary>
	public int FoodStoredTotal { get; private set; }
	public int FoodEatenTotal { get; private set; }

	// Food meter: food stored and eaten in each of the last 60 seconds (a ring of one-second slots).
	private readonly int[] _foodInPerSecond = new int[60];
	private readonly int[] _foodOutPerSecond = new int[60];
	private int _second;

	/// <summary>Food that reached storage (kitchens, belts, farmers) during the last minute.</summary>
	public int FoodProducedLastMinute { get; private set; }

	/// <summary>Food actually eaten during the last minute.</summary>
	public int FoodEatenLastMinute { get; private set; }

	/// <summary>How much food the player's units eat per minute right now; kept up to date by World.</summary>
	public int FoodUpkeepPerMinute { get; internal set; }

	internal void FoodEaten()
	{
		FoodEatenTotal++;
		_foodOutPerSecond[_second]++;
		FoodEatenLastMinute++;
	}

	// Power meter: energy chargers made from batteries, and energy consumers used, per second (same ring).
	private readonly int[] _energyInPerSecond = new int[60];
	private readonly int[] _energyOutPerSecond = new int[60];

	/// <summary>Energy the player's chargers made from batteries during the last minute.</summary>
	public int EnergyChargedLastMinute { get; private set; }

	/// <summary>Energy the player's factories, towers and units used during the last minute.</summary>
	public int EnergyUsedLastMinute { get; private set; }

	internal void EnergyCharged(int amount)
	{
		_energyInPerSecond[_second] += amount;
		EnergyChargedLastMinute += amount;
	}

	internal void EnergyUsed(int amount)
	{
		_energyOutPerSecond[_second] += amount;
		EnergyUsedLastMinute += amount;
	}

	/// <summary>Moves the food and power meters on by one second (called by World once a second).</summary>
	internal void NextSecond()
	{
		_second = (_second + 1) % _foodInPerSecond.Length;
		FoodProducedLastMinute -= _foodInPerSecond[_second];
		FoodEatenLastMinute -= _foodOutPerSecond[_second];
		_foodInPerSecond[_second] = 0;
		_foodOutPerSecond[_second] = 0;
		EnergyChargedLastMinute -= _energyInPerSecond[_second];
		EnergyUsedLastMinute -= _energyOutPerSecond[_second];
		_energyInPerSecond[_second] = 0;
		_energyOutPerSecond[_second] = 0;
	}

	internal void HashInto(ref StateHash hash)
	{
		foreach (int count in _items) hash.Add(count);
		hash.Add(Warehouses); hash.Add(Hunger); hash.Add(Starving);
	}

	/// <summary>Adds without checking room (starting stock, test setup).</summary>
	public void Add(ItemType type, int amount = 1) => _items[(int)type] += amount;

	public bool CanAfford(ItemStack[] cost)
	{
		foreach (var stack in cost)
			if (_items[(int)stack.Type] < stack.Amount)
				return false;
		return true;
	}

	/// <summary>Removes the whole cost, or nothing if the player can't afford all of it.</summary>
	public bool TrySpend(ItemStack[] cost)
	{
		if (!CanAfford(cost))
			return false;
		foreach (var stack in cost)
			_items[(int)stack.Type] -= stack.Amount;
		return true;
	}

	/// <summary>Gives a cost back (tearing a building down). May go over capacity; nothing is lost.</summary>
	public void Refund(ItemStack[] cost)
	{
		foreach (var stack in cost)
			_items[(int)stack.Type] += stack.Amount;
	}
}
