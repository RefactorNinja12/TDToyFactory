namespace FactoryTD.Sim;

/// <summary>
/// Balance for electricity. Energy is counted in whole energy units (EU); rates are per tick
/// (20 ticks per second), so "4 EU/s" is 4 EU every 20 ticks.
/// </summary>
public static class PowerStats
{
	/// <summary>Energy one battery gives when a charger uses it up.</summary>
	public const int EnergyPerBattery = 300;
	/// <summary>Most energy one charger can hold.</summary>
	public const int ChargerCapacity = 1000;
	/// <summary>Batteries a charger keeps waiting in its input.</summary>
	public const int ChargerBatteryBuffer = 2;

	/// <summary>Pylons power every tile whose centre is within this many tiles.</summary>
	public const int PylonRadius = 5;
	/// <summary>The toybox powers a small area around itself, so every grid has a starting point.</summary>
	public const int CoreRadius = 4;
	/// <summary>Pylons, chargers and the toybox link with a cord up to this many tiles apart.</summary>
	public const int LinkRange = 8;
}

/// <summary>Toy power pylon: carries the grid's power to the tiles around it and links to other pylons by cord.</summary>
public sealed class Pylon : Building
{
	public Pylon(int x, int y, Direction facing, int owner)
		: base(BuildingType.Pylon, x, y, facing, owner)
	{
	}

	public override bool OutputsToward(Direction direction) => false;
}

/// <summary>
/// Toy battery charger: uses up batteries from belts and puts their energy into the grid it is linked to.
/// </summary>
public sealed class BatteryCharger : Building
{
	/// <summary>Stored energy, shared with the rest of its network.</summary>
	public int Energy { get; internal set; }

	public BatteryCharger(int x, int y, Direction facing, int owner)
		: base(BuildingType.BatteryCharger, x, y, facing, owner)
	{
	}

	public override bool OutputsToward(Direction direction) => false;

	protected override void HashState(ref StateHash hash) => hash.Add(Energy);
}
