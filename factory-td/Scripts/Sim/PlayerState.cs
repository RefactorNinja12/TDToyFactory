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
			FoodStoredTotal++;
		return true;
	}

	/// <summary>All food ever stored / eaten, for the food meter.</summary>
	public int FoodStoredTotal { get; private set; }
	public int FoodEatenTotal { get; private set; }

	internal void FoodEaten() => FoodEatenTotal++;

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
