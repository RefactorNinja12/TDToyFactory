namespace FactoryTD.Sim;

/// <summary>
/// A toy planter where plastic carrots grow. Once finished it grows on a timer until ripe, then waits
/// for a farmer to harvest it (it never pushes onto belts); harvesting starts the timer again.
/// </summary>
public sealed class CropField : Building
{
	// ---- Balance: crops ----
	// One field gives ~5-6 food/min; an army of 20 soldiers plus workers needs ~5-6 fields and 2 kitchens.
	public const int GrowTicks = World.TicksPerSecond * 20;
	public const int Yield = 4;

	public CropField(int x, int y, Direction facing, int owner)
		: base(BuildingType.CropField, x, y, facing, owner) { }

	/// <summary>Ticks grown since the last harvest, up to GrowTicks.</summary>
	public int Growth { get; private set; }

	public bool IsRipe => Growth >= GrowTicks;

	/// <summary>0 = just planted .. 3 = ripe, for the view.</summary>
	public int Stage => IsRipe ? 3 : Growth * 3 / GrowTicks;

	public override void Tick(World world)
	{
		if (Growth < GrowTicks)
			Growth++;
	}

	/// <summary>Picks a ripe field: returns the crops and replants. 0 if it isn't ripe.</summary>
	public int Harvest()
	{
		if (!IsRipe)
			return 0;
		Growth = 0;
		return Yield;
	}
}

/// <summary>
/// A toy stove that cooks crops into food. Takes crops from farmers and from belts on any side, and pushes
/// the food out of its front (the arrow): onto a belt, or straight into a toybox/warehouse next to it.
/// </summary>
public sealed class Kitchen : Building
{
	// ---- Balance: cooking ----
	public static readonly ItemStack[] Recipe = { new(ItemType.Crop, 2) };
	public const int CookTicks = World.TicksPerSecond * 3;
	private const int MaxOutput = 5;

	// Room for 5 meals' worth of crops, so a farmer's whole armful (3) usually fits.
	private readonly Crafter _crafter = new(bufferCrafts: 5);
	private int _output;

	public Kitchen(int x, int y, Direction facing, int owner)
		: base(BuildingType.Kitchen, x, y, facing, owner) { }

	public Crafter Crafter => _crafter;

	/// <summary>Cooked food waiting to go out of the front.</summary>
	public int Finished => _output;

	public override bool OutputsToward(Direction direction) => direction == Facing;

	public override bool TryAccept(ItemType item, Direction moving) => _crafter.TryAccept(Recipe, item);

	public override bool CanTake(ItemType item) => IsBuilt && _crafter.HasRoom(Recipe, item);

	public override void Tick(World world)
	{
		if (_output < MaxOutput && _crafter.Tick(Recipe, CookTicks))
			_output++;
		if (_output > 0 && TryPush(world, ItemType.Food, Facing))
			_output--;
	}
}
