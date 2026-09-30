using System;
using System.Collections.Generic;

namespace FactoryTD.Sim;

public struct BeltItem
{
	public ItemType Type;

	/// <summary>0 = just entered (at the edge it came in from), Conveyor.Length = at the front edge.</summary>
	public int Progress;

	/// <summary>Progress before the last tick, so the view can interpolate between ticks.</summary>
	public int PrevProgress;

	/// <summary>The direction the item was moving when it came onto this belt.
	/// It differs from Facing on a turn, which lets the view draw the item going around the corner.</summary>
	public Direction EntryDirection;
}

/// <summary>
/// Moves items one tile towards Facing and hands them to whatever is in front.
/// Accepts items from the back and both sides. Integer-only, for lockstep.
/// </summary>
public sealed class Conveyor : Building
{
	public const int Length = 64;   // progress units per tile
	public const int Speed = 4;     // progress units per tick -> 16 ticks (0.8 s) per tile
	public const int Spacing = 32;  // minimum distance between items -> max 2 items per tile

	// [0] is the item furthest along.
	private readonly List<BeltItem> _items = new();

	public IReadOnlyList<BeltItem> Items => _items;

	public Conveyor(int x, int y, Direction facing, int owner)
		: base(BuildingType.Conveyor, x, y, facing, owner) { }

	public override void Tick(World world)
	{
		for (int i = 0; i < _items.Count; i++)
		{
			var item = _items[i];
			item.PrevProgress = item.Progress;
			int limit = i == 0 ? Length : _items[i - 1].Progress - Spacing;
			if (item.Progress < limit)
				item.Progress = Math.Min(item.Progress + Speed, limit);
			_items[i] = item;
		}

		if (_items.Count > 0 && _items[0].Progress >= Length)
		{
			var next = world.GetBuilding(FrontX, FrontY);
			if (next != null && next.Offer(_items[0].Type, Facing))
				_items.RemoveAt(0);
		}
	}

	protected override void HashState(ref StateHash hash)
	{
		hash.Add(_items.Count);
		foreach (var item in _items)
		{
			hash.Add((int)item.Type); hash.Add(item.Progress); hash.Add((int)item.EntryDirection);
		}
	}

	public override bool TryAccept(ItemType item, Direction moving)
	{
		// Never take items from the tile we are pointing at (two belts facing each other).
		if (moving == Facing.Opposite())
			return false;
		if (_items.Count > 0 && _items[^1].Progress < Spacing)
			return false;

		_items.Add(new BeltItem { Type = item, EntryDirection = moving });
		return true;
	}
}
