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

	// ---- Electric fence: zaps whoever hits it, from its grid ----
	public const int ZapDamage = 10;            // electronics (RC cars) take double
	public const int ZapEnergy = 300;           // 40 zaps per battery
	public const int ZapCooldownTicks = 20;

	public static bool IsWall(BuildingType type) => type is BuildingType.BrickWall or BuildingType.Gate or BuildingType.ElectricFence;

	public static int ZapDamageTo(UnitType type) =>
		UnitStats.Armor(type) == ArmorClass.Electronic ? ZapDamage * 2 : ZapDamage;

	/// <summary>Units that smash through walls rather than walk far round them.</summary>
	public static bool BreaksWalls(UnitType type) => type == UnitType.BrickGolem;
}

/// <summary>
/// A brick wall, a gate or an electric fence: blocks walking once built. A gate lets its owner's units through;
/// a powered fence zaps the units that hit it.
/// </summary>
public sealed class Wall : Building
{
	public Wall(BuildingType type, int x, int y, Direction facing, int owner)
		: base(type, x, y, facing, owner)
	{
	}

	public override bool OutputsToward(Direction direction) => false;

	/// <summary>Zaps so far (the view sparks when it changes).</summary>
	public int Zaps { get; private set; }

	private int _zapCooldown;

	/// <summary>Whether this wall stops a unit of <paramref name="player"/> from stepping onto it.</summary>
	public bool Blocks(int player) => IsBuilt && !(Type == BuildingType.Gate && Owner == player);

	public override void Tick(World world)
	{
		if (Type != BuildingType.ElectricFence)
			return;
		if (_zapCooldown > 0)
			_zapCooldown--;
		NoPower = !world.HasPower(this, WallStats.ZapEnergy);
	}

	/// <summary>A unit hits the wall: a powered fence that is ready zaps it.</summary>
	internal void HitBy(World world, Unit attacker)
	{
		if (Type != BuildingType.ElectricFence || !IsBuilt || _zapCooldown > 0 || !world.TryDrawPower(this, WallStats.ZapEnergy))
			return;
		attacker.Health -= WallStats.ZapDamageTo(attacker.Type);
		_zapCooldown = WallStats.ZapCooldownTicks;
		Zaps++;
	}

	protected override void HashState(ref StateHash hash)
	{
		hash.Add(Zaps); hash.Add(_zapCooldown);
	}
}
