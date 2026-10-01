using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>
/// Units take up room: after everyone has moved, units that overlap are pushed apart, half each, so a
/// crowd spreads out instead of stacking on one spot. Integer maths and a fixed order (deterministic).
/// A push that would end in a wall or a big toy is left out.
/// </summary>
public sealed partial class World
{
	/// <summary>Most a pair is pushed apart in one tick (sub-tile units), so crowds spread smoothly.</summary>
	private const int MaxPushPerTick = UnitStats.SubTile / 8;

	private readonly Dictionary<int, List<Unit>> _unitsByTile = new();

	/// <summary>How much room a unit takes: its radius in sub-tile units (two units keep their radii apart).</summary>
	public static int UnitRadius(UnitType type) => type switch
	{
		UnitType.BrickGolem => UnitStats.SubTile * 2 / 5,
		UnitType.RcCar => UnitStats.SubTile / 3,
		UnitType.PlasticSoldier => UnitStats.SubTile * 3 / 10,
		_ => UnitStats.SubTile / 4, // mice: workers and cheese hunters
	};

	/// <summary>
	/// Close enough to a path tile's centre to go on to the next one. Not exact: units walking the same way
	/// push each other a little, and would never all stand exactly on the same centre.
	/// </summary>
	private static bool CloseTo(Unit unit, int x, int y)
	{
		const int near = UnitStats.SubTile / 3;
		int dx = unit.X - x, dy = unit.Y - y;
		return dx * dx + dy * dy <= near * near;
	}

	private void SeparateUnits()
	{
		foreach (var list in _unitsByTile.Values)
			list.Clear();
		foreach (var unit in _units)
		{
			if (unit.Health <= 0)
				continue;
			int key = unit.TileY * Map.Width + unit.TileX;
			if (!_unitsByTile.TryGetValue(key, out var list))
				_unitsByTile[key] = list = new List<Unit>();
			list.Add(unit);
		}

		foreach (var unit in _units)
		{
			if (unit.Health <= 0)
				continue;
			for (int dy = -1; dy <= 1; dy++)
				for (int dx = -1; dx <= 1; dx++)
				{
					int key = (unit.TileY + dy) * Map.Width + unit.TileX + dx;
					if (!_unitsByTile.TryGetValue(key, out var list))
						continue;
					foreach (var other in list)
						if (other.Id > unit.Id) // each pair once
							PushApart(unit, other);
				}
		}
	}

	private void PushApart(Unit a, Unit b)
	{
		int minimum = UnitRadius(a.Type) + UnitRadius(b.Type);
		int dx = b.X - a.X, dy = b.Y - a.Y;
		if (dx > minimum || dx < -minimum || dy > minimum || dy < -minimum)
			return;
		int distance = IntMath.Sqrt(dx * dx + dy * dy);
		if (distance >= minimum)
			return;
		if (distance == 0)
		{
			// Right on top of each other: split along a direction picked from their ids.
			(dx, dy) = ((a.Id + b.Id) % 4) switch { 0 => (1, 0), 1 => (0, 1), 2 => (-1, 0), _ => (0, -1) };
			distance = 1;
		}
		int push = System.Math.Min((minimum - distance + 1) / 2, MaxPushPerTick);
		int px = dx * push / distance, py = dy * push / distance;
		if (px == 0 && py == 0)
			return;
		Shift(a, -px, -py);
		Shift(b, px, py);
	}

	private void Shift(Unit unit, int dx, int dy)
	{
		int x = unit.X + dx, y = unit.Y + dy;
		int tileX = x / UnitStats.SubTile, tileY = y / UnitStats.SubTile;
		if (!Map.InBounds(tileX, tileY) || Map[tileX, tileY] != TileType.Floor)
			return;
		unit.X = x;
		unit.Y = y;
	}
}
