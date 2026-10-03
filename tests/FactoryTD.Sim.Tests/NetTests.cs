using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactoryTD.Net;
using FactoryTD.Sim;
using FactoryTD.Sim.Tests.Support;
using Xunit;

namespace FactoryTD.Sim.Tests;

/// <summary>A machine in a test match: its session, maybe a bot, and a 20/s step clock like Game.cs.</summary>
internal sealed class NetPlayer
{
	private const int StepMs = 1000 / World.TicksPerSecond;
	private int _clock;

	public NetPlayer(MatchSession session, bool bot = false)
	{
		Session = session;
		WithBot = bot;
	}

	public MatchSession Session { get; }
	public bool WithBot { get; }
	public BotPlayer Bot { get; private set; }

	/// <summary>The checksum after every step this machine ran.</summary>
	public List<ulong> Sums { get; } = new();

	public void Frame(long now, int ms)
	{
		Session.Update(now);
		_clock = Math.Min(_clock + ms, StepMs * 5);
		while (_clock >= StepMs && Session.State == SessionState.Playing)
		{
			if (WithBot)
				(Bot ??= new BotPlayer(Session.LocalPlayer)).Tick(Session.World, Session.Commands);
			if (!Session.TryStep())
				break;
			Sums.Add(Session.World.Checksum());
			_clock -= StepMs;
		}
	}
}

public class NetTests
{
	private const string Password = "hemligt";

	private static World NewWorld(int seed) => World.CreateMatch(obstacles: true, seed);

	private static HostSession Host(FakeNetwork net, string password = Password) =>
		new(net.Host, password, "Värd", NewWorld);

	private static ClientSession Client(FakeNetwork net, string address = "10.0.0.2", string password = Password,
		string build = null) => new(net.Connect(address), password, "Kompis", NewWorld, build);

	/// <summary>Runs every machine in 10 ms frames until <paramref name="done"/> or the time runs out.</summary>
	private static void Run(FakeNetwork net, Func<bool> done, int maxMs, params NetPlayer[] players)
	{
		for (int t = 0; t < maxMs && !done(); t += 10)
		{
			net.Advance(10);
			foreach (var p in players)
				p.Frame(net.Now, 10);
		}
	}

	private static (NetPlayer Host, NetPlayer Client) Joined(FakeNetwork net, bool bots = false)
	{
		var host = new NetPlayer(Host(net), bots);
		var client = new NetPlayer(Client(net), bots);
		Run(net, () => client.Session.State != SessionState.Connecting && client.Session.State != SessionState.Joining, 5000, host, client);
		Assert.Equal(SessionState.Lobby, client.Session.State);
		return (host, client);
	}

	/// <summary>Two machines in a started match that have both run <paramref name="steps"/> steps.</summary>
	private static (FakeNetwork, NetPlayer, NetPlayer) Playing(int latency, int seed, int steps)
	{
		var net = new FakeNetwork(latencyMs: latency);
		var (host, client) = Joined(net);
		((HostSession)host.Session).Start(seed);
		Run(net, () => client.Sums.Count >= steps, 10_000, host, client);
		return (net, host, client);
	}

	[Fact]
	public void RightPassword_GetsIn_NamesAreExchanged()
	{
		var net = new FakeNetwork(latencyMs: 80);
		var (host, client) = Joined(net);
		var hostSession = (HostSession)host.Session;
		Assert.True(hostSession.CanStart);
		Assert.Equal("Kompis", hostSession.OpponentName);
		Assert.Equal("Värd", client.Session.OpponentName);
		Assert.Equal(1, client.Session.LocalPlayer);
	}

	[Fact]
	public void WrongPassword_IsRejected_AndGetsNoGameData()
	{
		var net = new FakeNetwork(latencyMs: 40);
		var host = new NetPlayer(Host(net));
		var client = new NetPlayer(Client(net, password: "fel"));
		Run(net, () => client.Session.State == SessionState.Ended, 3000, host, client);
		Assert.Equal(EndReason.Rejected, client.Session.EndReason);
		Assert.Equal(RejectReason.WrongPassword, client.Session.Rejection);
		Assert.False(((HostSession)host.Session).CanStart);
		var toClient = net.Sent.Where(s => s.From == net.Host).Select(s => (MessageType)s.Data[0]).ToList();
		Assert.Equal(new[] { MessageType.Challenge, MessageType.Reject }, toClient);
		// The host hangs up shortly after saying why.
		net.Advance(1000);
		host.Frame(net.Now, 0);
		Assert.Empty(net.Host.Peers);
	}

	[Fact]
	public void ThreeWrongPasswords_BlockTheAddress_ForAWhile()
	{
		var net = new FakeNetwork(latencyMs: 20);
		var host = new NetPlayer(Host(net));
		RejectReason? Attempt(string password, string address = "10.0.0.9")
		{
			var c = new NetPlayer(Client(net, address, password));
			Run(net, () => c.Session.State is SessionState.Ended or SessionState.Lobby, 3000, host, c);
			if (c.Session.State == SessionState.Lobby)
				c.Session.Leave();
			Run(net, () => false, 700, host, c);
			return c.Session.Rejection;
		}
		Assert.Equal(RejectReason.WrongPassword, Attempt("a"));
		Assert.Equal(RejectReason.WrongPassword, Attempt("b"));
		Assert.Equal(RejectReason.WrongPassword, Attempt("c"));
		Assert.Equal(RejectReason.Blocked, Attempt(Password));               // even the right one, for now
		Assert.Null(Attempt(Password, "10.0.0.3"));                          // other addresses are fine
		Run(net, () => false, HostSession.BlockMs, host);
		Assert.Null(Attempt(Password));                                      // the block wears off
	}

	[Fact]
	public void RecordedProof_DoesNotWorkAgain()
	{
		var net = new FakeNetwork(latencyMs: 20);
		var (_, client) = Joined(net);
		var hello = net.Sent.First(s => s.From != net.Host && s.Data[0] == (byte)MessageType.Hello).Data;
		var proof = net.Sent.First(s => s.From != net.Host && s.Data[0] == (byte)MessageType.Proof).Data;
		Assert.Equal(SessionState.Lobby, client.Session.State);

		// An eavesdropper replays both packets to a fresh host with the same password.
		var net2 = new FakeNetwork(latencyMs: 20);
		var host2 = new NetPlayer(Host(net2));
		var thief = net2.Connect("10.0.0.66");
		Run(net2, () => false, 200, host2);
		thief.Send(ITransport.Server, hello);
		Run(net2, () => false, 200, host2);
		thief.Send(ITransport.Server, proof);
		Run(net2, () => false, 200, host2);
		var replies = thief.Poll().Where(e => e.Kind == NetEventKind.Data).Select(e => (MessageType)e.Data[0]).ToList();
		Assert.Equal(new[] { MessageType.Challenge, MessageType.Reject }, replies);
		Assert.False(((HostSession)host2.Session).CanStart);
	}

	[Fact]
	public void ThePassword_NeverAppearsInAnyPacket()
	{
		var (net, host, client) = Playing(latency: 20, seed: 3, steps: 40);
		var secret = Encoding.UTF8.GetBytes(Password);
		Assert.DoesNotContain(net.Sent, s => s.Data.AsSpan().IndexOf(secret) >= 0);
	}

	[Fact]
	public void OtherBuild_IsRejected_AndASecondPlayerFindsItFull()
	{
		var net = new FakeNetwork(latencyMs: 20);
		var host = new NetPlayer(Host(net));
		var old = new NetPlayer(Client(net, build: "an older build"));
		Run(net, () => old.Session.State == SessionState.Ended, 2000, host, old);
		Assert.Equal(RejectReason.WrongVersion, old.Session.Rejection);

		var first = new NetPlayer(Client(net, "10.0.0.2"));
		Run(net, () => first.Session.State == SessionState.Lobby, 2000, host, first);
		var second = new NetPlayer(Client(net, "10.0.0.3"));
		Run(net, () => second.Session.State == SessionState.Ended, 2000, host, first, second);
		Assert.Equal(RejectReason.Full, second.Session.Rejection);
		Assert.Equal(SessionState.Lobby, first.Session.State);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(150, 40)]
	[InlineData(400, 120)]
	public void TwoBots_PlayTheSameMatch_OnBothMachines(int latency, int jitter)
	{
		var net = new FakeNetwork(latency, jitter, seed: latency);
		var (host, client) = Joined(net, bots: true);
		Run(net, () => false, 3000, host, client); // a few pings measure the round trip
		((HostSession)host.Session).Start(seed: 7);
		const int Steps = 20 * 60;
		Run(net, () => host.Sums.Count >= Steps && client.Sums.Count >= Steps, Steps * 50 * 3, host, client);
		Assert.Equal(host.Session.InputDelay, client.Session.InputDelay);
		Assert.True(host.Session.InputDelay * 50 >= latency, $"delay {host.Session.InputDelay} steps for {latency} ms");

		Assert.True(client.Sums.Count >= Steps, $"client reached {client.Sums.Count} steps");
		Assert.Equal(host.Sums.Take(Steps), client.Sums.Take(Steps));
		var world = host.Session.World;
		Assert.Contains(world.Buildings, b => b.Owner == 0 && b.Type != BuildingType.Core);
		Assert.Contains(world.Buildings, b => b.Owner == 1 && b.Type != BuildingType.Core); // the client's commands arrived
		Assert.Equal(SessionState.Playing, client.Session.State);
	}

	[Fact]
	public void LatePackets_StallTheGame_ThenItCarriesOnInStep()
	{
		var net = new FakeNetwork(latencyMs: 60);
		var (host, client) = Joined(net, bots: true);
		((HostSession)host.Session).Start(seed: 1);
		Run(net, () => client.Sums.Count >= 100, 10_000, host, client);
		net.Frozen = true;
		Run(net, () => false, 2000, host, client);
		int stalledAt = host.Sums.Count;
		Assert.True(host.Session.IsWaiting && client.Session.IsWaiting);
		Run(net, () => false, 500, host, client);
		Assert.Equal(stalledAt, host.Sums.Count); // nobody runs ahead
		net.Frozen = false;
		Run(net, () => client.Sums.Count >= stalledAt + 100, 10_000, host, client);
		Assert.False(client.Session.IsWaiting);
		int both = Math.Min(host.Sums.Count, client.Sums.Count);
		Assert.Equal(host.Sums.Take(both), client.Sums.Take(both));
	}

	[Fact]
	public void ADesync_StopsBothSides_AtTheCheckAfterIt()
	{
		var (net, host, client) = Playing(latency: 40, seed: 2, steps: 50);
		long tamperedAt = client.Session.World.TickCount;
		client.Session.World.SpawnUnit(UnitType.PlasticSoldier, 1, 100, 30); // only on one machine
		Run(net, () => host.Session.State == SessionState.Ended && client.Session.State == SessionState.Ended, 5000, host, client);
		Assert.Equal(EndReason.Desync, host.Session.EndReason);
		Assert.Equal(EndReason.Desync, client.Session.EndReason);
		Assert.InRange(host.Session.DesyncStep, tamperedAt, tamperedAt + MatchSession.HashEvery);
		Assert.Equal(host.Session.DesyncStep, client.Session.DesyncStep);
		Assert.Contains($"Desync at tick {host.Session.DesyncStep}", host.Session.DesyncReport());
	}

	[Fact]
	public void Leaving_TellsTheOther_AndSilenceEndsTheMatch()
	{
		var (net, host, client) = Playing(latency: 40, seed: 2, steps: 20);
		client.Session.Leave();
		Run(net, () => host.Session.State == SessionState.Ended, 2000, host, client);
		Assert.Equal(EndReason.Left, client.Session.EndReason);
		Assert.Equal(EndReason.OpponentLeft, host.Session.EndReason);

		var (net2, host2, client2) = Playing(latency: 40, seed: 2, steps: 20);
		net2.Dropping = true;
		Run(net2, () => false, MatchSession.TimeoutMs + 500, host2, client2);
		Assert.Equal(EndReason.ConnectionLost, host2.Session.EndReason);
		Assert.Equal(EndReason.ConnectionLost, client2.Session.EndReason);
	}

	[Fact]
	public void NoHost_CouldNotConnect()
	{
		var net = new FakeNetwork();
		var lonely = new NetPlayer(new ClientSession(new FakeEndpoint(net, "x"), Password, "Ensam", NewWorld));
		Run(net, () => lonely.Session.State == SessionState.Ended, MatchSession.TimeoutMs + 500, lonely);
		Assert.Equal(EndReason.CouldNotConnect, lonely.Session.EndReason);
	}

	[Theory]
	[InlineData(0, 3)]
	[InlineData(100, 4)]
	[InlineData(400, 10)]
	[InlineData(5000, 12)]
	public void InputDelay_CoversTheRoundTrip(int rtt, int delay) => Assert.Equal(delay, HostSession.DelayFor(rtt));

	[Fact]
	public void BrokenPackets_AreIgnored()
	{
		var (net, host, client) = Playing(latency: 20, seed: 2, steps: 10);
		var raw = net.Host.Peers.Values.Single().Endpoint;
		foreach (var junk in new[] { Array.Empty<byte>(), new byte[] { (byte)MessageType.Input, 1 }, new byte[] { 200, 1, 2 },
			new byte[] { (byte)MessageType.Hash }, new byte[] { (byte)MessageType.Input, 50, 0, 0, 0, 3, 9, 9 } })
			raw.Send(ITransport.Server, junk);
		Run(net, () => client.Sums.Count >= 60, 5000, host, client);
		Assert.Equal(SessionState.Playing, host.Session.State);
		Assert.Equal(host.Sums.Take(60), client.Sums.Take(60));
	}
}
