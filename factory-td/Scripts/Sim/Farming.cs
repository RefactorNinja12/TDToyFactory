namespace FactoryTD.Sim;

/// <summary>
/// A toy planter where plastic carrots grow. Once finished it grows on a timer until ripe, then waits
/// for a farmer to harvest it (it never pushes onto belts); harvesting starts the timer again.
/// </summary>
public sealed class CropField : Building
{
	// ---- Balance: crops ----
	public const int GrowTicks = World.TicksPerSecond * 30;
	public const int Yield = 3;

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
