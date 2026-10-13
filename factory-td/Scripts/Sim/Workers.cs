using System.Collections.Generic;

namespace FactoryTD.Sim;

// World partial: builders and farmers (choosing jobs, walking there, building, harvesting and delivering).
public sealed partial class World
{
	/// <summary>Turns off <see cref="ReplaceLostBuilders"/> (tests that want no units at all).</summary>
	internal bool NoBuilderRespawn { get; set; }

	/// <summary>
	/// A player is never left without a builder: if the last one is gone, a new one comes out of the toybox,
	/// so the player can always build again.
	/// </summary>
	private void ReplaceLostBuilders()
	{
		if (NoBuilderRespawn)
			return;
		for (int p = 0; p < _cores.Length; p++)
		{
			var core = _cores[p];
			if (core == null || core.Health <= 0 || CountUnits(p, UnitType.Builder) > 0)
				continue;
			SpawnUnit(UnitType.Builder, p, core.X + core.Width / 2, core.Y + core.Height / 2);
		}
	}

	/// <summary>
	/// A treadmill with cheese wants a builder: the nearest idle one of its owner walks over, or if all are
	/// busy the nearest one that is building (never the last builder, so the player can always still build).
	/// Nothing if one is already on the way.
	/// </summary>
	internal void CallBuilder(Treadmill mill)
	{
		int staying = 0; // builders not already on their way to a treadmill
		Unit idle = null, busy = null;
		long bestIdle = long.MaxValue, bestBusy = long.MaxValue;
		foreach (var unit in _units)
		{
			if (unit.Owner != mill.Owner || unit.Type != UnitType.Builder || unit.Health <= 0)
				continue;
			if (unit.Job == mill)
				return;
			if (unit.Job is Treadmill)
				continue; // already going to another treadmill
			staying++;
			long dx = unit.X - mill.CenterX, dy = unit.Y - mill.CenterY, d = dx * dx + dy * dy;
			if (unit.Job == null && d < bestIdle)
				(idle, bestIdle) = (unit, d);
			else if (unit.Job != null && d < bestBusy)
				(busy, bestBusy) = (unit, d);
		}
		var nearest = idle ?? busy;
		// The one called must leave at least one builder behind (two treadmills can't take the last two).
		if (staying <= 1 || nearest == null)
			return;
		nearest.Job = mill;
		nearest.Path = null;
	}

	/// <summary>How many builders have picked this construction site.</summary>
	public int BuildersOn(Building site)
	{
		int count = 0;
		foreach (var unit in _units)
			if (unit.Job == site && unit.Health > 0)
				count++;
		return count;
	}

	// ---------------------------------------------------------------------------------------------
	// Builders: pick the nearest unfinished site (spreading out over several), walk there, build.

	private const int CrowdPenalty = 12; // a site already being worked on counts as this many tiles further away

	private void TickBuilder(Unit builder)
	{
		builder.PrevX = builder.X;
		builder.PrevY = builder.Y;
		builder.MoveX = builder.MoveY = 0;

		var job = builder.Job;
		// Called to a treadmill: walk in and become the trainee.
		if (job is Treadmill mill)
		{
			if (GetBuilding(mill.X, mill.Y) == mill && !mill.HasTrainee)
			{
				switch (WalkTo(builder, mill))
				{
					case Walk.Arrived:
						if (CountUnits(builder.Owner, UnitType.Builder) <= 1)
							break; // the others died meanwhile: the last builder never goes in
						mill.Enter();
						builder.Health = 0; // used up: gone at the end of the tick
						return;
					case Walk.Walking:
						return;
				}
			}
			builder.Job = job = null;
		}
		if (job != null && (job.IsBuilt || GetBuilding(job.X, job.Y) != job))
			builder.Job = job = null;
		if (job == null)
		{
			builder.Job = job = ChooseSite(builder);
			builder.Path = null;
			if (job == null)
				return; // nothing to build: wait where it is
		}

		switch (WalkTo(builder, job))
		{
			case Walk.Arrived:
				job.AddWork(UnitStats.BuilderWorkPerTick);
				Face(builder, job);
				if (job.IsBuilt)
					_powerDirty = true; // finished: it may be a pylon or charger now
				break;
			case Walk.Unreachable:
				builder.Job = null; // try something else next tick
				break;
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Farmers: harvest the nearest ripe field (one farmer per field), carry the crops to the nearest
	// kitchen with room, or else to the toybox / a warehouse, and repeat.

	private const int FieldTakenPenalty = 1000; // a field someone else is already going for

	private void TickFarmer(Unit farmer)
	{
		farmer.PrevX = farmer.X;
		farmer.PrevY = farmer.Y;
		farmer.MoveX = farmer.MoveY = 0;

		if (farmer.CarryAmount > 0)
			DeliverCrops(farmer);
		else
			GoHarvest(farmer);
	}

	private void GoHarvest(Unit farmer)
	{
		// Fetching crops from storage for a kitchen (set up below when nothing is ripe).
		if (farmer.Job is Core or Warehouse)
		{
			FetchStoredCrops(farmer);
			return;
		}

		if (farmer.Job is not CropField field || !IsStanding(field) || !field.IsRipe)
		{
			farmer.Job = field = ChooseField(farmer);
			farmer.WorkTimer = 0;
			if (field == null)
			{
				// Nothing ripe: bring stored crops to a kitchen that has room, if there are any.
				if (_players[farmer.Owner].GetCount(ItemType.Crop) > 0 && KitchenWithRoom(farmer) != null)
					farmer.Job = NearestStorage(farmer);
				return;
			}
		}

		switch (WalkTo(farmer, field))
		{
			case Walk.Arrived:
				Face(farmer, field);
				if (++farmer.WorkTimer < UnitStats.HarvestTicks)
					return;
				farmer.Carrying = ItemType.Crop;
				farmer.CarryAmount = field.Harvest();
				farmer.WorkTimer = 0;
				farmer.Job = null;
				break;
			case Walk.Unreachable:
				farmer.Job = null;
				break;
		}
	}

	private void DeliverCrops(Unit farmer)
	{
		if (farmer.Job == null || !IsStanding(farmer.Job) || !farmer.Job.CanTake(farmer.Carrying))
		{
			farmer.Job = ChooseDropOff(farmer);
			if (farmer.Job == null)
				return; // everything full: wait with the crops
		}

		switch (WalkTo(farmer, farmer.Job))
		{
			case Walk.Arrived:
				Face(farmer, farmer.Job);
				while (farmer.CarryAmount > 0 && farmer.Job.Offer(farmer.Carrying, Direction.East))
					farmer.CarryAmount--;
				if (farmer.CarryAmount == 0)
					farmer.Carrying = ItemType.None;
				farmer.Job = null; // done, or this one is full: look again next tick
				break;
			case Walk.Unreachable:
				farmer.Job = null;
				break;
		}
	}

	/// <summary>Walks to the toybox/warehouse and picks up stored crops (then DeliverCrops takes over).</summary>
	private void FetchStoredCrops(Unit farmer)
	{
		var player = _players[farmer.Owner];
		if (!IsStanding(farmer.Job) || player.GetCount(ItemType.Crop) == 0 || KitchenWithRoom(farmer) == null)
		{
			farmer.Job = null;
			return;
		}
		switch (WalkTo(farmer, farmer.Job))
		{
			case Walk.Arrived:
				int take = System.Math.Min(CropField.Yield, player.GetCount(ItemType.Crop));
				player.TrySpend(new[] { new ItemStack(ItemType.Crop, take) });
				farmer.Carrying = ItemType.Crop;
				farmer.CarryAmount = take;
				farmer.Job = null; // DeliverCrops picks the kitchen
				break;
			case Walk.Unreachable:
				farmer.Job = null;
				break;
		}
	}

	private Building KitchenWithRoom(Unit farmer)
	{
		foreach (var building in _buildings)
			if (building is Kitchen && building.Owner == farmer.Owner && building.CanTake(ItemType.Crop))
				return building;
		return null;
	}

	private Building NearestStorage(Unit farmer)
	{
		Building best = null;
		int bestDistance = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building.Owner != farmer.Owner || !building.IsBuilt || building is not (Core or Warehouse))
				continue;
			int distance = TileDistance(farmer, building);
			if (distance < bestDistance)
				(best, bestDistance) = (building, distance);
		}
		return best;
	}

	private CropField ChooseField(Unit farmer)
	{
		CropField best = null;
		int bestScore = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building is not CropField field || field.Owner != farmer.Owner || !field.IsBuilt || !field.IsRipe)
				continue;
			int score = TileDistance(farmer, field);
			foreach (var other in _units)
				if (other != farmer && other.Job == field && other.Health > 0)
					score += FieldTakenPenalty;
			if (score < bestScore)
				(best, bestScore) = (field, score);
		}
		return best;
	}

	/// <summary>The nearest finished kitchen with room for the load, else the nearest toybox/warehouse with room.</summary>
	private Building ChooseDropOff(Unit farmer)
	{
		Building best = null;
		int bestScore = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building.Owner != farmer.Owner || !building.IsBuilt || !building.CanTake(farmer.Carrying))
				continue;
			bool storage = building is Core or Warehouse;
			int score = TileDistance(farmer, building) + (storage ? 1000 : 0); // kitchens first
			if (score < bestScore)
				(best, bestScore) = (building, score);
		}
		return best;
	}

	// ---------------------------------------------------------------------------------------------
	// Walking for builders and farmers.

	private enum Walk { Walking, Arrived, Unreachable }

	/// <summary>One step along the unit's path to the building; Arrived once it stands next to it.</summary>
	private Walk WalkTo(Unit unit, Building target)
	{
		if (IsAround(target, unit.TileX, unit.TileY))
			return Walk.Arrived;

		if (unit.Path == null || unit.PathTarget != target)
		{
			unit.Path = FindPathTo(unit.TileX, unit.TileY, target);
			unit.PathIndex = 0;
			unit.PathTarget = target;
			if (unit.Path == null)
				return Walk.Unreachable;
		}

		StepAlongPath(unit);
		return Walk.Walking;
	}

	/// <summary>Walks to the centre of the next tile on the unit's path (a step never overshoots it).</summary>
	private void StepAlongPath(Unit unit)
	{
		if (unit.PathIndex < unit.Path.Count)
		{
			const int half = UnitStats.SubTile / 2;
			var (tx, ty) = unit.Path[unit.PathIndex];
			int goalX = tx * UnitStats.SubTile + half, goalY = ty * UnitStats.SubTile + half;
			TryStep(unit, goalX, goalY);
			if (CloseTo(unit, goalX, goalY))
				unit.PathIndex++;
		}
	}

	private static void Face(Unit unit, Building building)
	{
		unit.MoveX = building.CenterX - unit.X;
		unit.MoveY = building.CenterY - unit.Y;
	}

	private static int TileDistance(Unit unit, Building building) =>
		System.Math.Abs(building.X - unit.TileX) + System.Math.Abs(building.Y - unit.TileY);

	/// <summary>The nearest unfinished site of the builder's owner, preferring ones nobody works on yet.</summary>
	private Building ChooseSite(Unit builder)
	{
		Building best = null;
		int bestScore = int.MaxValue;
		foreach (var building in _buildings)
		{
			if (building.Owner != builder.Owner || building.IsBuilt)
				continue;
			int distance = System.Math.Abs(building.X - builder.TileX) + System.Math.Abs(building.Y - builder.TileY);
			int score = distance + CrowdPenalty * BuildersOn(building);
			if (score < bestScore)
			{
				best = building;
				bestScore = score;
			}
		}
		return best;
	}

	/// <summary>Whether tile (x, y) is on the building or next to it (diagonals count).</summary>
	private static bool IsAround(Building building, int x, int y) =>
		x >= building.X - 1 && x <= building.X + building.Width &&
		y >= building.Y - 1 && y <= building.Y + building.Height;

	private static readonly (int dx, int dy)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

	/// <summary>
	/// Breadth-first path over floor (buildings don't block builders) to any tile around the building.
	/// The start tile is not included. Null if unreachable.
	/// </summary>
	private List<(int X, int Y)> FindPathTo(int fromX, int fromY, Building target) =>
		GridSearch.PathTo(Map.Width, Map.Height, fromX, fromY, IsFloor, (x, y) => IsAround(target, x, y));

	private bool IsFloor(int x, int y) => Map[x, y] == TileType.Floor;
}
