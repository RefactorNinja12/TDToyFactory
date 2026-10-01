using System.Collections.Generic;
using FactoryTD.Net;
using Godot;

namespace FactoryTD.View.Net;

/// <summary>
/// Godot's ENet (UDP, works the same on every OS) as a plain reliable, ordered packet pipe for the sessions in
/// Scripts/Net. No RPCs: every byte is ours. The host listens on a port; a client connects to an address.
/// </summary>
public sealed class ENetTransport : ITransport
{
	private const int MaxClients = 4; // more than one so late joiners hear "full" instead of nothing

	private readonly ENetMultiplayerPeer _peer = new();
	private readonly List<NetEvent> _events = new();
	private readonly bool _isClient;
	private MultiplayerPeer.ConnectionStatus _lastStatus;

	private ENetTransport(bool isClient)
	{
		_isClient = isClient;
		_peer.TransferMode = MultiplayerPeer.TransferModeEnum.Reliable;
		_peer.PeerConnected += id => _events.Add(new NetEvent(NetEventKind.Connected, (int)id, Address: AddressOf((int)id)));
		_peer.PeerDisconnected += id => _events.Add(new NetEvent(NetEventKind.Disconnected, (int)id));
	}

	/// <summary>Starts listening; null and an error text if the port can't be used.</summary>
	public static ENetTransport Host(int port, out string error)
	{
		var transport = new ENetTransport(isClient: false);
		var result = transport._peer.CreateServer(port, MaxClients);
		error = result == Error.Ok ? null : $"Kunde inte öppna port {port} ({result}). Är den upptagen?";
		return result == Error.Ok ? transport : null;
	}

	/// <summary>Starts connecting; the session hears Connected or Disconnected.</summary>
	public static ENetTransport Join(string address, int port, out string error)
	{
		var transport = new ENetTransport(isClient: true);
		var result = transport._peer.CreateClient(address, port);
		error = result == Error.Ok ? null : $"Kunde inte ansluta till {address}:{port} ({result}).";
		return result == Error.Ok ? transport : null;
	}

	public void Send(int peer, byte[] data)
	{
		if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
			return;
		_peer.SetTargetPeer(peer);
		_peer.PutPacket(data);
	}

	public void Disconnect(int peer)
	{
		if (_isClient)
			_peer.Close();
		else
			_peer.DisconnectPeer(peer);
	}

	public IEnumerable<NetEvent> Poll()
	{
		_peer.Poll();
		var status = _peer.GetConnectionStatus();
		// A client that never got through: ENet just goes back to disconnected.
		if (_isClient && _lastStatus == MultiplayerPeer.ConnectionStatus.Connecting && status == MultiplayerPeer.ConnectionStatus.Disconnected)
			_events.Add(new NetEvent(NetEventKind.Disconnected, ITransport.Server));
		_lastStatus = status;
		while (_peer.GetAvailablePacketCount() > 0)
		{
			int from = _peer.GetPacketPeer();
			_events.Add(new NetEvent(NetEventKind.Data, from, _peer.GetPacket()));
		}
		var events = _events.ToArray();
		_events.Clear();
		return events;
	}

	/// <summary>Sends what is queued, then closes (leaving the match).</summary>
	public void Close()
	{
		_peer.Host?.Flush();
		_peer.Close();
	}

	private string AddressOf(int id) =>
		_isClient ? "host" : _peer.GetPeer(id)?.GetRemoteAddress() ?? "?";
}
