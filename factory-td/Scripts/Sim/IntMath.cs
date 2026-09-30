namespace FactoryTD.Sim;

/// <summary>Integer-only math for the simulation (floats are not deterministic across machines).</summary>
public static class IntMath
{
	/// <summary>Floor of the square root of a non-negative value.</summary>
	public static int Sqrt(int value)
	{
		if (value <= 0)
			return 0;
		int x = value;
		int y = (x + 1) / 2;
		while (y < x)
		{
			x = y;
			y = (x + value / x) / 2;
		}
		return x;
	}
}
