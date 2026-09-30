namespace FactoryTD.Sim;

/// <summary>
/// The toy box. Each player has one; it takes any item from any side into the owner's stock while there
/// is room (see PlayerState.Capacity), so belts into a full toybox back up.
/// Enemy units shoot it; at zero health its owner loses (it is never removed).
/// </summary>
public sealed class Core : Building
{
	private readonly PlayerState _player;

	public Core(int x, int y, Direction facing, int owner, PlayerState player)
		: base(BuildingType.Core, x, y, facing, owner)
	{
		_player = player;
	}

	public override bool TryAccept(ItemType item, Direction moving) => _player.TryStore(item);

	public override bool CanTake(ItemType item) => _player.HasRoom(item);
}
