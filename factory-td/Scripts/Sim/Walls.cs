namespace FactoryTD.Sim;

/// <summary>Balance for walls. Distances are in sub-tile units, costs in flow-field units (10 = one tile).</summary>
public static class WallStats
{
	/// <summary>A wall brick takes exactly 5 golem punches (12 damage × 200% against buildings = 24).</summary>
	public const int BrickHealth = 120;

	/// <summary>Walls are hit at contact: a unit attacks a wall when the wall's nearest edge is this close.</summary>
	public const int HitReach = UnitStats.SubTile * 6 / 5;

	/// <summary>
	/// What crossing an enemy wall costs units that can't smash through: more than any way round, so they
	/// only break walls when the base is sealed off, and then where the fewest walls are.
	/// </summary>
	public const int SealedWallPenalty = 1_000_000;

	/// <summary>Golems: about the time 5 punches take, in golem walking (≈ 8 tiles).</summary>
	public const int GolemWallPenalty = 80;

	public static bool IsWall(BuildingType type) => type is BuildingType.BrickWall or BuildingType.Gate;

	/// <summary>Units that smash through walls rather than walk far round them.</summary>
	public static bool BreaksWalls(UnitType type) => type == UnitType.BrickGolem;
}

/// <summary>A brick wall or a gate: blocks walking once built. A gate lets its owner's units through.</summary>
public sealed class Wall : Building
{
	public Wall(BuildingType type, int x, int y, Direction facing, int owner)
		: base(type, x, y, facing, owner)
	{
	}

	public override bool OutputsToward(Direction direction) => false;

	/// <summary>Whether this wall stops a unit of <paramref name="player"/> from stepping onto it.</summary>
	public bool Blocks(int player) => IsBuilt && !(Type == BuildingType.Gate && Owner == player);
}
