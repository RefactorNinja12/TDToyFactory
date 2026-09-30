using FactoryTD.Sim;

namespace FactoryTD.UI;

public enum Mood { Good, Warn, Bad }

/// <summary>The food line in the resource bar: production vs upkeep, and how long the stock lasts.</summary>
public static class FoodMeter
{
	public static (string Text, Mood Mood) Describe(PlayerState player)
	{
		int produced = player.FoodProducedLastMinute;
		int eaten = player.FoodUpkeepPerMinute;
		int net = produced - eaten;
		int stock = player.GetCount(ItemType.Food);

		string text = $"Mat: +{produced}/min in   −{eaten}/min äts   netto {(net >= 0 ? "+" : "")}{net}/min";
		if (player.Starving)
			return (text + "   ⚠ SVÄLT! Fabrikerna står still och enheterna tappar hälsa", Mood.Bad);
		if (net < 0)
		{
			int minutes = stock / -net;
			text += minutes < 1 ? "   ⚠ maten tar slut inom en minut" : $"   räcker ~{minutes} min";
			return (text, minutes < 2 ? Mood.Bad : Mood.Warn);
		}
		return (text, Mood.Good);
	}
}
