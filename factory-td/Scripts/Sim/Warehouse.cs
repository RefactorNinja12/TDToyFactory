namespace FactoryTD.Sim;

/// <summary>A building that puts every item it is given into its owner's shared stock (toybox, warehouse).</summary>
public abstract class StoreBuilding : Building
{
	private readonly PlayerState _player;

	protected StoreBuilding(BuildingType type, int x, int y, Direction facing, int owner, PlayerState player)
		: base(type, x, y, facing, owner)
	{
		_player = player;
	}

	public override bool TryAccept(ItemType item, Direction moving) => _player.TryStore(item);

	public override bool CanTake(ItemType item) => _player.HasRoom(item);
}

/// <summary>
/// Extra storage. Takes any item from belts on any side into the owner's shared stock (the same pool as
/// the toybox), and each finished warehouse adds PlayerState.WarehouseCapacity room for every item type.
/// </summary>
public sealed class Warehouse : StoreBuilding
{
	public Warehouse(int x, int y, Direction facing, int owner, PlayerState player)
		: base(BuildingType.Warehouse, x, y, facing, owner, player) { }
}
