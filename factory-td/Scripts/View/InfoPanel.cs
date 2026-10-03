using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Hover a building to see what it does, what it needs fed to it (with how much it has right now),
/// and how it's doing. Shown next to the mouse, hidden while placing a building.
/// </summary>
public partial class InfoPanel : CanvasLayer
{
	private const float IconSize = 18f;
	private const double RefreshSeconds = 0.15;

	private static readonly Color Dim = UiTheme.TextDim;
	private static readonly Color Good = UiTheme.Good;
	private static readonly Color Missing = UiTheme.Bad;
	private BuildingType _shownType;

	private World _world;
	private BuildController _builder;
	private int _localPlayer;
	private PanelContainer _panel;
	private VBoxContainer _rows;
	private double _refresh;

	public void Bind(World world, BuildController builder, int localPlayer)
	{
		_world = world;
		_builder = builder;
		_localPlayer = localPlayer;
	}

	public override void _Ready()
	{
		_panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
		AddChild(_panel);
		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 6);
		_panel.AddChild(margin);
		_rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_rows.AddThemeConstantOverride("separation", 2);
		margin.AddChild(_rows);
	}

	public override void _Process(double delta)
	{
		var building = Hovered();
		if (building == null)
		{
			_panel.Visible = false;
			return;
		}

		// Follow the mouse, kept on screen.
		var mouse = GetViewport().GetMousePosition();
		var screen = GetViewport().GetVisibleRect().Size;
		var size = _panel.Size;
		_panel.Position = new Vector2(
			Mathf.Min(mouse.X + 18, screen.X - size.X - 4),
			Mathf.Min(mouse.Y + 18, screen.Y - size.Y - 4));

		_refresh -= delta;
		if (_panel.Visible && _refresh > 0)
			return;
		_refresh = RefreshSeconds;
		Fill(building);
		_panel.Visible = true;
		_panel.ResetSize(); // shrink to the new content
	}

	private Building Hovered()
	{
		if (_world == null || _builder == null || _builder.HasSelection)
			return null;
		var mouse = GetViewport().GetMousePosition();
		var world = GetViewport().GetCanvasTransform().AffineInverse() * mouse;
		var cell = BuildingVisuals.WorldToCell(world);
		var building = _world.GetBuilding(cell.X, cell.Y);
		return building != null && Knowledge.ShowBuilding(_world, _localPlayer, building) ? building : null;
	}

	// ---------------------------------------------------------------------------------------------

	private void Fill(Building building)
	{
		// Remove right away (not just QueueFree) so the panel can shrink to the new content this frame.
		foreach (var child in _rows.GetChildren())
		{
			_rows.RemoveChild(child);
			child.QueueFree();
		}
		_shownType = building.Type;
		foreach (var row in InfoRows.For(_world, building, _localPlayer))
		{
			var color = row.Tone switch { Tone.Dim => Dim, Tone.Good => Good, Tone.Missing => Missing, _ => Colors.White };
			switch (row.Kind)
			{
				case RowKind.Title: Title(row.Text); break;
				case RowKind.Text: Text(row.Text, color); break;
				case RowKind.Item: Item(row.Item, row.Text, color); break;
				case RowKind.Progress: Progress(row.Done, row.Total); break;
			}
		}
	}

	private void Progress(int done, int total)
	{
		var bar = new ProgressBar
		{
			MaxValue = total,
			Value = done,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(200, 8),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_rows.AddChild(bar);
	}

	/// <summary>The title row: the building's own picture and its name, in the accent colour.</summary>
	private void Title(string text)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(UiTheme.Icon(BuildingVisuals.GetTexture(_shownType), 28));
		var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", UiTheme.Accent);
		row.AddChild(label);
		_rows.AddChild(row);
		_rows.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
	}

	private void Text(string text, Color? color = null)
	{
		var label = new Label
		{
			Text = text,
			Modulate = color ?? Colors.White,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(260, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_rows.AddChild(label);
	}

	private void Item(ItemType item, string text, Color? color = null)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddChild(UiTheme.Icon(BuildingVisuals.GetItemTexture(item), IconSize));
		row.AddChild(new Label { Text = text, Modulate = color ?? Colors.White, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore });
		_rows.AddChild(row);
	}
}
