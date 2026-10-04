using System;
using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;

namespace FactoryTD.Net;

/// <summary>
/// The hosting player (player 0, the "server"). Lets one opponent in after a version check and a password
/// proof (the password itself never travels), blocks an address for a while after repeated wrong
/// passwords, starts the match, merges both players' commands into turns and checks the checksums.
/// </summary>
public sealed class HostSession : MatchSession
{
	public const int MaxFailures = 3, FailureWindowMs = 60_000, BlockMs = 30_000;
	private const int CloseAfterRejectMs = 500;

	private readonly string _password;
	private readonly string _buildId;
	private readonly Dictionary<int, (string Address, string Name, byte[] Challenge, byte[] Expected)> _joining = new();
	private readonly Dictionary<string, List<long>> _failures = new();
	private readonly Dictionary<string, long> _blockedUntil = new();
	private readonly Dictionary<int, long> _closing = new();
	private readonly Dictionary<long, List<PlayerCommand>>[] _inputs = { new(), new() };
	private int _opponent = -1;

	public HostSession(ITransport transport, string password, string name, Func<MatchSettings, World> createWorld,
		string buildId = null)
		: base(transport, name, createWorld)
	{
		_password = password;
		_buildId = buildId ?? Protocol.BuildId;
		LocalPlayer = 0;
		State = SessionState.Lobby;
	}

	/// <summary>Someone got in and the match hasn't started: the host may press Start.</summary>
	public bool CanStart => State == SessionState.Lobby && _opponent >= 0;

	protected override bool HasOpponent => _opponent >= 0;

	/// <summary>Input delay for a measured round trip: the turn must be back before the step that needs it.</summary>
	public static int DelayFor(int rttMs) => Math.Clamp((rttMs + 40 + 49) / 50 + 1, 3, 12);

	public void Start(int seed, MapTheme theme = MapTheme.Nursery)
	{
		if (!CanStart)
			return;
		int delay = DelayFor(RttMs);
		Transport.Send(_opponent, new PacketWriter(MessageType.Start).Int(seed).Byte((byte)delay).Byte((byte)theme).ToArray());
		BeginMatch(new MatchSettings(seed, theme), delay);
	}

	protected override void SendToOpponent(byte[] packet)
	{
		if (_opponent >= 0)
			Transport.Send(_opponent, packet);
	}

	protected override void OnEvent(NetEvent e)
	{
		switch (e.Kind)
		{
			case NetEventKind.Connected:
				OnConnected(e.Peer, e.Address);
				break;
			case NetEventKind.Disconnected:
				_joining.Remove(e.Peer);
				_closing.Remove(e.Peer);
				if (e.Peer != _opponent)
					break;
				_opponent = -1;
				if (State == SessionState.Playing)
					End(EndReason.OpponentLeft);
				break;
			case NetEventKind.Data when !_closing.ContainsKey(e.Peer):
				OnData(e.Peer, e.Data);
				break;
		}
	}

	private void OnConnected(int peer, string address)
	{
		if (_blockedUntil.TryGetValue(address, out long until) && until > Now)
			Reject(peer, RejectReason.Blocked);
		else if (_opponent >= 0 || State != SessionState.Lobby)
			Reject(peer, RejectReason.Full);
		else
			_joining[peer] = (address, "", null, null);
	}

	private void OnData(int peer, byte[] data)
	{
		var r = new PacketReader(data);
		if (peer == _opponent)
		{
			LastHeard = Now;
			if (!HandleCommon(peer, ref r))
				OnOpponentMessage(ref r);
			return;
		}
		if (!_joining.TryGetValue(peer, out var join))
			return;
		// Before the proof is accepted only Hello and Proof are listened to.
		if (r.Type == MessageType.Hello && join.Challenge == null)
		{
			int version = r.Int();
			string build = r.Text();
			string name = r.Text();
			var nonce = r.Bytes(Protocol.NonceSize).ToArray();
			if (!r.Ok || version != Protocol.Version || build != _buildId)
			{
				Reject(peer, RejectReason.WrongVersion);
				return;
			}
			var challenge = Protocol.RandomBytes(Protocol.ChallengeSize);
			_joining[peer] = (join.Address, name, challenge, Protocol.PasswordProof(_password, challenge, nonce));
			Transport.Send(peer, new PacketWriter(MessageType.Challenge).Bytes(challenge).ToArray());
		}
		else if (r.Type == MessageType.Proof && join.Expected != null)
		{
			var proof = r.Bytes(Protocol.ProofSize).ToArray();
			if (!r.Ok || !Protocol.ProofMatches(join.Expected, proof))
			{
				WrongPassword(peer, join.Address);
				return;
			}
			_joining.Remove(peer);
			_opponent = peer;
			OpponentName = join.Name;
			LastHeard = Now;
			Transport.Send(peer, new PacketWriter(MessageType.Welcome).Byte(1).Text(Name).ToArray());
		}
	}

	private void OnOpponentMessage(ref PacketReader r)
	{
		switch (r.Type)
		{
			case MessageType.Input when State == SessionState.Playing:
				var commands = Protocol.ReadCommands(ref r, out long step);
				if (commands == null || step < World.TickCount || _inputs[1].ContainsKey(step))
					return;
				// The client may only command its own player.
				_inputs[1][step] = commands.Where(c => c.Player == 1).ToList();
				TryMakeTurn(step);
				break;
			case MessageType.Hash when State == SessionState.Playing:
				long at = r.Int();
				ulong hash = r.Long();
				if (r.Ok)
					AddRemoteHash(at, hash);
				break;
		}
	}

	protected override void OnLocalInput(long step, List<PlayerCommand> commands)
	{
		_inputs[0][step] = commands;
		TryMakeTurn(step);
	}

	private void TryMakeTurn(long step)
	{
		if (!_inputs[0].TryGetValue(step, out var mine) || !_inputs[1].TryGetValue(step, out var theirs))
			return;
		_inputs[0].Remove(step);
		_inputs[1].Remove(step);
		var turn = mine.Concat(theirs).ToList();
		AddTurn(step, turn);
		Transport.Send(_opponent, Protocol.Commands(MessageType.Turn, step, turn));
	}

	protected override void OnDesync(long step)
	{
		SendToOpponent(new PacketWriter(MessageType.Desync).Int((int)step).ToArray());
		EndWithDesync(step);
	}

	private void WrongPassword(int peer, string address)
	{
		if (!_failures.TryGetValue(address, out var times))
			_failures[address] = times = new List<long>();
		times.RemoveAll(t => Now - t > FailureWindowMs);
		times.Add(Now);
		if (times.Count >= MaxFailures)
		{
			_blockedUntil[address] = Now + BlockMs;
			times.Clear();
		}
		Reject(peer, RejectReason.WrongPassword);
	}

	/// <summary>Says why, then closes the connection a moment later (so the reason arrives first).</summary>
	private void Reject(int peer, RejectReason reason)
	{
		_joining.Remove(peer);
		Transport.Send(peer, new PacketWriter(MessageType.Reject).Byte((byte)reason).ToArray());
		_closing[peer] = Now + CloseAfterRejectMs;
	}

	protected override void OnUpdate()
	{
		foreach (int peer in _closing.Where(c => c.Value <= Now).Select(c => c.Key).ToList())
		{
			_closing.Remove(peer);
			Transport.Disconnect(peer);
		}
	}
}
