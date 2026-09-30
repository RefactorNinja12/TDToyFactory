namespace FactoryTD.Sim;

/// <summary>Things that travel on conveyors.</summary>
public enum ItemType : byte
{
	None,

	// Tier 1: raw resources
	Brick,
	Plastic,
	Battery,

	// Tier 2: made by the assembler
	Gear,
	Spring,
	CircuitBoard,
}

/// <summary>An amount of one item type, e.g. a building cost.</summary>
public readonly record struct ItemStack(ItemType Type, int Amount);

public static class Items
{
	/// <summary>Every real item type in order, e.g. for cycling a sorter filter.</summary>
	public static readonly ItemType[] All =
	{
		ItemType.Brick, ItemType.Plastic, ItemType.Battery,
		ItemType.Gear, ItemType.Spring, ItemType.CircuitBoard,
	};

	public static ItemType FromResource(ResourceType resource) => resource switch
	{
		ResourceType.Brick => ItemType.Brick,
		ResourceType.Plastic => ItemType.Plastic,
		ResourceType.Battery => ItemType.Battery,
		_ => ItemType.None,
	};
}
