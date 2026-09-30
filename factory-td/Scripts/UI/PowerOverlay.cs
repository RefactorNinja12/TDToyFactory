using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>A powered area to draw: centre and radius in tiles, and whether its network has any energy.</summary>
public readonly record struct PowerCircle(float X, float Y, int Radius, bool Charged);

/// <summary>What the power overlay draws: coverage circles and sagging cords. Positions are in tiles.</summary>
public static class PowerOverlay
{
	/// <summary>How far a cord sags in the middle, as a share of its length.</summary>
	public const float SagPerLength = 0.12f;

	public static List<PowerCircle> Circles(World world, int player)
	{
		var circles = new List<PowerCircle>();
		foreach (var network in world.Power.Networks)
		{
			if (network.Owner != player)
				continue;
			foreach (var node in network.Nodes)
			{
				int radius = PowerGrid.Radius(node);
				if (radius > 0)
					circles.Add(new PowerCircle(Center(node.CenterX), Center(node.CenterY), radius, network.Energy > 0));
			}
		}
		return circles;
	}

	/// <summary>Every cord of the player, as points along a hanging curve from one node's centre to the other's.</summary>
	public static List<(float X, float Y)[]> Cords(World world, int player, int segments = 8, System.Func<Building, bool> include = null)
	{
		var cords = new List<(float X, float Y)[]>();
		foreach (var (a, b) in world.Power.Cords)
			if (a.Owner == player && (include == null || (include(a) && include(b))))
				cords.Add(CordPoints(Center(a.CenterX), Center(a.CenterY), Center(b.CenterX), Center(b.CenterY), segments));
		return cords;
	}

	/// <summary>A parabola that sags downwards (+y) by <see cref="SagPerLength"/> of the cord's length in the middle.</summary>
	public static (float X, float Y)[] CordPoints(float ax, float ay, float bx, float by, int segments)
	{
		float length = System.MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
		float sag = length * SagPerLength;
		var points = new (float X, float Y)[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float t = i / (float)segments;
			points[i] = (ax + (bx - ax) * t, ay + (by - ay) * t + 4 * sag * t * (1 - t));
		}
		return points;
	}

	/// <summary>
	/// The player's nodes a new pylon or charger at (x, y) would link to, nearest first: for the placement preview.
	/// </summary>
	public static List<Building> PreviewLinks(World world, int player, int x, int y)
	{
		long s = UnitStats.SubTile, range2 = PowerStats.LinkRange * s * PowerStats.LinkRange * s;
		long cx = x * s + s / 2, cy = y * s + s / 2;
		var links = new List<(long Distance, int Order, Building Node)>();
		int order = 0;
		foreach (var building in world.Buildings)
		{
			order++;
			if (building.Owner != player || !building.IsBuilt || !PowerGrid.IsNode(building))
				continue;
			long dx = building.CenterX - cx, dy = building.CenterY - cy;
			if (dx * dx + dy * dy <= range2)
				links.Add((dx * dx + dy * dy, order, building));
		}
		links.Sort((p, q) => p.Distance != q.Distance ? p.Distance.CompareTo(q.Distance) : p.Order.CompareTo(q.Order));
		return links.ConvertAll(l => l.Node);
	}

	private static float Center(int subTile) => subTile / (float)UnitStats.SubTile;
}
