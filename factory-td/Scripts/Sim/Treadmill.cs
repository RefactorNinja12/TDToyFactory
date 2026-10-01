namespace FactoryTD.Sim;

/// <summary>
/// Treadmill (drawn and named as a boxing ring, "Boxningsring"): a builder mouse trains in it for a piece of
/// cheese and comes out as a cheese hunter.
/// Fed melted cheese by belt; with enough cheese it calls a builder (never the last one), who walks in and
/// is used up, then trains. Runs on the mouse, so it needs no power; like every army factory it stops
/// while its player is starving.
/// </summary>
public sealed class Treadmill : Building
{
	public static readonly ItemStack[] Recipe = { new(ItemType.MeltedCheese, 2) };

	private readonly Crafter _crafter = new(bufferCrafts: 2);

	public Treadmill(int x, int y, Direction facing, int owner)
		: base(BuildingType.Treadmill, x, y, facing, owner) { }

	public Crafter Crafter => _crafter;

	/// <summary>A builder has climbed on and is being trained.</summary>
	public bool HasTrainee { get; private set; }

	public override bool TryAccept(ItemType item, Direction moving) => _crafter.TryAccept(Recipe, item);

	public override bool CanTake(ItemType item) => IsBuilt && _crafter.HasRoom(Recipe, item);

	public override bool OutputsToward(Direction direction) => false;

	internal void Enter() => HasTrainee = true;

	public override void Tick(World world)
	{
		if (world.Players[Owner].Starving)
			return;
		if (!HasTrainee)
		{
			if (_crafter.CanWork(Recipe))
				world.CallBuilder(this);
			return;
		}
		if (_crafter.Tick(Recipe, UnitStats.BuildTicks(UnitType.CheeseHunter)))
		{
			HasTrainee = false;
			world.SpawnUnit(UnitType.CheeseHunter, Owner, X + Width / 2, Y + Height / 2);
		}
	}

	protected override void HashState(ref StateHash hash)
	{
		_crafter.HashInto(ref hash);
		hash.Add(HasTrainee);
	}
}
