namespace FactoryTD.Sim;

public static class CraneStats
{
	/// <summary>Tiles the rail reaches forward from the base, over the field.</summary>
	public const int Reach = 5;

	/// <summary>Sub-tiles the claw moves per tick: 1/8 tile, so 2.5 tiles per second.</summary>
	public const int Speed = UnitStats.SubTile / 8;

	/// <summary>Ticks to close the claw on an item, and to let go of it over the output.</summary>
	public const int GrabTicks = 6, DropTicks = 4;
}

public enum ClawState : byte
{
	Home,   // at the base, looking for an extractor with something to pick
	Out,    // running out along the rail
	Grab,   // closing on the item
	Back,   // carrying it home
	Drop,   // letting go over the output (waits while the output is full)
}

/// <summary>
/// A toy claw crane on a rail on stilts, built at the edge of a resource field and facing into it. The rail
/// reaches <see cref="CraneStats.Reach"/> tiles forward over whatever is below (it takes no tiles). The claw
/// picks one item at a time from the nearest of the owner's extractors under the rail that has something
/// stored, carries it back and drops it out of the back of the base, onto a belt or into a building; if the
/// output is full it waits holding the item. So extractors in the middle of a field, with no belt beside
/// them, still get their items out. Needs no power (wind-up).
/// </summary>
public sealed class ClawCrane : Building
{
	public ClawCrane(int x, int y, Direction facing, int owner)
		: base(BuildingType.ClawCrane, x, y, facing, owner) { }

	public ClawState State { get; private set; }

	/// <summary>How far out along the rail the claw is, in sub-tiles from the base (0 = home).</summary>
	public int Claw { get; private set; }

	/// <summary>Where the claw was before the last tick, so the view can interpolate.</summary>
	public int PrevClaw { get; private set; }

	public ItemType Carrying { get; private set; }

	private int _target, _timer;

	/// <summary>Items come out of the back (the base faces into the field).</summary>
	public override bool OutputsToward(Direction direction) => direction == Facing.Opposite();

	/// <summary>The tile <paramref name="tiles"/> along the rail (1 = the first tile past the base).</summary>
	public (int X, int Y) RailTile(int tiles) => (X + Facing.DX() * tiles, Y + Facing.DY() * tiles);

	/// <summary>The extractors the claw can reach: the owner's, under the rail, within the reach.</summary>
	public static System.Collections.Generic.IEnumerable<Extractor> Served(World world, int x, int y, Direction facing, int owner)
	{
		for (int d = 1; d <= CraneStats.Reach; d++)
			if (world.GetBuilding(x + facing.DX() * d, y + facing.DY() * d) is Extractor e && e.Owner == owner)
				yield return e;
	}

	public override void Tick(World world)
	{
		PrevClaw = Claw;
		switch (State)
		{
			case ClawState.Home:
				for (int d = 1; d <= CraneStats.Reach; d++)
				{
					var (tx, ty) = RailTile(d);
					if (world.GetBuilding(tx, ty) is Extractor { IsBuilt: true, Stored: > 0 } e && e.Owner == Owner)
					{
						_target = d * UnitStats.SubTile;
						State = ClawState.Out;
						break;
					}
				}
				break;
			case ClawState.Out:
				Claw = System.Math.Min(Claw + CraneStats.Speed, _target);
				if (Claw == _target)
				{
					State = ClawState.Grab;
					_timer = CraneStats.GrabTicks;
				}
				break;
			case ClawState.Grab:
				if (--_timer > 0)
					break;
				var (gx, gy) = RailTile(_target / UnitStats.SubTile);
				if (world.GetBuilding(gx, gy) is Extractor extractor && extractor.Owner == Owner && extractor.TryTakeOne())
					Carrying = extractor.Output;
				State = ClawState.Back;
				break;
			case ClawState.Back:
				Claw = System.Math.Max(Claw - CraneStats.Speed, 0);
				if (Claw == 0)
				{
					State = Carrying == ItemType.None ? ClawState.Home : ClawState.Drop;
					_timer = CraneStats.DropTicks;
				}
				break;
			case ClawState.Drop:
				if (_timer > 0)
				{
					_timer--;
					break;
				}
				if (TryPush(world, Carrying, Facing.Opposite()))
				{
					Carrying = ItemType.None;
					State = ClawState.Home;
				}
				break;
		}
	}

	protected override void HashState(ref StateHash hash)
	{
		hash.Add((int)State); hash.Add(Claw); hash.Add((int)Carrying); hash.Add(_target); hash.Add(_timer);
	}
}
