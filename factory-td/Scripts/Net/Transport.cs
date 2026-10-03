using System.Collections.Generic;

namespace FactoryTD.Net;

public enum NetEventKind
{
	Connected,
	Data,
	Disconnected,
}

/// <summary>Something that happened on the connection: a peer came or went, or a packet arrived.</summary>
public readonly record struct NetEvent(NetEventKind Kind, int Peer, byte[] Data = null, string Address = "");

/// <summary>
/// A reliable, ordered packet pipe (ENet in the game, a fake network in tests). The host sees each client as
/// a peer id; a client sees only the host, as <see cref="Server"/>.
/// </summary>
public interface ITransport
{
	/// <summary>The peer id of the host, seen from a client.</summary>
	const int Server = 1;

	void Send(int peer, byte[] data);

	void Disconnect(int peer);

	/// <summary>Everything that arrived since the last call, in order.</summary>
	IEnumerable<NetEvent> Poll();
}
