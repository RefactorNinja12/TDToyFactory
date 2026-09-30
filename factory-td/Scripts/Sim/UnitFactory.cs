namespace FactoryTD.Sim;

/// <summary>
/// Tier 3: takes its recipe's inputs from belts on any side and, whenever it has them, builds a unit
/// that sets off towards the enemy core. Runs automatically; there is no queue.
/// </summary>
public sealed class UnitFactory : Building
{
	private readonly Crafter _crafter = new();

	public UnitType Produces { get; }
	public ItemStack[] Recipe => UnitStats.Recipe(Produces);

	/// <summary>Buffered inputs and build progress, e.g. for the info panel.</summary>
	public Crafter Crafter => _crafter;

	public UnitFactory(BuildingType type, int x, int y, Direction facing, int owner, UnitType produces)
		: base(type, x, y, facing, owner)
	{
		Produces = produces;
	}

	public override bool TryAccept(ItemType item, Direction moving) => _crafter.TryAccept(Recipe, item);

	public override void Tick(World world)
	{
		if (world.CountUnits(Owner, Produces) >= UnitStats.Cap(Produces))
			return;
		if (_crafter.Tick(Recipe, UnitStats.BuildTicks(Produces)))
			world.SpawnUnit(Produces, Owner, X + Width / 2, Y + Height / 2);
	}
}
