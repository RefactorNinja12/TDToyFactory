using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FactoryTD.Sim;

namespace FactoryTD.Net;

public enum MessageType : byte
{
	Hello,      // client → host: protocol version, build id, name, client nonce
	Challenge,  // host → client: 32 random bytes
	Proof,      // client → host: HMAC-SHA256(password, challenge + client nonce)
	Welcome,    // host → client: you are player N, host name
	Reject,     // host → client: why not (then the host disconnects)
	Start,      // host → client: map seed, input delay, which map
	Input,      // client → host: the client's commands for one step
	Turn,       // host → client: every player's commands for one step
	Hash,       // both: the checksum after a step
	Desync,     // host → client: checksums differed at this step
	Ping,       // both: a time stamp to echo
	Pong,       // both: the echoed time stamp
	Bye,        // both: leaving the match
}

public enum RejectReason : byte
{
	WrongVersion,
	WrongPassword,
	Blocked,
	Full,
}

/// <summary>What a match is played on, chosen by the host: the seed (where the obstacles lie) and the map.</summary>
public readonly record struct MatchSettings(int Seed, MapTheme Theme = MapTheme.Nursery);

/// <summary>Message layouts, the build id and the password proof. All numbers little-endian.</summary>
public static class Protocol
{
	/// <summary>Bump when a message layout changes (2: Start carries the map).</summary>
	public const int Version = 2;

	public const int DefaultPort = 7777;
	public const int NonceSize = 16, ChallengeSize = 32, ProofSize = 32;
	public const int MaxNameBytes = 32;

	/// <summary>Differs between any two builds of the game, so only identical builds play together.</summary>
	public static string BuildId { get; } = typeof(World).Assembly.ManifestModule.ModuleVersionId.ToString("N");

	/// <summary>
	/// What the client sends instead of the password: it proves knowing the password for this one challenge
	/// only, so a recorded proof is worth nothing next time, and the password itself never leaves the machine.
	/// </summary>
	public static byte[] PasswordProof(string password, byte[] challenge, byte[] clientNonce)
	{
		var data = new byte[challenge.Length + clientNonce.Length];
		challenge.CopyTo(data, 0);
		clientNonce.CopyTo(data, challenge.Length);
		return HMACSHA256.HashData(Encoding.UTF8.GetBytes(password ?? ""), data);
	}

	public static bool ProofMatches(byte[] expected, byte[] got) =>
		got != null && got.Length == expected.Length && CryptographicOperations.FixedTimeEquals(expected, got);

	public static byte[] RandomBytes(int count) => RandomNumberGenerator.GetBytes(count);

	public static byte[] Commands(MessageType type, long step, IReadOnlyList<PlayerCommand> commands)
	{
		var w = new PacketWriter(type);
		w.Int((int)step);
		w.Byte((byte)commands.Count);
		Span<byte> bytes = stackalloc byte[PlayerCommand.Size];
		foreach (var command in commands)
		{
			command.Write(bytes);
			w.Bytes(bytes);
		}
		return w.ToArray();
	}

	/// <summary>Reads the commands of an Input/Turn message (after its type byte); null if malformed.</summary>
	public static List<PlayerCommand> ReadCommands(ref PacketReader r, out long step)
	{
		step = r.Int();
		int count = r.Byte();
		var list = new List<PlayerCommand>(count);
		for (int i = 0; i < count; i++)
		{
			var command = PlayerCommand.Read(r.Bytes(PlayerCommand.Size));
			if (command == null)
				return null;
			list.Add(command.Value);
		}
		return r.Ok ? list : null;
	}
}

/// <summary>Builds a packet: a type byte, then fields.</summary>
public sealed class PacketWriter
{
	private readonly List<byte> _bytes = new();

	public PacketWriter(MessageType type) => _bytes.Add((byte)type);

	public PacketWriter Byte(byte value) { _bytes.Add(value); return this; }

	public PacketWriter Int(int value)
	{
		Span<byte> b = stackalloc byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(b, value);
		return Bytes(b);
	}

	public PacketWriter Long(ulong value)
	{
		Span<byte> b = stackalloc byte[8];
		BinaryPrimitives.WriteUInt64LittleEndian(b, value);
		return Bytes(b);
	}

	public PacketWriter Bytes(ReadOnlySpan<byte> value)
	{
		foreach (byte b in value)
			_bytes.Add(b);
		return this;
	}

	/// <summary>A length-prefixed UTF-8 string, cut to <paramref name="maxBytes"/>.</summary>
	public PacketWriter Text(string value, int maxBytes = Protocol.MaxNameBytes)
	{
		var bytes = Encoding.UTF8.GetBytes(value ?? "");
		int length = Math.Min(bytes.Length, maxBytes);
		Byte((byte)length);
		return Bytes(bytes.AsSpan(0, length));
	}

	public byte[] ToArray() => _bytes.ToArray();
}

/// <summary>Reads a packet. Reading past the end gives zeros and clears <see cref="Ok"/>, so bad packets can't crash.</summary>
public ref struct PacketReader
{
	private readonly ReadOnlySpan<byte> _data;
	private int _at;

	public PacketReader(ReadOnlySpan<byte> data)
	{
		_data = data;
		_at = data.Length > 0 ? 1 : 0;
		Ok = data.Length > 0;
		Type = data.Length > 0 ? (MessageType)data[0] : (MessageType)byte.MaxValue; // empty = nothing known
	}

	public MessageType Type { get; }

	public bool Ok { get; private set; }

	public ReadOnlySpan<byte> Bytes(int count)
	{
		if (_at + count > _data.Length)
		{
			Ok = false;
			_at = _data.Length;
			return new byte[count];
		}
		var slice = _data.Slice(_at, count);
		_at += count;
		return slice;
	}

	public byte Byte() => Bytes(1)[0];

	public int Int() => BinaryPrimitives.ReadInt32LittleEndian(Bytes(4));

	public ulong Long() => BinaryPrimitives.ReadUInt64LittleEndian(Bytes(8));

	public string Text() => Encoding.UTF8.GetString(Bytes(Byte()));
}
