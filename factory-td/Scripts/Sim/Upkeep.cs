namespace FactoryTD.Sim;

// World partial: food upkeep and starvation, storage capacity from warehouses.
public sealed partial class World
{
	private const int TicksPerMinute = TicksPerSecond * 60;

	/// <summary>Units eat: see PlayerState.Hunger. Starving units lose health every StarveTicks.</summary>
	private void TickUpkeep()
	{
		foreach (var player in _players)
			player.FoodUpkeepPerMinute = 0;
		foreach (var unit in _units)
		{
			if (unit.Health <= 0)
				continue;
			int perMinute = UnitStats.FoodPerMinute(unit.Type);
			_players[unit.Owner].Hunger += perMinute;
			_players[unit.Owner].FoodUpkeepPerMinute += perMinute;
		}
		if (TickCount % TicksPerSecond == 0)
			foreach (var player in _players)
				player.NextSecond();

		var foodOnly = new ItemStack[] { new(ItemType.Food, 1) };
		foreach (var player in _players)
		{
			while (player.Hunger >= TicksPerMinute && player.TrySpend(foodOnly))
			{
				player.Hunger -= TicksPerMinute;
				player.FoodEaten();
			}
			player.Starving = player.Hunger >= TicksPerMinute;
			if (player.Starving)
				player.Hunger = TicksPerMinute; // no debt: back to normal as soon as there is food
		}

		if (TickCount % UnitStats.StarveTicks == 0)
			foreach (var unit in _units)
				if (_players[unit.Owner].Starving)
					unit.Health -= UnitStats.StarveDamage;
	}

	/// <summary>Counts each player's finished warehouses, which set how much they can store.</summary>
	private void UpdateStorage()
	{
		foreach (var player in _players)
			player.Warehouses = 0;
		foreach (var building in _buildings)
			if (building.Type == BuildingType.Warehouse && building.IsBuilt)
				_players[building.Owner].Warehouses++;
	}
}
