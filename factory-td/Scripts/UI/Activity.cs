using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>
/// Which buildings are working right now, for the animations: a building counts as working while its
/// progress (extractor, or the crafter of an assembler/factory/kitchen/treadmill) moved within the last
/// <see cref="WorkingTicks"/>. Starving, no power, a full output or missing input all freeze the progress,
/// so the building stands still without knowing why. Call <see cref="Observe"/> once per frame or tick.
/// </summary>
public sealed class Activity
{
	public const int WorkingTicks = World.TicksPerSecond / 2;

	private readonly Dictionary<Building, (int Signature, long Changed)> _seen = new();
	private long _now;

	public void Observe(World world)
	{
		_now = world.TickCount;
		foreach (var building in world.Buildings)
		{
			if (Signature(building) is not { } signature)
				continue;
			if (!_seen.TryGetValue(building, out var last))
				_seen[building] = (signature, long.MinValue / 2); // first sight: not working yet
			else if (last.Signature != signature)
				_seen[building] = (signature, _now);
		}
		if (_seen.Count > world.Buildings.Count) // something was removed or destroyed
		{
			var standing = world.Buildings.ToHashSet();
			foreach (var gone in _seen.Keys.Where(b => !standing.Contains(b)).ToList())
				_seen.Remove(gone);
		}
	}

	public bool IsWorking(Building building) =>
		building.IsBuilt && _seen.TryGetValue(building, out var last) && _now - last.Changed <= WorkingTicks;

	/// <summary>A number that changes whenever the building gets on with its work; null = never animates.</summary>
	public static int? Signature(Building building) => building switch
	{
		Extractor e => e.Progress,
		Assembler a => a.Crafter.Progress,
		UnitFactory f => f.Crafter.Progress,
		Kitchen k => k.Crafter.Progress * 7919 + k.CheeseCrafter.Progress,
		Treadmill t => t.Crafter.Progress,
		_ => null,
	};
}
