namespace FactoryTD.Sim;

// World partial: army units (targeting, shooting, walking towards the enemy core) and projectiles.
public sealed partial class World
{
	private void TickUnit(Unit unit)
	{
		unit.PrevX = unit.X;
		unit.PrevY = unit.Y;
		unit.MoveX = unit.MoveY = 0;
		if (unit.AttackCooldown > 0)
			unit.AttackCooldown--;
		if (Winner >= 0 || _cores[EnemyOf(unit.Owner)] == null)
			return;
		if (PowerStats.IsElectric(unit.Type) && !FreePower && TickCharge(unit))
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
		var (seenUnit, seenBuilding) = SelectTarget(unit, UnitStats.Sight(unit.Type), includeCore: false);
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

	/// <summary>Fires at the target if the unit is ready; false while it reloads.</summary>
	private bool Attack(Unit unit, Unit targetUnit, Building targetBuilding, DamageKind kind)
	{
		if (unit.AttackCooldown > 0)
			return false;
		var def = UnitStats.Def(unit.Type);
		int damage = targetBuilding != null ? def.Damage * def.BuildingDamagePercent / 100 : def.Damage;
		Fire(new Projectile(kind, unit.Owner, damage, 0, def.ShotTravelTicks, unit.X, unit.Y, targetUnit, targetBuilding));
		unit.AttackCooldown = def.AttackTicks;
		return true;
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
	public Unit FindNearestEnemy(int owner, int x, int y, int range)
	{
		Unit best = null;
		long bestDistance = (long)range * range;
		foreach (var unit in _units)
		{
			if (unit.Owner == owner || unit.Health <= 0)
				continue;
			long dx = unit.X - x, dy = unit.Y - y;
			long distance = dx * dx + dy * dy;
			if (distance <= bestDistance && (best == null || distance < bestDistance))
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
	/// </summary>
	internal (Unit, Building) SelectTarget(Unit unit, int range, bool includeCore)
	{
		long range2 = (long)range * range;

		if (UnitStats.Def(unit.Type).TargetsUnits)
		{
			var enemyUnit = FindNearestEnemy(unit.Owner, unit.X, unit.Y, range);
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
				if (building == null || building.Owner == unit.Owner || building.ScanMark == mark || building is Wall)
					continue;
				building.ScanMark = mark;
				long distance = DistanceSquaredTo(building, unit.X, unit.Y);
				if (distance > range2)
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
