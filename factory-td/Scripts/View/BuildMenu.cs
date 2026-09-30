using System;
using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Bottom bar: category tabs (Logistik / Produktion / Försvar) and one toggle button per building
/// in the open category.
/// </summary>
public partial class BuildMenu : CanvasLayer
{
	private readonly Dictionary<BuildingType, Button> _buttons = new();
	private readonly List<(Button Tab, HBoxContainer Row)> _categories = new();
	private Label _status;
	private PlayerState _player;

	/// <summary>The chosen building, or null when the selection is cleared.</summary>
	public event Action<BuildingType?> SelectionChanged;

	public override void _Ready()
	{
		var panel = new PanelContainer { TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
		panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Begin;
		panel.OffsetTop = panel.OffsetBottom = -8;
		AddChild(panel);

		var column = new VBoxContainer();
		panel.AddChild(column);

		// Always present (empty when there's nothing to say) so the bar doesn't jump in size.
		_status = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(1f, 0.55f, 0.45f),
		};
		column.AddChild(_status);

		var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.AddChild(tabs);

		foreach (var (name, types) in BuildingVisuals.MenuCategories)
		{
			var tab = new Button { Text = name, ToggleMode = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 0) };
			tabs.AddChild(tab);

			var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
			row.AddThemeConstantOverride("separation", 6);
			column.AddChild(row);

			int index = _categories.Count;
			tab.Pressed += () => ShowCategory(index);
			_categories.Add((tab, row));

			foreach (var type in types)
			{
				var button = new Button
				{
					ToggleMode = true,
					FocusMode = Control.FocusModeEnum.None, // keep Space/Enter away from the buttons
					Icon = BuildingVisuals.GetTexture(type),
					ExpandIcon = true,
					Text = Texts.DisplayName(type) + "\n" + Texts.CostText(type, "\n"),
					TooltipText = Texts.Description(type),
					IconAlignment = HorizontalAlignment.Center,
					VerticalIconAlignment = VerticalAlignment.Top,
					CustomMinimumSize = new Vector2(112, 128),
				};
				button.Toggled += pressed => OnToggled(type, pressed);
				row.AddChild(button);
				_buttons[type] = button;
			}
		}

		column.AddChild(new Label
		{
			Text = "Vänsterklick: bygg   Dra: bandet följer musen   R: rotera   Högerklick: avbryt / riv   Klick på sorterare/maskin: byt sort",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(1, 1, 1, 0.7f),
		});

		ShowCategory(0);
	}

	public void Bind(PlayerState player) => _player = player;

	// Dim the buttons the player can't afford right now (they stay clickable, the status explains why).
	public override void _Process(double delta)
	{
		if (_player == null)
			return;
		foreach (var (type, button) in _buttons)
			button.Modulate = _player.CanAfford(BuildingRules.Cost(type)) ? Colors.White : new Color(1, 1, 1, 0.45f);
	}

	public void ShowStatus(string text) => _status.Text = text;

	public void ClearSelection()
	{
		foreach (var button in _buttons.Values)
			button.SetPressedNoSignal(false);
	}

	private void ShowCategory(int index)
	{
		for (int i = 0; i < _categories.Count; i++)
		{
			_categories[i].Tab.SetPressedNoSignal(i == index);
			_categories[i].Row.Visible = i == index;
		}
	}

	private void OnToggled(BuildingType type, bool pressed)
	{
		if (!pressed)
		{
			SelectionChanged?.Invoke(null);
			return;
		}

		foreach (var (other, button) in _buttons)
			if (other != type)
				button.SetPressedNoSignal(false);
		SelectionChanged?.Invoke(type);
	}
}
