using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>
/// Building a splitter, sorter or junction straight onto one of your own belts: the belt is taken up (its
/// cost comes back) and the new piece goes in its place, so there is no need to remove the belt first. Two
/// ordinary commands, like the junctions the belt planner puts in, so it works the same online.
/// </summary>
public static class BeltReplace
{
	/// <summary>The pieces that go into a belt line.</summary>
	public static bool GoesOnBelts(BuildingType type) =>
		type is BuildingType.Splitter or BuildingType.Sorter or BuildingType.Junction;

	/// <summary>Whether <paramref name="type"/> can replace the owner's belt at (x, y) (and it can be paid for).</summary>
	public static bool CanReplace(World world, BuildingType type, int x, int y, int owner)
	{
		if (!GoesOnBelts(type) || !world.Map.InBounds(x, y))
			return false;
		return world.GetBuilding(x, y) is Conveyor belt && belt.Owner == owner
			&& world.Players[owner].CanAfford(BuildingRules.Cost(type));
	}

	public static List<PlayerCommand> Commands(BuildingType type, int x, int y, Direction facing, int owner) => new()
	{
		PlayerCommand.Remove(owner, x, y),
		PlayerCommand.Place(owner, type, x, y, facing),
	};
}
