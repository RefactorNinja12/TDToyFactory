namespace FactoryTD.Sim;

/// <summary>
/// Takes an item from any side and hands items out in turn straight on, to the right and to the left,
/// relative to the way the item came in. A blocked side is skipped, so one full belt doesn't stop the others.
/// Its own facing doesn't matter.
/// </summary>
public sealed class Splitter : Building
{
	private ItemType _held;
	private Direction _heldMoving; // the way the held item was travelling when it came in
	private int _next;             // 0 = straight, 1 = right, 2 = left

	public Splitter(int x, int y, Direction facing, int owner)
		: base(BuildingType.Splitter, x, y, facing, owner) { }

	public override bool OutputsToward(Direction direction) => true;

	public override bool TryAccept(ItemType item, Direction moving)
	{
		if (_held != ItemType.None)
			return false;
		_held = item;
		_heldMoving = moving;
		return true;
	}

	public override void Tick(World world)
	{
		if (_held == ItemType.None)
			return;

		for (int i = 0; i < 3; i++)
		{
			int output = (_next + i) % 3;
			if (TryPush(world, _held, OutputDirection(output)))
			{
				_held = ItemType.None;
				_next = (output + 1) % 3;
				return;
			}
		}
	}

	private Direction OutputDirection(int output) => output switch
	{
		0 => _heldMoving,
		1 => _heldMoving.RotatedClockwise(),
		_ => _heldMoving.RotatedCounterClockwise(),
	};
}

/// <summary>
/// Lets two belts cross. Items go straight through: east-west traffic and north-south traffic each
/// have their own slot, so the two lines never mix.
/// </summary>
public sealed class Junction : Building
{
	private ItemType _horizontal, _vertical;
	private Direction _horizontalMoving, _verticalMoving;

	public Junction(int x, int y, Direction facing, int owner)
		: base(BuildingType.Junction, x, y, facing, owner) { }

	public override bool OutputsToward(Direction direction) => true;

	public override bool TryAccept(ItemType item, Direction moving)
	{
		if (moving is Direction.East or Direction.West)
		{
			if (_horizontal != ItemType.None)
				return false;
			(_horizontal, _horizontalMoving) = (item, moving);
		}
		else
		{
			if (_vertical != ItemType.None)
				return false;
			(_vertical, _verticalMoving) = (item, moving);
		}
		return true;
	}

	public override void Tick(World world)
	{
		if (_horizontal != ItemType.None && TryPush(world, _horizontal, _horizontalMoving))
			_horizontal = ItemType.None;
		if (_vertical != ItemType.None && TryPush(world, _vertical, _verticalMoving))
			_vertical = ItemType.None;
	}
}

/// <summary>
/// Takes an item from any side. Items matching Filter carry straight on; everything else turns off
/// to the sides, alternating right and left. Players cycle the filter by clicking the sorter.
/// Its own facing doesn't matter.
/// </summary>
public sealed class Sorter : Building
{
	private ItemType _held;
	private Direction _heldMoving;
	private bool _leftNext;

	public ItemType Filter { get; private set; } = ItemType.Brick;

	public Sorter(int x, int y, Direction facing, int owner)
		: base(BuildingType.Sorter, x, y, facing, owner) { }

	public override bool OutputsToward(Direction direction) => true;

	public override bool CycleSetting()
	{
		int index = System.Array.IndexOf(Items.All, Filter);
		Filter = Items.All[(index + 1) % Items.All.Length];
		return true;
	}

	public override bool TryAccept(ItemType item, Direction moving)
	{
		if (_held != ItemType.None)
			return false;
		_held = item;
		_heldMoving = moving;
		return true;
	}

	public override void Tick(World world)
	{
		if (_held == ItemType.None)
			return;

		if (_held == Filter)
		{
			if (TryPush(world, _held, _heldMoving))
				_held = ItemType.None;
			return;
		}

		var first = _leftNext ? _heldMoving.RotatedCounterClockwise() : _heldMoving.RotatedClockwise();
		if (TryPush(world, _held, first) || TryPush(world, _held, first.Opposite()))
		{
			_held = ItemType.None;
			_leftNext = !_leftNext;
		}
	}
}
