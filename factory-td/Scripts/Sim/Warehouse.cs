namespace FactoryTD.Sim;

/// <summary>
/// Extra storage. Takes any item from belts on any side into the owner's shared stock (the same pool as
/// the toybox), and each finished warehouse adds PlayerState.WarehouseCapacity room for every item type.
/// </summary>
public sealed class Warehouse : Building
{
	private readonly PlayerState _player;

	public Warehouse(int x, int y, Direction facing, int owner, PlayerState player)
		: base(BuildingType.Warehouse, x, y, facing, owner)
	{
		_player = player;
	}

	public override bool TryAccept(ItemType item, Direction moving) => _player.TryStore(item);

	public override bool CanTake(ItemType item) => _player.HasRoom(item);
}
