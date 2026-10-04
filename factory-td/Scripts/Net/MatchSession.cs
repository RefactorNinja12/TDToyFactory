using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactoryTD.Sim;

namespace FactoryTD.Net;

public enum SessionState
{
	Connecting, // client: waiting for the host to answer
	Joining,    // client: version and password check
	Lobby,      // connected (or, for the host, waiting for someone), match not started
	Playing,
	Ended,
}

public enum EndReason
{
	None,
	Left,           // we left
	OpponentLeft,
	ConnectionLost, // nothing heard for a while
	CouldNotConnect,
	Rejected,       // see Rejection
	Desync,         // see DesyncStep
}

/// <summary>
/// One side of an online match (lockstep). Both machines run the whole simulation; only commands travel.
/// A command sent while stepping step s runs at step s + InputDelay on both machines. The host collects
/// every player's commands for a step into a turn and sends it out; nobody steps without its turn (a late
/// packet stalls the game, it is never guessed). Every <see cref="HashEvery"/> steps the client sends its
/// checksum and the host compares, so a desync stops the match instead of playing on wrong.
/// The caller drives it: <see cref="Update"/> with the clock every frame, <see cref="TryStep"/> at 20/s.
/// </summary>
public abstract class MatchSession
{
	public const int HashEvery = 20;
	public const int TimeoutMs = 10_000, PingEveryMs = 1000, WaitingAfterMs = 1000;
	private const int ReportCommands = 200;

	protected readonly ITransport Transport;
	private readonly Func<MatchSettings, World> _createWorld;
	private readonly List<PlayerCommand> _pending = new();
	private readonly Dictionary<long, List<PlayerCommand>> _turns = new();
	private readonly Dictionary<long, ulong> _localHashes = new(), _remoteHashes = new();
	private readonly Queue<(long Step, PlayerCommand Command)> _recent = new();
	private long _lastPing, _stalledSince = -1;
	private bool _updated;

	protected MatchSession(ITransport transport, string name, Func<MatchSettings, World> createWorld)
	{
		Transport = transport;
		Name = name ?? "";
		_createWorld = createWorld;
		Commands = new LockstepCommands(this);
	}

	public SessionState State { get; protected set; }
	public EndReason EndReason { get; private set; }
	public RejectReason? Rejection { get; protected set; }
	public long DesyncStep { get; private set; } = -1;
	public string Name { get; }
	public string OpponentName { get; protected set; } = "";
	public int LocalPlayer { get; protected set; }
	public int InputDelay { get; private set; }
	public int Seed => Settings.Seed;

	/// <summary>What the host chose: the map seed and which map.</summary>
	public MatchSettings Settings { get; private set; }
	public int RttMs { get; private set; }
	public World World { get; private set; }

	/// <summary>Where this machine's player (or its bot) sends commands.</summary>
	public ICommandSink Commands { get; }

	/// <summary>The other side has been holding up the game long enough to say so.</summary>
	public bool IsWaiting => _stalledSince >= 0 && Now - _stalledSince >= WaitingAfterMs;

	protected long Now { get; private set; }
	protected long LastHeard { get; set; }

	/// <summary>Handles what arrived, pings, and gives up on a silent opponent.</summary>
	public void Update(long nowMs)
	{
		Now = nowMs;
		if (!_updated)
		{
			_updated = true;
			LastHeard = nowMs; // the timeouts count from the first update
		}
		foreach (var e in Transport.Poll())
		{
			if (State == SessionState.Ended)
				break;
			OnEvent(e);
		}
		if (State is SessionState.Lobby or SessionState.Playing && HasOpponent)
		{
			if (Now - LastHeard > TimeoutMs)
			{
				End(EndReason.ConnectionLost);
				return;
			}
			if (Now - _lastPing >= PingEveryMs)
			{
				_lastPing = Now;
				SendToOpponent(new PacketWriter(MessageType.Ping).Int((int)Now).ToArray());
			}
		}
		else if (State is SessionState.Connecting or SessionState.Joining && Now - LastHeard > TimeoutMs)
		{
			End(EndReason.CouldNotConnect);
		}
		OnUpdate();
	}

	/// <summary>Runs the next step if its turn is here. False = waiting for the other side (or not playing).</summary>
	public bool TryStep()
	{
		if (State != SessionState.Playing)
			return false;
		long step = World.TickCount;
		if (!_turns.Remove(step, out var turn))
		{
			if (step >= InputDelay)
			{
				if (_stalledSince < 0)
					_stalledSince = Now;
				return false;
			}
			turn = new List<PlayerCommand>(); // the first steps: nobody could have sent anything yet
		}
		_stalledSince = -1;

		var input = _pending.ToList();
		_pending.Clear();
		OnLocalInput(step + InputDelay, input);

		foreach (var command in turn)
		{
			World.Apply(command);
			_recent.Enqueue((step, command));
			if (_recent.Count > ReportCommands)
				_recent.Dequeue();
		}
		World.Tick();
		if (World.TickCount % HashEvery == 0)
			OnLocalHash(World.TickCount, World.Checksum());
		return true;
	}

	/// <summary>Leave the match (tells the other side).</summary>
	public void Leave()
	{
		if (State == SessionState.Ended)
			return;
		SendToOpponent(new PacketWriter(MessageType.Bye).ToArray());
		End(EndReason.Left);
	}

	/// <summary>What to write to the desync log: where, both checksums, the last commands.</summary>
	public string DesyncReport()
	{
		var text = new StringBuilder();
		text.AppendLine($"Desync at tick {DesyncStep}, build {Protocol.BuildId}, local player {LocalPlayer}, seed {Seed}, map {Settings.Theme}, delay {InputDelay}");
		text.AppendLine($"local checksum {Hash(_localHashes, DesyncStep)}, remote {Hash(_remoteHashes, DesyncStep)}");
		foreach (var (step, command) in _recent)
			text.AppendLine($"{step}: {command}");
		return text.ToString();
	}

	private static string Hash(Dictionary<long, ulong> hashes, long step) =>
		hashes.TryGetValue(step, out var h) ? h.ToString("X16") : "?";

	protected abstract bool HasOpponent { get; }

	protected abstract void SendToOpponent(byte[] packet);

	protected abstract void OnEvent(NetEvent e);

	protected virtual void OnUpdate() { }

	/// <summary>This machine's commands for <paramref name="step"/> are final.</summary>
	protected abstract void OnLocalInput(long step, List<PlayerCommand> commands);

	protected virtual void OnLocalHash(long step, ulong hash)
	{
		_localHashes[step] = hash;
		CompareHashes(step);
	}

	protected void BeginMatch(MatchSettings settings, int inputDelay)
	{
		Settings = settings;
		InputDelay = inputDelay;
		World = _createWorld(settings);
		State = SessionState.Playing;
	}

	protected void AddTurn(long step, List<PlayerCommand> commands) => _turns[step] = commands;

	protected void AddRemoteHash(long step, ulong hash)
	{
		_remoteHashes[step] = hash;
		CompareHashes(step);
	}

	private void CompareHashes(long step)
	{
		if (!_localHashes.TryGetValue(step, out var mine) || !_remoteHashes.TryGetValue(step, out var theirs))
			return;
		if (mine != theirs)
		{
			OnDesync(step);
			return;
		}
		// Both agree: forget them (keep the maps small) except what a later report could need.
		_localHashes.Remove(step - HashEvery * 10);
		_remoteHashes.Remove(step - HashEvery * 10);
	}

	protected virtual void OnDesync(long step) => EndWithDesync(step);

	protected void EndWithDesync(long step)
	{
		DesyncStep = step;
		End(EndReason.Desync);
	}

	/// <summary>Messages both sides understand. Returns false if it wasn't one of them.</summary>
	protected bool HandleCommon(int peer, ref PacketReader r)
	{
		switch (r.Type)
		{
			case MessageType.Ping:
				int stamp = r.Int();
				if (r.Ok)
					Transport.Send(peer, new PacketWriter(MessageType.Pong).Int(stamp).ToArray());
				return true;
			case MessageType.Pong:
				int sent = r.Int();
				if (r.Ok)
					RttMs = (int)Math.Max(0, Now - sent);
				return true;
			case MessageType.Bye:
				End(EndReason.OpponentLeft);
				return true;
			default:
				return false;
		}
	}

	protected void End(EndReason reason)
	{
		if (State == SessionState.Ended)
			return;
		State = SessionState.Ended;
		EndReason = reason;
		OnEnded();
	}

	protected virtual void OnEnded() { }

	/// <summary>Collects this machine's commands until the next step makes them final.</summary>
	private sealed class LockstepCommands : ICommandSink
	{
		private readonly MatchSession _session;

		public LockstepCommands(MatchSession session) => _session = session;

		/// <summary>Sent now, made final at the next step, run InputDelay steps after that.</summary>
		public int DelayTicks => _session.InputDelay + 1;

		public void Send(PlayerCommand command) =>
			_session._pending.Add(command with { Player = _session.LocalPlayer });
	}
}
