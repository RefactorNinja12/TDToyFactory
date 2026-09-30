using System;
using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

public static class DragPath
{
	/// <summary>
	/// Every tile from (but not including) the start to the target, one step at a time so fast mouse moves
	/// leave no gaps: first along x, then along y. Each step says which way it moved.
	/// </summary>
	public static IEnumerable<(int X, int Y, Direction Moving)> Walk(int fromX, int fromY, int toX, int toY)
	{
		int x = fromX, y = fromY;
		while (x != toX || y != toY)
		{
			Direction moving;
			if (x != toX) { moving = toX > x ? Direction.East : Direction.West; x += Math.Sign(toX - x); }
			else { moving = toY > y ? Direction.South : Direction.North; y += Math.Sign(toY - y); }
			yield return (x, y, moving);
		}
	}
}
