using System.Collections.Generic;
using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Top-left panel: the local player's stock and both cores' health.
/// Also shows the result when the match ends.
/// </summary>
public partial class ResourceBar : CanvasLayer
{
	private static readonly ItemType[] Shown = { ItemType.Brick, ItemType.Plastic, ItemType.Battery };

	private readonly Dictionary<ItemType, Label> _counts = new();
	private World _world;
	private int _localPlayer;
	private Label _health;
	private Label _result;

	public void Bind(World world, int localPlayer)
	{
		_world = world;
		_localPlayer = localPlayer;
	}

	public override void _Ready()
	{
		var panel = new PanelContainer
		{
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Position = new Vector2(8, 8),
		};
		AddChild(panel);

		var column = new VBoxContainer();
		panel.AddChild(column);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		column.AddChild(row);

		foreach (var type in Shown)
		{
			var entry = new HBoxContainer { TooltipText = BuildingVisuals.ItemName(type) };
			entry.AddChild(new TextureRect
			{
				Texture = BuildingVisuals.GetItemTexture(type),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(40, 40),
			});
			var count = new Label { Text = "0", CustomMinimumSize = new Vector2(40, 0), VerticalAlignment = VerticalAlignment.Center };
			count.AddThemeFontSizeOverride("font_size", 22);
			entry.AddChild(count);
			row.AddChild(entry);
			_counts[type] = count;
		}

		_health = new Label();
		column.AddChild(_health);

		_result = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
		_result.AddThemeFontSizeOverride("font_size", 64);
		_result.AddThemeConstantOverride("outline_size", 12);
		_result.AddThemeColorOverride("font_outline_color", Colors.Black);
		_result.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		_result.GrowHorizontal = Control.GrowDirection.Both;
		_result.GrowVertical = Control.GrowDirection.Both;
		AddChild(_result);
	}

	public override void _Process(double delta)
	{
		if (_world == null)
			return;

		var player = _world.Players[_localPlayer];
		foreach (var (type, label) in _counts)
			label.Text = player.GetCount(type).ToString();

		var own = _world.GetCore(_localPlayer);
		var enemy = _world.GetCore(_world.EnemyOf(_localPlayer));
		int builders = _world.CountUnits(_localPlayer, UnitType.Builder);
		int farmers = _world.CountUnits(_localPlayer, UnitType.Farmer);
		_health.Text = $"Din låda: {own?.Health}/{own?.MaxHealth}    Fiendens låda: {enemy?.Health}/{enemy?.MaxHealth}    Byggare: {builders}/{UnitStats.MaxBuilders}    Bönder: {farmers}/{UnitStats.MaxFarmers}";

		if (_world.Winner >= 0)
		{
			_result.Visible = true;
			_result.Text = _world.Winner == _localPlayer ? "Du vann!" : "Du förlorade!";
		}
	}
}
