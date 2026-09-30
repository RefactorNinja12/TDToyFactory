using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>The power line in the resource bar: energy charged vs used, what is stored, and trouble.</summary>
public static class PowerMeter
{
	/// <summary>Energy is shown in ⚡ = 100 EU, so one battery is 60 ⚡.</summary>
	public const int EnergyPerBolt = 100;

	public static int Bolts(int energy) => energy / EnergyPerBolt;

	public static (string Text, Mood Mood) Describe(World world, int player)
	{
		var state = world.Players[player];
		int charged = Bolts(state.EnergyChargedLastMinute);
		int used = Bolts(state.EnergyUsedLastMinute);
		int stored = Bolts(world.Power.EnergyStored(player));
		int unpowered = 0;
		foreach (var building in world.Buildings)
			if (building.Owner == player && building.NoPower)
				unpowered++;

		string text = $"Ström: +{charged}⚡/min laddas   −{used}⚡/min används   lagrat {stored}⚡";
		if (unpowered > 0)
			return (text + $"   ⚠ {unpowered} {(unpowered == 1 ? "byggnad" : "byggnader")} utan ström", Mood.Bad);
		int net = charged - used;
		if (net < 0 && stored < -net * 2)
		{
			int minutes = stored / -net;
			return (text + (minutes < 1 ? "   ⚠ strömmen tar slut inom en minut" : $"   räcker ~{minutes} min"), Mood.Warn);
		}
		return (text, Mood.Good);
	}
}
