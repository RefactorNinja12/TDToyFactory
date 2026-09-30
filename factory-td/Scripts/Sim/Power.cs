namespace FactoryTD.Sim;

/// <summary>
/// Balance for electricity. Energy is counted in whole energy units (EU); rates are per tick
/// (20 ticks per second), so "4 EU/s" is 4 EU every 20 ticks.
/// </summary>
public static class PowerStats
{
	/// <summary>Energy one battery gives when a charger uses it up.</summary>
	public const int EnergyPerBattery = 6000;
	/// <summary>Most energy one charger can hold.</summary>
	public const int ChargerCapacity = 20000;

	/// <summary>Army factories (not worker factories) while they craft.</summary>
	public const int FactoryEnergyPerTick = 2;
	/// <summary>Assemblers while they craft.</summary>
	public const int AssemblerEnergyPerTick = 1;
	/// <summary>Towers pay this for every shot.</summary>
	public const int TowerShotEnergy = 30;

	// ---- Golems and cars carry a battery: it drains off the grid and recharges on it. ----
	/// <summary>Energy a unit takes from the grid per tick while charging (or topping up).</summary>
	public const int UnitChargePerTick = 40;
	/// <summary>At or below this share of its battery a unit turns back to the grid.</summary>
	public const int ReturnPercent = 35;
	/// <summary>A charging unit stays on the grid until its battery is this full.</summary>
	public const int ChargedPercent = 90;
	/// <summary>An empty unit crawls home at this share of its speed.</summary>
	public const int EmptySpeedPercent = 25;

	/// <summary>Battery size; 0 = not electric. Cars are faster, so they get about twice the golem's range.</summary>
	public static int MaxCharge(UnitType type) => type switch
	{
		UnitType.BrickGolem => 12000,
		UnitType.RcCar => 8000,
		_ => 0,
	};

	public static int DrainPerTick(UnitType type) => type switch
	{
		UnitType.BrickGolem => 10,
		UnitType.RcCar => 10,
		_ => 0,
	};

	public static bool IsElectric(UnitType type) => MaxCharge(type) > 0;
	/// <summary>Batteries a charger keeps waiting in its input.</summary>
	public const int ChargerBatteryBuffer = 2;

	/// <summary>Pylons power every tile whose centre is within this many tiles.</summary>
	public const int PylonRadius = 5;
	/// <summary>The toybox powers a small area around itself, so every grid has a starting point.</summary>
	public const int CoreRadius = 4;
	/// <summary>Pylons, chargers and the toybox link with a cord up to this many tiles apart.</summary>
	public const int LinkRange = 8;
}

/// <summary>What a golem or car is doing about its battery.</summary>
public enum UnitPower : byte
{
	/// <summary>Enough charge: fights as usual (and tops up while it happens to be on the grid).</summary>
	Normal,
	/// <summary>Low: walks back to the nearest powered tile, doesn't attack.</summary>
	Returning,
	/// <summary>On the grid, recharging until ChargedPercent; stands still, doesn't attack.</summary>
	Charging,
	/// <summary>Flat battery: crawls home at EmptySpeedPercent, doesn't attack.</summary>
	Empty,
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

	/// <summary>Batteries waiting to be used up.</summary>
	public int Batteries { get; private set; }

	public override bool OutputsToward(Direction direction) => false;

	public override bool CanTake(ItemType item) => item == ItemType.Battery && Batteries < PowerStats.ChargerBatteryBuffer;

	public override bool TryAccept(ItemType item, Direction moving)
	{
		if (!CanTake(item))
			return false;
		Batteries++;
		return true;
	}

	/// <summary>Uses up one battery per tick while the energy fits.</summary>
	public override void Tick(World world)
	{
		if (Batteries > 0 && Energy + PowerStats.EnergyPerBattery <= PowerStats.ChargerCapacity)
		{
			Batteries--;
			Energy += PowerStats.EnergyPerBattery;
			world.Players[Owner].EnergyCharged(PowerStats.EnergyPerBattery);
		}
	}

	protected override void HashState(ref StateHash hash)
	{
		hash.Add(Energy);
		hash.Add(Batteries);
	}
}
