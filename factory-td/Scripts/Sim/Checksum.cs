namespace FactoryTD.Sim;

/// <summary>
/// 64-bit FNV-1a style hash over integers. Used for World.Checksum: two machines running the same
/// match must produce the same value every tick (lockstep desync detection, determinism tests).
/// </summary>
public struct StateHash
{
	private const ulong Prime = 1099511628211UL;
	private ulong _value;

	public static StateHash Start() => new() { _value = 14695981039346656037UL };

	public ulong Value => _value;

	public void Add(long value)
	{
		for (int i = 0; i < 8; i++)
		{
			_value ^= (byte)(value >> (i * 8));
			_value *= Prime;
		}
	}

	public void Add(bool value) => Add(value ? 1 : 0);
}
