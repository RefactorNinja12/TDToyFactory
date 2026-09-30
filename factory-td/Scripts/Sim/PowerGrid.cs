using System.Collections.Generic;

namespace FactoryTD.Sim;

/// <summary>One connected set of pylons, chargers and the toybox. Its chargers' energy is one shared pool.</summary>
public sealed class PowerNetwork
{
	public int Owner { get; }
	/// <summary>Every linked node (pylons, chargers, toybox), in building order.</summary>
	public List<Building> Nodes { get; } = new();
	public List<BatteryCharger> Chargers { get; } = new();

	public PowerNetwork(int owner) => Owner = owner;

	/// <summary>Energy stored in all of this network's chargers.</summary>
	public int Energy
	{
		get
		{
			int sum = 0;
			foreach (var charger in Chargers)
				sum += charger.Energy;
			return sum;
		}
	}
}

/// <summary>
/// Which tiles each player's grid powers, and which nodes are linked by cords. Worked out again from the
/// finished buildings whenever buildings change (World rebuilds it together with the flow fields).
/// Nodes link when their centres are within <see cref="PowerStats.LinkRange"/>; the cords drawn are a
/// shortest-first spanning tree, so a cluster of pylons shows a tidy set of cords.
/// </summary>
public sealed class PowerGrid
{
	private const long S = UnitStats.SubTile;

	private readonly int _width;
	private readonly int _height;
	private readonly PowerNetwork[][] _coverage; // per player: network powering each tile, or null
	private readonly List<PowerNetwork> _networks = new();
	private readonly List<(Building A, Building B)> _cords = new();

	public IReadOnlyList<PowerNetwork> Networks => _networks;
	public IReadOnlyList<(Building A, Building B)> Cords => _cords;

	public PowerGrid(int width, int height, int players)
	{
		_width = width;
		_height = height;
		_coverage = new PowerNetwork[players][];
		for (int p = 0; p < players; p++)
			_coverage[p] = new PowerNetwork[width * height];
	}

	/// <summary>The player's network that powers this tile, or null.</summary>
	public PowerNetwork NetworkAt(int player, int x, int y) =>
		x >= 0 && y >= 0 && x < _width && y < _height ? _coverage[player][y * _width + x] : null;

	public static bool IsNode(Building b) => b is Pylon or BatteryCharger or Core;

	/// <summary>Tiles a node powers around its centre (0 for chargers).</summary>
	public static int Radius(Building b) => b switch
	{
		Pylon => PowerStats.PylonRadius,
		Core => PowerStats.CoreRadius,
		_ => 0,
	};

	internal void Rebuild(IReadOnlyList<Building> buildings)
	{
		_networks.Clear();
		_cords.Clear();
		for (int p = 0; p < _coverage.Length; p++)
		{
			System.Array.Clear(_coverage[p]);
			var nodes = new List<Building>();
			foreach (var b in buildings)
				if (b.Owner == p && b.IsBuilt && IsNode(b))
					nodes.Add(b);
			BuildPlayer(p, nodes);
		}
	}

	private void BuildPlayer(int player, List<Building> nodes)
	{
		// Kruskal: every pair in link range, shortest first (ties by building order, so it is deterministic).
		long range2 = PowerStats.LinkRange * S * PowerStats.LinkRange * S;
		var edges = new List<(long Distance, int I, int J)>();
		for (int i = 0; i < nodes.Count; i++)
			for (int j = i + 1; j < nodes.Count; j++)
			{
				long d = Distance2(nodes[i], nodes[j]);
				if (d <= range2)
					edges.Add((d, i, j));
			}
		edges.Sort();

		var parent = new int[nodes.Count];
		for (int i = 0; i < parent.Length; i++)
			parent[i] = i;
		int Find(int i)
		{
			while (parent[i] != i)
				i = parent[i] = parent[parent[i]];
			return i;
		}
		foreach (var (_, i, j) in edges)
		{
			int a = Find(i), b = Find(j);
			if (a == b)
				continue;
			parent[System.Math.Max(a, b)] = System.Math.Min(a, b);
			_cords.Add((nodes[i], nodes[j]));
		}

		// Networks in order of their first node.
		var byRoot = new Dictionary<int, PowerNetwork>();
		for (int i = 0; i < nodes.Count; i++)
		{
			int root = Find(i);
			if (!byRoot.TryGetValue(root, out var network))
			{
				network = new PowerNetwork(player);
				byRoot[root] = network;
				_networks.Add(network);
			}
			network.Nodes.Add(nodes[i]);
			if (nodes[i] is BatteryCharger charger)
				network.Chargers.Add(charger);
		}

		// Coverage: first network (in order) wins where two of the player's networks overlap.
		var coverage = _coverage[player];
		for (int i = 0; i < nodes.Count; i++)
		{
			int radius = Radius(nodes[i]);
			if (radius == 0)
				continue;
			var network = byRoot[Find(i)];
			long r2 = radius * S * radius * S;
			int cx = nodes[i].CenterX, cy = nodes[i].CenterY;
			int minX = System.Math.Max(0, (int)((cx - radius * S) / S)), maxX = System.Math.Min(_width - 1, (int)((cx + radius * S) / S));
			int minY = System.Math.Max(0, (int)((cy - radius * S) / S)), maxY = System.Math.Min(_height - 1, (int)((cy + radius * S) / S));
			for (int y = minY; y <= maxY; y++)
				for (int x = minX; x <= maxX; x++)
				{
					long dx = x * S + S / 2 - cx, dy = y * S + S / 2 - cy;
					int index = y * _width + x;
					if (dx * dx + dy * dy <= r2 && coverage[index] == null)
						coverage[index] = network;
				}
		}
	}

	private static long Distance2(Building a, Building b)
	{
		long dx = a.CenterX - b.CenterX, dy = a.CenterY - b.CenterY;
		return dx * dx + dy * dy;
	}
}
