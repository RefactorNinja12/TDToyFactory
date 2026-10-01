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

	// Food chain: crops from fields, food cooked from crops (units eat food)
	Crop,
	Food,

	// Cheese: melted from cheese deposits; kitchens cook it, treadmills train cheese hunters with it
	MeltedCheese,
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
		ItemType.Crop, ItemType.Food, ItemType.MeltedCheese,
	};

	public static ItemType FromResource(ResourceType resource) => resource switch
	{
		ResourceType.Brick => ItemType.Brick,
		ResourceType.Plastic => ItemType.Plastic,
		ResourceType.Battery => ItemType.Battery,
		ResourceType.Cheese => ItemType.MeltedCheese,
		_ => ItemType.None,
	};
}
