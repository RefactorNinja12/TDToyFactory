using System.Collections.Generic;
using FactoryTD.Sim;

namespace FactoryTD.UI;

/// <summary>One stock in the resource bar: an icon with count / room (Full when there is no room left).</summary>
public readonly record struct StockEntry(ItemType Item, int Count, int Capacity)
{
	public bool Full => Count >= Capacity;
}

/// <summary>A compact gauge: a short number for the bar, a mood colour, and the long text for its tooltip.</summary>
public readonly record struct Gauge(string Short, Mood Mood, string Tooltip);

/// <summary>A health bar (own or enemy toybox): Value of Max.</summary>
public readonly record struct Bar(int Value, int Max);

/// <summary>A worker count with its cap, e.g. builders 12/20.</summary>
public readonly record struct WorkerCount(UnitType Type, int Count, int Max);

/// <summary>
/// What the resource bar shows, as data: icons with numbers instead of sentences. The full explanations
/// (food and power meters) become tooltips.
/// </summary>
public sealed record ResourceBarModel(
	List<StockEntry> Stocks, Gauge Food, Gauge Power, Bar OwnCore, Bar EnemyCore, List<WorkerCount> Workers)
{
	/// <summary>Raw materials and food always show; everything else only once there is some.</summary>
	private static readonly ItemType[] AlwaysShown =
		{ ItemType.Brick, ItemType.Plastic, ItemType.Battery, ItemType.Crop, ItemType.Food };

	public static ResourceBarModel For(World world, int player)
	{
		var state = world.Players[player];
		var stocks = new List<StockEntry>();
		foreach (var item in Items.All)
		{
			int count = state.GetCount(item);
			if (count > 0 || System.Array.IndexOf(AlwaysShown, item) >= 0)
				stocks.Add(new StockEntry(item, count, state.Capacity(item)));
		}

		var (foodText, foodMood) = FoodMeter.Describe(state);
		int net = state.FoodProducedLastMinute - state.FoodUpkeepPerMinute;
		var food = new Gauge($"{(net >= 0 ? "+" : "")}{net}/min", foodMood, foodText);

		var (powerText, powerMood) = PowerMeter.Describe(world, player);
		var power = new Gauge($"{PowerMeter.Bolts(world.Power.EnergyStored(player))}⚡", powerMood, powerText);

		var own = world.GetCore(player);
		var enemy = world.GetCore(world.EnemyOf(player));
		var workers = new List<WorkerCount>
		{
			new(UnitType.Builder, world.CountUnits(player, UnitType.Builder), UnitStats.MaxBuilders),
			new(UnitType.Farmer, world.CountUnits(player, UnitType.Farmer), UnitStats.MaxFarmers),
			new(UnitType.Scout, world.CountUnits(player, UnitType.Scout), UnitStats.MaxScouts),
		};
		return new ResourceBarModel(stocks, food, power,
			new Bar(own?.Health ?? 0, own?.MaxHealth ?? 1), new Bar(enemy?.Health ?? 0, enemy?.MaxHealth ?? 1), workers);
	}
}

/// <summary>One cost line on a build card: item icon + amount, red when the player has too few.</summary>
public readonly record struct CostEntry(ItemType Item, int Amount, bool Affordable);

/// <summary>A building's card in the build menu.</summary>
public sealed record BuildCardModel(BuildingType Type, string Name, char Hotkey, List<CostEntry> Cost, string Tooltip)
{
	public bool Affordable => Cost.TrueForAll(c => c.Affordable);

	public static BuildCardModel For(BuildingType type, int place, PlayerState player)
	{
		var cost = new List<CostEntry>();
		foreach (var stack in BuildingRules.Cost(type))
			cost.Add(new CostEntry(stack.Type, stack.Amount, player.GetCount(stack.Type) >= stack.Amount));
		return new BuildCardModel(type, Texts.DisplayName(type), BuildHotkeys.LetterFor(place), cost, Texts.Description(type));
	}
}

/// <summary>A short message shown above the build menu that fades out (newest replaces the old one).</summary>
public sealed class Toasts
{
	public const double ShowSeconds = 3.0;
	public const double FadeSeconds = 0.6;

	private string _text = "";
	private double _shownAt = double.NegativeInfinity;

	public void Show(string text, double now)
	{
		_text = text ?? "";
		_shownAt = now;
	}

	/// <summary>The message and how visible it is now (1 = fully, 0 = gone).</summary>
	public (string Text, float Alpha) At(double now)
	{
		double age = now - _shownAt;
		if (_text.Length == 0 || age < 0 || age >= ShowSeconds)
			return ("", 0f);
		double fadeStart = ShowSeconds - FadeSeconds;
		float alpha = age < fadeStart ? 1f : (float)((ShowSeconds - age) / FadeSeconds);
		return (_text, alpha);
	}
}
