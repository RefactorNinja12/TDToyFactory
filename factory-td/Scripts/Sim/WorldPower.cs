namespace FactoryTD.Sim;

// World partial: power for buildings and units (drawing energy from a network, unit batteries, the way back to the grid).
public sealed partial class World
{
	/// <summary>Which tiles each player's power grid covers, its networks and cords.</summary>
	public PowerGrid Power
	{
		get
		{
			RebuildFieldsIfDirty();
			return _power;
		}
	}

	/// <summary>
	/// Takes <paramref name="amount"/> energy from the player's network powering this tile, all or nothing.
	/// Whoever asks first in a tick gets served first (buildings in build order, then units by id).
	/// </summary>
	public bool TryDrawPower(int player, int x, int y, int amount) =>
		FreePower || Draw(Power.NetworkAt(player, x, y), amount);

	/// <summary>Like the tile version, from the owner's network that powers any tile of the building.</summary>
	public bool TryDrawPower(Building building, int amount) => FreePower || Draw(NetworkOf(building), amount);

	/// <summary>Whether the building's network has at least this much energy right now.</summary>
	public bool HasPower(Building building, int amount)
	{
		if (FreePower)
			return true;
		var network = NetworkOf(building);
		return network != null && network.Energy >= amount;
	}

	/// <summary>The owner's network powering the building (the first of its tiles that is covered), or null.</summary>
	public PowerNetwork NetworkOf(Building building)
	{
		var grid = Power;
		PowerNetwork found = null;
		building.AnyTile((x, y) => (found = grid.NetworkAt(building.Owner, x, y)) != null);
		return found;
	}

	private bool Draw(PowerNetwork network, int amount)
	{
		if (network == null || network.Energy < amount)
			return false;
		_players[network.Owner].EnergyUsed(amount);
		foreach (var charger in network.Chargers)
		{
			int take = System.Math.Min(amount, charger.Energy);
			charger.Energy -= take;
			amount -= take;
			if (amount == 0)
				break;
		}
		return true;
	}

	/// <summary>
	/// Test switch: every consumer counts as powered and golems/cars never lose charge.
	/// Lets tests of other systems ignore the power grid. Always false in real matches.
	/// </summary>
	internal bool FreePower { get; set; }

	/// <summary>
	/// Battery upkeep for golems and cars (see <see cref="UnitPower"/>). On its own grid it charges from that
	/// network; off it, it drains. Returns true when the battery decides what the unit does this tick
	/// (returning, charging or empty: no fighting), false when it may fight as usual.
	/// </summary>
	private bool TickCharge(Unit unit)
	{
		int max = PowerStats.MaxCharge(unit.Type);
		var network = Power.NetworkAt(unit.Owner, unit.TileX, unit.TileY);
		if (network != null)
		{
			int want = System.Math.Min(PowerStats.UnitChargePerTick, max - unit.Charge);
			if (want > 0 && Draw(network, want))
				unit.Charge += want;
			// Coming back, or low while passing through: stay and charge up before going out again.
			if (unit.PowerState is UnitPower.Returning or UnitPower.Empty ||
				(unit.PowerState == UnitPower.Normal && unit.Charge * 100L <= max * (long)PowerStats.ReturnPercent))
				unit.PowerState = UnitPower.Charging;
			if (unit.PowerState == UnitPower.Charging && unit.Charge * 100L >= max * (long)PowerStats.ChargedPercent)
				unit.PowerState = UnitPower.Normal;
			return unit.PowerState == UnitPower.Charging;
		}

		unit.Charge = System.Math.Max(0, unit.Charge - PowerStats.DrainPerTick(unit.Type));
		if (unit.Charge == 0)
			unit.PowerState = UnitPower.Empty;
		else if (unit.PowerState == UnitPower.Charging ||
			(unit.PowerState == UnitPower.Normal && unit.Charge * 100L <= max * (long)PowerStats.ReturnPercent))
			unit.PowerState = UnitPower.Returning;
		if (unit.PowerState == UnitPower.Normal)
			return false;

		var (nextX, nextY) = PowerField(unit.Owner).NextTile(unit.TileX, unit.TileY);
		int speed = UnitStats.Speed(unit.Type);
		if (unit.PowerState == UnitPower.Empty)
			speed = speed * PowerStats.EmptySpeedPercent / 100;
		const int half = UnitStats.SubTile / 2;
		TryStep(unit, nextX * UnitStats.SubTile + half, nextY * UnitStats.SubTile + half, speed);
		return true;
	}

	/// <summary>Distance to the nearest tile the player's grid powers (built on first use after a change).</summary>
	internal FlowField PowerField(int player)
	{
		RebuildFieldsIfDirty();
		if (_powerFieldDirty[player])
		{
			_powerFieldDirty[player] = false;
			_powerFields[player].Build(this, player, (x, y) => _power.NetworkAt(player, x, y) != null);
		}
		return _powerFields[player];
	}
}
