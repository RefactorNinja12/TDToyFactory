using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Top-left panel, drawn from UI.ResourceBarModel: small item icons with their count and a thin fill bar,
/// the food and power gauges as short numbers in their mood colour, both toyboxes' health as bars and the
/// worker counts. The explanations are tooltips. Also shows the result when the match ends.
/// </summary>
public partial class ResourceBar : CanvasLayer
{
	private const float Icon = 22f;
	private const double RefreshSeconds = 0.25;

	private World _world;
	private int _localPlayer;
	private double _refresh;

	private HBoxContainer _stockRow;
	private readonly Dictionary<ItemType, (Control Box, Label Count, ProgressBar Fill)> _stocks = new();
	private (Control Box, Label Value) _food, _power;
	private ProgressBar _ownCore, _enemyCore;
	private readonly Dictionary<UnitType, (Control Box, Label Count)> _workers = new();
	private Label _result;

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
	}

	public override void _Ready()
	{
		var panel = new PanelContainer { Position = new Vector2(8, 8), MouseFilter = Control.MouseFilterEnum.Pass };
		AddChild(panel);
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 4);
		panel.AddChild(column);

		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 10);
		column.AddChild(top);
		_stockRow = new HBoxContainer();
		_stockRow.AddThemeConstantOverride("separation", 10);
		top.AddChild(_stockRow);
		top.AddChild(new VSeparator());
		_food = Chip(top, BuildingVisuals.GetItemTexture(ItemType.Food));
		_power = Chip(top, GD.Load<Texture2D>("res://Assets/Sprites/Power/pylon.png"));

		var bottom = new HBoxContainer();
		bottom.AddThemeConstantOverride("separation", 10);
		column.AddChild(bottom);
		_ownCore = CoreBar(bottom, "Din leksakslåda", UiTheme.Good);
		_enemyCore = CoreBar(bottom, "Fiendens leksakslåda", UiTheme.Bad);
		bottom.AddChild(new VSeparator());
		foreach (var type in new[] { UnitType.Builder, UnitType.Farmer, UnitType.Scout })
		{
			var chip = Chip(bottom, BuildingVisuals.GetUnitTexture(type));
			chip.Box.TooltipText = Texts.UnitName(type);
			_workers[type] = chip;
		}

		_result = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
		_result.AddThemeFontSizeOverride("font_size", 64);
		_result.AddThemeConstantOverride("outline_size", 12);
		_result.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		_result.GrowHorizontal = Control.GrowDirection.Both;
		_result.GrowVertical = Control.GrowDirection.Both;
		AddChild(_result);
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;
		_refresh -= delta;
		if (_refresh > 0)
			return;
		_refresh = RefreshSeconds;

		var model = ResourceBarModel.For(_world, _localPlayer);
		var shown = new HashSet<ItemType>();
		foreach (var stock in model.Stocks)
		{
			shown.Add(stock.Item);
			if (!_stocks.TryGetValue(stock.Item, out var entry))
				_stocks[stock.Item] = entry = StockChip(stock.Item);
			entry.Box.Visible = true;
			entry.Count.Text = stock.Count.ToString();
			entry.Fill.MaxValue = stock.Capacity;
			entry.Fill.Value = stock.Count;
			entry.Fill.Modulate = stock.Full ? UiTheme.Bad : Colors.White;
			entry.Box.TooltipText = $"{Texts.ItemName(stock.Item)}: {stock.Count}/{stock.Capacity}{(stock.Full ? " (fullt)" : "")}";
		}
		foreach (var (item, entry) in _stocks)
			if (!shown.Contains(item))
				entry.Box.Visible = false;

		SetGauge(_food, model.Food);
		SetGauge(_power, model.Power);
		SetBar(_ownCore, model.OwnCore, "Din leksakslåda");
		SetBar(_enemyCore, model.EnemyCore, "Fiendens leksakslåda");
		foreach (var worker in model.Workers)
		{
			var (box, count) = _workers[worker.Type];
			count.Text = worker.Count.ToString();
			box.TooltipText = $"{Texts.UnitName(worker.Type)}: {worker.Count}/{worker.Max}";
		}

		if (_world.Winner >= 0)
		{
			_result.Visible = true;
			_result.Text = _world.Winner == _localPlayer ? "Du vann!" : "Du förlorade!";
		}
	}

	private (Control Box, Label Count, ProgressBar Fill) StockChip(ItemType item)
	{
		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
		box.AddThemeConstantOverride("separation", 1);
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 3);
		line.AddChild(UiTheme.Icon(BuildingVisuals.GetItemTexture(item), Icon));
		var count = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(30, 0) };
		line.AddChild(count);
		box.AddChild(line);
		var fill = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 3), MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddChild(fill);
		_stockRow.AddChild(box);
		return (box, count, fill);
	}

	private static (Control Box, Label Value) Chip(Container parent, Texture2D icon)
	{
		var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(UiTheme.Icon(icon, Icon));
		var value = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddChild(value);
		parent.AddChild(box);
		return (box, value);
	}

	private static ProgressBar CoreBar(Container parent, string name, Color colour)
	{
		var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = name };
		box.AddThemeConstantOverride("separation", 3);
		box.AddChild(UiTheme.Icon(BuildingVisuals.GetTexture(BuildingType.Core), Icon));
		var bar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(70, 8),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = colour,
		};
		box.AddChild(bar);
		parent.AddChild(box);
		return bar;
	}

	private static void SetGauge((Control Box, Label Value) chip, Gauge gauge)
	{
		chip.Value.Text = gauge.Short;
		chip.Value.Modulate = gauge.Mood switch { Mood.Bad => UiTheme.Bad, Mood.Warn => UiTheme.Warn, _ => UiTheme.Good };
		chip.Box.TooltipText = gauge.Tooltip;
	}

	private static void SetBar(ProgressBar bar, Bar value, string name)
	{
		bar.MaxValue = value.Max;
		bar.Value = value.Value;
		((Control)bar.GetParent()).TooltipText = $"{name}: {value.Value}/{value.Max}";
	}
}
