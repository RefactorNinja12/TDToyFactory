namespace FactoryTD.Sim;

/// <summary>
/// Produces one item of its deposit's resource every ProductionTicks and pushes it to any neighbour
/// that takes it (conveyors not pointing back into the extractor, cores), preferring the front.
/// Stores up to MaxStored items when the output is blocked. Deposits are infinite for now.
/// </summary>
public sealed class Extractor : Building
{
	public const int ProductionTicks = 40; // 2 s at 20 ticks/s
	public const int MaxStored = 5;

	public ItemType Output { get; }
	public int Stored { get; private set; }

	/// <summary>Ticks spent on the item currently being produced.</summary>
	public int Progress { get; private set; }

	public Extractor(BuildingType type, int x, int y, Direction facing, int owner)
		: base(type, x, y, facing, owner)
	{
		Output = Items.FromResource(BuildingRules.RequiredResource(type));
	}

	public override void Tick(World world)
	{
		if (Stored < MaxStored && ++Progress >= ProductionTicks)
		{
			Progress = 0;
			Stored++;
		}

		if (Stored > 0 && TryPushAnySide(world, Output))
			Stored--;
	}

	public override bool OutputsToward(Direction direction) => true;

	protected override void HashState(ref StateHash hash)
	{
		hash.Add(Stored); hash.Add(Progress);
	}
}
