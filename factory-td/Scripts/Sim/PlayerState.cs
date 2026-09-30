using System;

namespace FactoryTD.Sim;

/// <summary>Per-player state. For now: how many of each item the player has delivered to their core.</summary>
public sealed class PlayerState
{
	private static readonly int ItemTypeCount = Enum.GetValues<ItemType>().Length;

	private readonly int[] _items = new int[ItemTypeCount];

	public int Id { get; }

	public PlayerState(int id) => Id = id;

	public int GetCount(ItemType type) => _items[(int)type];

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

	public void Refund(ItemStack[] cost)
	{
		foreach (var stack in cost)
			_items[(int)stack.Type] += stack.Amount;
	}
}
