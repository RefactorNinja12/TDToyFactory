using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>
/// Which conveyor sprite to show: straight, or a curve when it is fed from exactly one side and not from behind.
/// The curve sprite is a right turn east -> south; flipped vertically it is a left turn east -> north.
/// </summary>
public readonly record struct ConveyorLook(bool Curve, bool FlipV, int QuarterTurns)
{
	public static ConveyorLook For(World world, Conveyor conveyor)
	{
		var facing = conveyor.Facing;
		// Right turn: e.g. moving east, then turning south (clockwise).
		return Of(facing, IsFedMoving(world, conveyor, facing), IsFedMoving(world, conveyor, facing.RotatedCounterClockwise()),
			IsFedMoving(world, conveyor, facing.RotatedClockwise()));
	}

	/// <summary>
	/// A planned belt (not built yet) pointing <paramref name="facing"/> that items reach moving
	/// <paramref name="moving"/> (null: nothing feeds it yet).
	/// </summary>
	public static ConveyorLook Turning(Direction facing, Direction? moving) =>
		Of(facing, moving == facing, moving == facing.RotatedCounterClockwise(), moving == facing.RotatedClockwise());

	private static ConveyorLook Of(Direction facing, bool fromBack, bool rightTurn, bool leftTurn)
	{
		if (!fromBack && rightTurn != leftTurn)
		{
			var reference = rightTurn ? Direction.South : Direction.North;
			return new ConveyorLook(true, leftTurn, ((int)facing - (int)reference + 4) & 3);
		}
		return new ConveyorLook(false, false, (int)facing);
	}

	/// <summary>Whether the neighbour that items moving in <paramref name="moving"/> would come from feeds this conveyor.</summary>
	private static bool IsFedMoving(World world, Conveyor conveyor, Direction moving)
	{
		var source = world.GetBuilding(conveyor.X - moving.DX(), conveyor.Y - moving.DY());
		return source != null && source.Width == 1 && source.Height == 1 && source.OutputsToward(moving);
	}
}
