using System;
using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Bottom bar: category tabs and one card per building in the open category (UI.BuildCardModel: icon,
/// name, cost as small item icons that turn red when you have too little, the hotkey letter as a badge).
/// Keyboard: a number opens a category, then Z X C F G T picks the building (UI.BuildHotkeys).
/// Status messages appear as a fading toast above the bar (UI.Toasts).
/// </summary>
public partial class BuildMenu : CanvasLayer
{
	private const float CardIcon = 40f;
	private const float CostIcon = 14f;
	private const double RefreshSeconds = 0.25;

	private readonly Dictionary<BuildingType, Button> _buttons = new();
	private readonly Dictionary<BuildingType, List<(Label Amount, CostEntry Entry)>> _costLabels = new();
	private readonly List<(Button Tab, HBoxContainer Row)> _categories = new();
	private readonly Toasts _toasts = new();
	private PanelContainer _toastPanel;
	private Label _toast;
	private PlayerState _player;
	private BuildHotkeys _hotkeys;
	private double _refresh;

	/// <summary>The chosen building, or null when the selection is cleared.</summary>
	public event Action<BuildingType?> SelectionChanged;

	public override void _Ready()
	{
		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		column.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		column.GrowHorizontal = Control.GrowDirection.Both;
		column.GrowVertical = Control.GrowDirection.Begin;
		column.OffsetTop = column.OffsetBottom = -8;
		column.AddThemeConstantOverride("separation", 6);
		AddChild(column);

		// Toast above the bar: takes no room when hidden, so the bar never jumps.
		_toastPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_toast = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		_toastPanel.AddChild(_toast);
		column.AddChild(_toastPanel);

		var panel = new PanelContainer { TextureFilter = CanvasItem.TextureFilterEnum.Nearest, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		column.AddChild(panel);
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		panel.AddChild(inner);

		var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		tabs.AddThemeConstantOverride("separation", 4);
		inner.AddChild(tabs);

		var categoryTypes = new List<BuildingType[]>();
		foreach (var (_, types) in BuildingVisuals.MenuCategories)
			categoryTypes.Add(types);
		_hotkeys = new BuildHotkeys(categoryTypes);

		foreach (var (name, types) in BuildingVisuals.MenuCategories)
		{
			int index = _categories.Count;
			var tab = new Button
			{
				Text = $"{index + 1} {name}",
				ToggleMode = true,
				FocusMode = Control.FocusModeEnum.None,
				CustomMinimumSize = new Vector2(0, 30),
				TooltipText = $"{name} (tangent {index + 1})",
			};
			tab.AddThemeFontSizeOverride("font_size", 12);
			tabs.AddChild(tab);
			tab.Pressed += () => ShowCategory(index);

			var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
			row.AddThemeConstantOverride("separation", 6);
			inner.AddChild(row);
			_categories.Add((tab, row));

			for (int place = 0; place < types.Length; place++)
				row.AddChild(Card(types[place], place));
		}

		var hint = new Label
		{
			Text = "1–7 → Z X C F G T  •  R rotera  •  dra: band  •  högerklick: avbryt / riv",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = UiTheme.TextDim,
		};
		hint.AddThemeFontSizeOverride("font_size", 11);
		inner.AddChild(hint);

		ShowCategory(0);
	}

	/// <summary>One building card: hotkey badge, icon, name and cost (refreshed in _Process).</summary>
	private Button Card(BuildingType type, int place)
	{
		var model = BuildCardModel.For(type, place, new PlayerState(0));
		var button = new Button
		{
			ToggleMode = true,
			FocusMode = Control.FocusModeEnum.None, // keep Space/Enter away from the buttons
			CustomMinimumSize = new Vector2(92, 92),
			TooltipText = model.Tooltip,
		};
		button.Toggled += pressed => OnToggled(type, pressed);
		_buttons[type] = button;

		var content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.AddThemeConstantOverride("separation", 2);
		button.AddChild(content);

		content.AddChild(UiTheme.Icon(BuildingVisuals.GetTexture(type), CardIcon));
		var name = new Label { Text = model.Name, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		name.AddThemeFontSizeOverride("font_size", 11);
		content.AddChild(name);

		var cost = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		cost.AddThemeConstantOverride("separation", 2);
		content.AddChild(cost);
		var labels = new List<(Label, CostEntry)>();
		foreach (var entry in model.Cost)
		{
			cost.AddChild(UiTheme.Icon(BuildingVisuals.GetItemTexture(entry.Item), CostIcon));
			var amount = new Label { Text = entry.Amount.ToString(), MouseFilter = Control.MouseFilterEnum.Ignore };
			amount.AddThemeFontSizeOverride("font_size", 11);
			cost.AddChild(amount);
			labels.Add((amount, entry));
		}
		_costLabels[type] = labels;

		// The hotkey letter as a small badge in the corner.
		var badge = new Label
		{
			Text = model.Hotkey.ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(4, 3),
			CustomMinimumSize = new Vector2(16, 16),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		badge.AddThemeFontSizeOverride("font_size", 11);
		badge.AddThemeColorOverride("font_color", UiTheme.Outline);
		badge.AddThemeConstantOverride("outline_size", 0);
		var badgeBox = UiTheme.Box(UiTheme.Accent, UiTheme.Accent.Darkened(0.4f), radius: 4, border: 1, shadow: 0);
		badgeBox.ContentMarginLeft = badgeBox.ContentMarginRight = 2;
		badgeBox.ContentMarginTop = badgeBox.ContentMarginBottom = 0;
		badge.AddThemeStyleboxOverride("normal", badgeBox);
		button.AddChild(badge);
		return button;
	}

	public void Bind(PlayerState player) => _player = player;

	// _Input: the hotkeys see the keys before the build controller and the camera.
	public override void _Input(InputEvent @event)
	{
		if (_hotkeys == null || @event is not InputEventKey key || key.Echo || !key.Pressed)
			return;
		char c = key.Keycode == Key.Escape ? BuildHotkeys.Escape
			: (long)key.Keycode is >= 32 and < 127 ? (char)(long)key.Keycode : '\0';
		if (c == '\0')
			return;
		var result = _hotkeys.Press(c);
		switch (result.Outcome)
		{
			case HotkeyOutcome.None:
				return;
			case HotkeyOutcome.CategoryOpened:
				ShowCategory(result.Category);
				ShowStatus($"{BuildingVisuals.MenuCategories[result.Category].Name}: välj med Z X C F G T (Esc avbryter)");
				break;
			case HotkeyOutcome.Selected:
				ShowCategory(result.Category);
				ShowStatus("");
				_buttons[result.Type].ButtonPressed = true; // same as clicking it
				break;
			case HotkeyOutcome.Cancelled:
				ShowStatus("");
				break;
		}
		GetViewport().SetInputAsHandled();
	}

	// Cost numbers turn red for the items the player has too few of; the toast fades.
	public override void _Process(double delta)
	{
		var (text, alpha) = _toasts.At(Time.GetTicksMsec() / 1000.0);
		_toastPanel.Visible = alpha > 0;
		_toastPanel.Modulate = new Color(1, 1, 1, alpha);
		_toast.Text = text;

		if (_player == null)
			return;
		_refresh -= delta;
		if (_refresh > 0)
			return;
		_refresh = RefreshSeconds;
		foreach (var (type, labels) in _costLabels)
		{
			bool all = true;
			foreach (var (amount, entry) in labels)
			{
				bool enough = _player.GetCount(entry.Item) >= entry.Amount;
				amount.Modulate = enough ? Colors.White : UiTheme.Bad;
				all &= enough;
			}
			_buttons[type].Modulate = all ? Colors.White : new Color(1, 1, 1, 0.7f);
		}
	}

	public void ShowStatus(string text)
	{
		if (string.IsNullOrEmpty(text))
			_toasts.Show("", 0);
		else
			_toasts.Show(text, Time.GetTicksMsec() / 1000.0);
	}

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
