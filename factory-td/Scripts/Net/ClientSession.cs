using System;
using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.Net;

/// <summary>
/// The joining player (player 1). Says hello with its version, answers the host's challenge with a proof of
/// the password, then waits for Start and plays the turns the host sends.
/// </summary>
public sealed class ClientSession : MatchSession
{
	private readonly string _password;
	private readonly string _buildId;
	private readonly byte[] _nonce = Protocol.RandomBytes(Protocol.NonceSize);
	private bool _connected;

	public ClientSession(ITransport transport, string password, string name, Func<MatchSettings, World> createWorld,
		string buildId = null)
		: base(transport, name, createWorld)
	{
		_password = password;
		_buildId = buildId ?? Protocol.BuildId;
		LocalPlayer = 1;
		State = SessionState.Connecting;
	}

	protected override bool HasOpponent => _connected;

	protected override void SendToOpponent(byte[] packet)
	{
		if (_connected)
			Transport.Send(ITransport.Server, packet);
	}

	protected override void OnEvent(NetEvent e)
	{
		switch (e.Kind)
		{
			case NetEventKind.Connected:
				_connected = true;
				LastHeard = Now;
				State = SessionState.Joining;
				Transport.Send(ITransport.Server, new PacketWriter(MessageType.Hello)
					.Int(Protocol.Version).Text(_buildId, 64).Text(Name).Bytes(_nonce).ToArray());
				break;
			case NetEventKind.Disconnected:
				_connected = false;
				End(State is SessionState.Connecting or SessionState.Joining ? EndReason.CouldNotConnect : EndReason.OpponentLeft);
				break;
			case NetEventKind.Data:
				LastHeard = Now;
				OnData(e.Data);
				break;
		}
	}

	private void OnData(byte[] data)
	{
		var r = new PacketReader(data);
		if (HandleCommon(ITransport.Server, ref r))
			return;
		switch (r.Type)
		{
			case MessageType.Challenge when State == SessionState.Joining:
				var challenge = r.Bytes(Protocol.ChallengeSize).ToArray();
				if (r.Ok)
					Transport.Send(ITransport.Server, new PacketWriter(MessageType.Proof)
						.Bytes(Protocol.PasswordProof(_password, challenge, _nonce)).ToArray());
				break;
			case MessageType.Welcome when State == SessionState.Joining:
				LocalPlayer = r.Byte();
				OpponentName = r.Text();
				State = SessionState.Lobby;
				break;
			case MessageType.Reject:
				Rejection = (RejectReason)r.Byte();
				End(EndReason.Rejected);
				break;
			case MessageType.Start when State == SessionState.Lobby:
				int seed = r.Int();
				int delay = r.Byte();
				var theme = (MapTheme)r.Byte();
				if (r.Ok && theme <= MapTheme.Garden)
					BeginMatch(new MatchSettings(seed, theme), delay);
				break;
			case MessageType.Turn when State == SessionState.Playing:
				var commands = Protocol.ReadCommands(ref r, out long step);
				if (commands != null)
					AddTurn(step, commands);
				break;
			case MessageType.Desync when State == SessionState.Playing:
				EndWithDesync(r.Int());
				break;
		}
	}

	protected override void OnLocalInput(long step, List<PlayerCommand> commands) =>
		Transport.Send(ITransport.Server, Protocol.Commands(MessageType.Input, step, commands));

	protected override void OnLocalHash(long step, ulong hash)
	{
		base.OnLocalHash(step, hash);
		Transport.Send(ITransport.Server, new PacketWriter(MessageType.Hash).Int((int)step).Long(hash).ToArray());
	}
}
