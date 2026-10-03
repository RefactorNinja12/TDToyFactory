using System;
using System.Buffers.Binary;

namespace FactoryTD.Sim;

public enum CommandKind : byte
{
	Place,
	Remove,
	Configure,
}

/// <summary>
/// One thing a player does to the world: everything a player (or bot) can change goes through these, so the
/// same commands can be sent over the network and run on the same tick on every machine (lockstep).
/// </summary>
public readonly record struct PlayerCommand(CommandKind Kind, int Player, int X, int Y,
	BuildingType Type = BuildingType.Conveyor, Direction Facing = Direction.East)
{
	public static PlayerCommand Place(int player, BuildingType type, int x, int y, Direction facing) =>
		new(CommandKind.Place, player, x, y, type, facing);

	public static PlayerCommand Remove(int player, int x, int y) => new(CommandKind.Remove, player, x, y);

	public static PlayerCommand Configure(int player, int x, int y) => new(CommandKind.Configure, player, x, y);

	/// <summary>Bytes per command on the wire: kind, player, type, facing, x, y (16-bit each).</summary>
	public const int Size = 8;

	public void Write(Span<byte> to)
	{
		to[0] = (byte)Kind;
		to[1] = (byte)Player;
		to[2] = (byte)Type;
		to[3] = (byte)Facing;
		BinaryPrimitives.WriteInt16LittleEndian(to[4..], (short)X);
		BinaryPrimitives.WriteInt16LittleEndian(to[6..], (short)Y);
	}

	/// <summary>Reads a command; null when the bytes can't be one (unknown kind, type or facing).</summary>
	public static PlayerCommand? Read(ReadOnlySpan<byte> from)
	{
		if (from.Length < Size || from[0] > (byte)CommandKind.Configure || !Enum.IsDefined((BuildingType)from[2])
			|| from[3] > (byte)Direction.North)
			return null;
		return new PlayerCommand((CommandKind)from[0], from[1], BinaryPrimitives.ReadInt16LittleEndian(from[4..]),
			BinaryPrimitives.ReadInt16LittleEndian(from[6..]), (BuildingType)from[2], (Direction)from[3]);
	}
}

/// <summary>Where a player's commands go: straight into the world (local play) or out to the match (online).</summary>
public interface ICommandSink
{
	/// <summary>Ticks between sending a command and it taking effect (0 = at once).</summary>
	int DelayTicks { get; }

	void Send(PlayerCommand command);
}

/// <summary>Local play: commands take effect at once.</summary>
public sealed class DirectCommands : ICommandSink
{
	private readonly World _world;

	public DirectCommands(World world) => _world = world;

	public int DelayTicks => 0;

	public void Send(PlayerCommand command) => _world.Apply(command);
}
