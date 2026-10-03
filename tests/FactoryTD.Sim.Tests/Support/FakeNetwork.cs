using System;
using System.Collections.Generic;
using System.Linq;
using FactoryTD.Net;

namespace FactoryTD.Sim.Tests.Support;

/// <summary>
/// A network in memory for the lockstep tests: seeded latency and jitter, but every connection keeps its
/// order (like ENet's reliable channel). Everything sent is recorded so tests can search it.
/// Time only moves with <see cref="Advance"/>.
/// </summary>
internal sealed class FakeNetwork
{
	private readonly Random _random;
	private readonly List<(long At, long Order, FakeEndpoint To, NetEvent Event)> _inFlight = new();
	private readonly Dictionary<(FakeEndpoint, int), long> _lastArrival = new();
	private long _order;
	private int _nextPeer = 2;

	public FakeNetwork(int latencyMs = 0, int jitterMs = 0, int seed = 1)
	{
		LatencyMs = latencyMs;
		JitterMs = jitterMs;
		_random = new Random(seed);
		Host = new FakeEndpoint(this, "host");
	}

	public int LatencyMs { get; set; }
	public int JitterMs { get; set; }

	/// <summary>While true nothing arrives (packets wait, they are not lost).</summary>
	public bool Frozen { get; set; }

	/// <summary>While true everything sent is lost (a pulled cable).</summary>
	public bool Dropping { get; set; }

	public long Now { get; private set; }
	public FakeEndpoint Host { get; }

	/// <summary>Every packet sent: who sent it, to which peer, the bytes.</summary>
	public List<(FakeEndpoint From, int Peer, byte[] Data)> Sent { get; } = new();

	/// <summary>A new machine connecting to the host from <paramref name="address"/>.</summary>
	public FakeEndpoint Connect(string address)
	{
		var client = new FakeEndpoint(this, address);
		int id = _nextPeer++;
		Host.Peers[id] = (client, ITransport.Server);
		client.Peers[ITransport.Server] = (Host, id);
		Deliver(Host, id, new NetEvent(NetEventKind.Connected, id, Address: address));
		Deliver(client, ITransport.Server, new NetEvent(NetEventKind.Connected, ITransport.Server, Address: "host"));
		return client;
	}

	public void Advance(int ms)
	{
		Now += ms;
		if (Frozen)
			return;
		foreach (var due in _inFlight.Where(p => p.At <= Now).OrderBy(p => p.At).ThenBy(p => p.Order).ToList())
		{
			_inFlight.Remove(due);
			due.To.Inbox.Enqueue(due.Event);
		}
	}

	internal void Send(FakeEndpoint from, int peer, byte[] data)
	{
		Sent.Add((from, peer, data));
		if (Dropping || !from.Peers.TryGetValue(peer, out var to))
			return;
		Deliver(to.Endpoint, to.IdThere, new NetEvent(NetEventKind.Data, to.IdThere, data));
	}

	internal void Disconnect(FakeEndpoint from, int peer)
	{
		if (!from.Peers.Remove(peer, out var to))
			return;
		to.Endpoint.Peers.Remove(to.IdThere);
		Deliver(to.Endpoint, to.IdThere, new NetEvent(NetEventKind.Disconnected, to.IdThere));
		Deliver(from, peer, new NetEvent(NetEventKind.Disconnected, peer));
	}

	private void Deliver(FakeEndpoint to, int fromPeer, NetEvent e)
	{
		long at = Now + LatencyMs / 2 + (JitterMs > 0 ? _random.Next(JitterMs) : 0);
		// Never before the previous packet on the same connection.
		if (_lastArrival.TryGetValue((to, fromPeer), out long last))
			at = Math.Max(at, last);
		_lastArrival[(to, fromPeer)] = at;
		_inFlight.Add((at, _order++, to, e));
	}
}

internal sealed class FakeEndpoint : ITransport
{
	private readonly FakeNetwork _network;

	public FakeEndpoint(FakeNetwork network, string address) => (_network, Address) = (network, address);

	public string Address { get; }

	/// <summary>My peer id → the endpoint there and the id it knows me by.</summary>
	public Dictionary<int, (FakeEndpoint Endpoint, int IdThere)> Peers { get; } = new();

	public Queue<NetEvent> Inbox { get; } = new();

	public void Send(int peer, byte[] data) => _network.Send(this, peer, data);

	public void Disconnect(int peer) => _network.Disconnect(this, peer);

	public IEnumerable<NetEvent> Poll()
	{
		while (Inbox.Count > 0)
			yield return Inbox.Dequeue();
	}
}
