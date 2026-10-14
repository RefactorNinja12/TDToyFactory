namespace FactoryTD.Sim;

// World partial: army units (targeting, shooting, walking towards the enemy core) and projectiles.
public sealed partial class World
{
	/// <summary>Tests: toyboxes take no damage, so a bot can be watched for a whole match.</summary>
	internal bool CoresInvulnerable { get; set; }

	private void TickUnit(Unit unit)
	{
		unit.PrevX = unit.X;
		unit.PrevY = unit.Y;
		unit.MoveX = unit.MoveY = 0;
		if (unit.AttackCooldown > 0)
			unit.AttackCooldown--;
		if (unit.SlamCooldown > 0)
			unit.SlamCooldown--;
		if (Winner >= 0 || _cores[EnemyOf(unit.Owner)] == null)
			return;
		if (PowerStats.IsElectric(unit.Type) && !FreePower && TickCharge(unit))
			return;

		if (unit.Type == UnitType.BrickGolem && TickSlam(unit))
			return;

		// Enemy buildings in the way are always within range, so they get shot before the unit walks on;
		// its own buildings it just walks over.
		var (targetUnit, targetBuilding) = SelectTarget(unit, UnitStats.Range(unit.Type), includeCore: true);
		if (targetUnit != null || targetBuilding != null)
		{
			// Stand still and shoot.
			int aimX = targetUnit?.X ?? targetBuilding.CenterX;
			int aimY = targetUnit?.Y ?? targetBuilding.CenterY;
			unit.MoveX = aimX - unit.X;
			unit.MoveY = aimY - unit.Y;
			// Short reach (golems, cheese hunters) is a punch; the rest shoot.
			Attack(unit, targetUnit, targetBuilding, UnitStats.Def(unit.Type).Range <= UnitStats.SubTile * 3 / 2 ? DamageKind.Punch : DamageKind.Bullet);
			return;
		}

		// Nothing in range: go for anything it can see (except the core) before heading on to the core.
		// Only what it can walk straight up to: a target behind a wall would hold it at the wall forever.
		var (seenUnit, seenBuilding) = SelectTarget(unit, UnitStats.Sight(unit.Type), includeCore: false, reachableOnly: true);
		if (seenUnit != null || seenBuilding != null)
		{
			int goalX = seenUnit?.X ?? seenBuilding.CenterX;
			int goalY = seenUnit?.Y ?? seenBuilding.CenterY;
			if (TryStep(unit, goalX, goalY))
				return;
			// A wall is in the way of the straight line; fall back to the flow field.
		}

		// Head for the centre of the next tile downhill in the flow field.
		var (nextX, nextY) = FieldFor(unit.Owner, unit.Type).NextTile(unit.TileX, unit.TileY);
		const int half = UnitStats.SubTile / 2;
		if (GetBuilding(nextX, nextY) is Wall wall && wall.Owner != unit.Owner && wall.Blocks(unit.Owner))
		{
			// The way leads through an enemy wall: walk up to it and beat on it (everyone punches walls).
			if (DistanceSquaredTo(wall, unit.X, unit.Y) <= (long)WallStats.HitReach * WallStats.HitReach)
			{
				unit.MoveX = wall.CenterX - unit.X;
				unit.MoveY = wall.CenterY - unit.Y;
				if (Attack(unit, null, wall, DamageKind.Punch))
					wall.HitBy(this, unit);
				return;
			}
		}
		TryStep(unit, nextX * UnitStats.SubTile + half, nextY * UnitStats.SubTile + half);
	}

	/// <summary>Shoved by a pendulum: slides along until the push is spent or something solid stops it.</summary>
	private void TickPush(Unit unit)
	{
		unit.PrevX = unit.X;
		unit.PrevY = unit.Y;
		int moveX = unit.MoveX, moveY = unit.MoveY;
		int speed = System.Math.Abs(unit.PushX) + System.Math.Abs(unit.PushY);
		bool moved = TryStep(unit, unit.X + unit.PushX, unit.Y + unit.PushY, speed);
		(unit.MoveX, unit.MoveY) = (moveX, moveY); // keeps facing where it was going
		unit.PushTicks = moved ? unit.PushTicks - 1 : 0;
		if (unit.PushTicks == 0)
			unit.AirTicks = 0; // landed (at once if something solid cut the throw short)
		unit.Path = null;
	}

	/// <summary>Where and when golems slammed the ground lately (for the view's shock rings; not simulation state).</summary>
	public System.Collections.Generic.IReadOnlyList<SlamMark> RecentSlams => _recentSlams;
	private readonly System.Collections.Generic.List<SlamMark> _recentSlams = new();
	private const int SlamMarkTicks = 40;

	/// <summary>
	/// The golem's slam (<see cref="GolemStats"/>): an enemy unit it could walk straight up to comes close, so it raises
	/// its hands and stands still for the wind-up, then slams. True while it is busy slamming (it does nothing else).
	/// </summary>
	private bool TickSlam(Unit golem)
	{
		if (golem.SlamTicks > 0)
		{
			if (--golem.SlamTicks == 0)
				Slam(golem);
			return true;
		}
		if (golem.SlamCooldown > 0 || FindNearestEnemy(golem.Owner, golem.X, golem.Y, GolemStats.SlamTrigger, golem) == null)
			return false;
		golem.SlamTicks = GolemStats.SlamWindupTicks;
		return true;
	}

	/// <summary>Hits every enemy unit round the golem (not behind a wall), in spawn order, and throws it outwards.</summary>
	private void Slam(Unit golem)
	{
		golem.SlamCooldown = GolemStats.SlamCooldownTicks;
		_recentSlams.Add(new SlamMark(golem.X, golem.Y, TickCount));
		long radius2 = (long)GolemStats.SlamRadius * GolemStats.SlamRadius;
		int hits = 0;
		foreach (var other in _units)
		{
			if (hits >= GolemStats.SlamMaxTargets)
				break;
			long dx = other.X - golem.X, dy = other.Y - golem.Y;
			if (other.Owner == golem.Owner || other.Health <= 0 || dx * dx + dy * dy > radius2 || !WalkableLine(golem, other.TileX, other.TileY))
				continue;
			other.Health -= GolemStats.SlamDamage * Combat.DamagePercent(DamageKind.Area, UnitStats.Armor(other.Type)) / 100;
			Throw(other, (int)dx, (int)dy);
			hits++;
		}
	}

	/// <summary>Sends the unit flying straight along (dx, dy) for <see cref="GolemStats.ThrowDistance"/>.</summary>
	private static void Throw(Unit unit, int dx, int dy)
	{
		if (dx == 0 && dy == 0) // right under the golem: a direction picked from its id
			(dx, dy) = (unit.Id % 4) switch { 0 => (1, 0), 1 => (0, 1), 2 => (-1, 0), _ => (0, -1) };
		int length = IntMath.Sqrt(dx * dx + dy * dy);
		int step = GolemStats.ThrowDistance / GolemStats.ThrowTicks;
		unit.PushX = dx * step / length;
		unit.PushY = dy * step / length;
		unit.PushTicks = unit.AirTicks = GolemStats.ThrowTicksFor(unit.Type);
	}

	private void ForgetOldSlams() => _recentSlams.RemoveAll(m => TickCount - m.Tick > SlamMarkTicks);

	/// <summary>Fires at the target if the unit is ready; false while it reloads.</summary>
	private bool Attack(Unit unit, Unit targetUnit, Building targetBuilding, DamageKind kind)
	{
		if (unit.AttackCooldown > 0)
			return false;
		var def = UnitStats.Def(unit.Type);
		int damage = targetBuilding != null ? def.Damage * def.BuildingDamagePercent / 100 : def.Damage;
		if (unit.Type == UnitType.Samurai)
			unit.SwingCount++;
		if (unit.Type == UnitType.Samurai && targetUnit != null)
			Sweep(unit, targetUnit, damage, def.ShotTravelTicks);
		else
			Fire(new Projectile(kind, unit.Owner, damage, 0, def.ShotTravelTicks, unit.X, unit.Y, targetUnit, targetBuilding));
		unit.AttackCooldown = def.AttackTicks;
		return true;
	}

	/// <summary>
	/// The samurai's sword: the target and every other enemy unit in the arc in front of it (towards the target,
	/// within <see cref="SamuraiStats.SweepReach"/>) take a slash, at most <see cref="SamuraiStats.SweepMaxTargets"/>,
	/// in spawn order.
	/// </summary>
	private void Sweep(Unit unit, Unit target, int damage, int travelTicks)
	{
		Fire(new Projectile(DamageKind.Slash, unit.Owner, damage, 0, travelTicks, unit.X, unit.Y, target, null));
		int hits = 1;
		foreach (var other in _units)
		{
			if (hits >= SamuraiStats.SweepMaxTargets)
				break;
			if (other == target || other.Owner == unit.Owner || other.Health <= 0 || !SamuraiStats.InArc(unit, target, other))
				continue;
			Fire(new Projectile(DamageKind.Slash, unit.Owner, damage, 0, travelTicks, unit.X, unit.Y, other, null));
			hits++;
		}
	}

	/// <summary>A wall just finished: everyone it blocks who stands on it steps off to the nearest free tile.</summary>
	private void WallRaised(Wall wall)
	{
		MarkChanged(wall);
		const int half = UnitStats.SubTile / 2;
		foreach (var unit in _units)
		{
			if (GetBuilding(unit.TileX, unit.TileY) != wall || !wall.Blocks(unit.Owner))
				continue;
			int owner = unit.Owner;
			var path = GridSearch.PathTo(Map.Width, Map.Height, unit.TileX, unit.TileY,
				(x, y) => Map[x, y] == TileType.Floor, (x, y) => !BlocksUnit(owner, x, y));
			if (path == null || path.Count == 0)
				continue;
			var (tx, ty) = path[^1];
			unit.X = unit.PrevX = tx * UnitStats.SubTile + half;
			unit.Y = unit.PrevY = ty * UnitStats.SubTile + half;
			unit.Path = null;
		}
	}

	/// <summary>Moves the unit up to its speed towards a point. Refuses (returns false) to step into a wall.</summary>
	private bool TryStep(Unit unit, int goalX, int goalY, int speed = -1)
	{
		int dx = goalX - unit.X;
		int dy = goalY - unit.Y;
		int length = IntMath.Sqrt(dx * dx + dy * dy);
		if (length == 0)
			return true;

		if (speed < 0)
			speed = UnitStats.Speed(unit.Type);
		if (length > speed)
		{
			dx = dx * speed / length;
			dy = dy * speed / length;
		}

		int tileX = (unit.X + dx) / UnitStats.SubTile, tileY = (unit.Y + dy) / UnitStats.SubTile;
		if (BlocksUnit(unit.Owner, tileX, tileY))
			return false;
		// No slipping diagonally between two walls that touch at a corner.
		if (tileX != unit.TileX && tileY != unit.TileY && (WallBlocks(unit.Owner, tileX, unit.TileY) || WallBlocks(unit.Owner, unit.TileX, tileY)))
			return false;

		unit.X += dx;
		unit.Y += dy;
		unit.MoveX = dx;
		unit.MoveY = dy;
		return true;
	}

	/// <summary>
	/// The closest living unit not owned by <paramref name="owner"/> within <paramref name="range"/>
	/// (sub-tile units) of the point. Ties go to the oldest unit, so the result is deterministic.
	/// </summary>
	public Unit FindNearestEnemy(int owner, int x, int y, int range) => FindNearestEnemy(owner, x, y, range, null);

	/// <summary>
	/// With a <paramref name="walker"/>: only enemies it can walk straight up to. <paramref name="toEdge"/>: the range
	/// reaches the edge of the enemy's body, not its centre (a golem is big: mice keep their distance from its middle).
	/// </summary>
	private Unit FindNearestEnemy(int owner, int x, int y, int range, Unit walker, bool toEdge = false)
	{
		Unit best = null;
		long bestDistance = long.MaxValue;
		foreach (var unit in _units)
		{
			if (unit.Owner == owner || unit.Health <= 0)
				continue;
			long dx = unit.X - x, dy = unit.Y - y;
			long distance = dx * dx + dy * dy;
			long reach = range + (toEdge ? UnitRadius(unit.Type) : 0);
			if (distance <= reach * reach && (best == null || distance < bestDistance) && (walker == null || WalkableLine(walker, unit.TileX, unit.TileY)))
			{
				best = unit;
				bestDistance = distance;
			}
		}
		return best;
	}

	/// <summary>
	/// The best target within <paramref name="range"/>, in priority order:
	/// 1. enemy units, 2. enemy towers, 3. any other enemy building, 4. the enemy core last (if included).
	/// So an attack tears down the base around the core first. Nearest wins within a tier;
	/// ties go to the oldest unit / earliest-built building (deterministic).
	/// <paramref name="reachableOnly"/>: only targets it can walk straight up to (<see cref="WalkableLine"/>).
	/// </summary>
	internal (Unit, Building) SelectTarget(Unit unit, int range, bool includeCore, bool reachableOnly = false)
	{
		long range2 = (long)range * range;

		if (UnitStats.Def(unit.Type).TargetsUnits)
		{
			var enemyUnit = FindNearestEnemy(unit.Owner, unit.X, unit.Y, range, reachableOnly ? unit : null, toEdge: true);
			if (enemyUnit != null)
				return (enemyUnit, null);
		}

		// Only the tiles that can be in range (a building counts when its nearest edge is in range). Ties go to
		// the building placed first, exactly as a scan over the whole building list would choose.
		Building tower = null, other = null, core = null;
		long towerDistance = range2, otherDistance = range2;
		int reach = range / UnitStats.SubTile + 2, mark = ++_scanMark;
		for (int ty = unit.TileY - reach; ty <= unit.TileY + reach; ty++)
			for (int tx = unit.TileX - reach; tx <= unit.TileX + reach; tx++)
			{
				var building = GetBuilding(tx, ty);
				// Walls are only attacked when the way leads through them (see TickUnit).
				if (building == null || building.Owner == unit.Owner || building.ScanMark == mark || building is Wall or Trap)
					continue;
				building.ScanMark = mark;
				long distance = DistanceSquaredTo(building, unit.X, unit.Y);
				if (distance > range2 || (reachableOnly && !WalkableLine(unit, NearestTileX(building, unit.TileX), NearestTileY(building, unit.TileY))))
					continue;

				if (building is Tower)
				{
					if (tower == null || Closer(distance, building, towerDistance, tower))
						(tower, towerDistance) = (building, distance);
				}
				else if (building is Core)
				{
					if (includeCore)
						core = building;
				}
				else if (other == null || Closer(distance, building, otherDistance, other))
				{
					(other, otherDistance) = (building, distance);
				}
			}
		return (null, tower ?? other ?? core);
	}

	/// <summary>
	/// Whether nothing blocks the unit on the straight line of tiles from its own to (toX, toY): no wall or
	/// obstacle, and no squeezing diagonally past a corner. Strict, so walking straight there never gets stuck.
	/// </summary>
	private bool WalkableLine(Unit unit, int toX, int toY)
	{
		int x = unit.TileX, y = unit.TileY;
		int ax = System.Math.Abs(toX - x), ay = -System.Math.Abs(toY - y);
		int sx = System.Math.Sign(toX - x), sy = System.Math.Sign(toY - y);
		int error = ax + ay;
		while (x != toX || y != toY)
		{
			int e2 = 2 * error, fromX = x, fromY = y;
			if (e2 >= ay) { error += ay; x += sx; }
			if (e2 <= ax) { error += ax; y += sy; }
			if (BlocksUnit(unit.Owner, x, y))
				return false;
			if (x != fromX && y != fromY && (BlocksUnit(unit.Owner, x, fromY) || BlocksUnit(unit.Owner, fromX, y)))
				return false;
		}
		return true;
	}

	private static int NearestTileX(Building building, int x) => System.Math.Clamp(x, building.X, building.X + building.Width - 1);
	private static int NearestTileY(Building building, int y) => System.Math.Clamp(y, building.Y, building.Y + building.Height - 1);

	private static bool Closer(long distance, Building building, long bestDistance, Building best) =>
		distance < bestDistance || (distance == bestDistance && building.Serial < best.Serial);

	/// <summary>Squared distance from a point to the nearest edge of the building's footprint (0 if inside).</summary>
	private static long DistanceSquaredTo(Building building, int x, int y)
	{
		int left = building.X * UnitStats.SubTile, right = (building.X + building.Width) * UnitStats.SubTile;
		int top = building.Y * UnitStats.SubTile, bottom = (building.Y + building.Height) * UnitStats.SubTile;
		long dx = x < left ? left - x : x > right ? x - right : 0;
		long dy = y < top ? top - y : y > bottom ? y - bottom : 0;
		return dx * dx + dy * dy;
	}

	public void Fire(Projectile projectile) => _projectiles.Add(projectile);

	private void TickProjectiles()
	{
		for (int i = 0; i < _projectiles.Count; i++)
		{
			var shot = _projectiles[i];
			var unit = shot.TargetUnit != null && shot.TargetUnit.Health > 0 ? shot.TargetUnit : null;
			var building = shot.TargetBuilding != null && IsStanding(shot.TargetBuilding) ? shot.TargetBuilding : null;

			// Single-target shots home in on a unit while it lives.
			if (unit != null && shot.Kind != DamageKind.Area)
			{
				shot.ToX = unit.X;
				shot.ToY = unit.Y;
			}

			if (--shot.TicksLeft > 0)
				continue;

			if (shot.Kind == DamageKind.Area)
			{
				long radius2 = (long)shot.AreaRadius * shot.AreaRadius;
				foreach (var victim in _units)
				{
					long dx = victim.X - shot.ToX, dy = victim.Y - shot.ToY;
					if (victim.Owner != shot.Owner && victim.Health > 0 && dx * dx + dy * dy <= radius2)
						Hit(victim, shot);
				}
			}
			else if (unit != null)
			{
				Hit(unit, shot);
			}
			else if (building != null)
			{
				if (!(CoresInvulnerable && building is Core))
					building.TakeDamage(shot.Damage);
				if (building.Health == 0 && building.Type != BuildingType.Core)
					Destroy(building);
			}
		}
		_projectiles.RemoveAll(p => p.TicksLeft <= 0);
	}

	private static void Hit(Unit unit, Projectile shot)
	{
		int percent = Combat.DamagePercent(shot.Kind, UnitStats.Armor(unit.Type));
		unit.Health -= shot.Damage * percent / 100;
	}
}
