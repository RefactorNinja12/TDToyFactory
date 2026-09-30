using System.Collections.Generic;
using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Hover a building to see what it does, what it needs fed to it (with how much it has right now),
/// and how it's doing. Shown next to the mouse, hidden while placing a building.
/// </summary>
public partial class InfoPanel : CanvasLayer
{
	private const float IconSize = 26f;
	private const double RefreshSeconds = 0.15;

	private static readonly Color Dim = new(1, 1, 1, 0.65f);
	private static readonly Color Good = new(0.55f, 1f, 0.55f);
	private static readonly Color Missing = new(1f, 0.6f, 0.5f);

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
		return _world.GetBuilding(cell.X, cell.Y);
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

		string owner = building.Owner == _localPlayer ? "" : "  (fiende)";
		Title(BuildingVisuals.DisplayName(building.Type) + owner);
		Text($"Hälsa {building.Health}/{building.MaxHealth}", building.Health < building.MaxHealth ? Missing : Dim);

		if (!building.IsBuilt)
		{
			int builders = _world.BuildersOn(building);
			int percent = building.BuildTime == 0 ? 100 : building.BuildWork * 100 / building.BuildTime;
			Text($"Byggs: {percent}%", Good);
			Progress(building.BuildWork, building.BuildTime);
			Text(builders == 0
				? "Ingen byggare på väg. Fler byggare får du från en verktygslåda."
				: $"{builders} byggare på väg eller jobbar här.", builders == 0 ? Missing : Dim);
			Text("Fungerar inte förrän den är klar.", Dim);
			return;
		}

		switch (building)
		{
			case UnitFactory factory:
				Text($"Gör: {BuildingVisuals.UnitName(factory.Produces)}");
				Text("Behöver per trupp:", Dim);
				Needs(factory.Recipe, factory.Crafter);
				Progress(factory.Crafter.Progress, UnitStats.BuildTicks(factory.Produces));
				Text("Mata in materialet med band från vilken sida som helst.", Dim);
				break;

			case Assembler assembler:
				Text($"Gör: {BuildingVisuals.ItemName(assembler.Recipe.Output)}  (klicka för att byta)");
				Text("Behöver per styck:", Dim);
				Needs(assembler.Recipe.Inputs, assembler.Crafter);
				Progress(assembler.Crafter.Progress, assembler.Recipe.Ticks);
				Text($"Klara, väntar på att komma ut: {assembler.Finished}", Dim);
				Text("Tar emot från alla håll, lämnar ut åt pilens håll.", Dim);
				break;

			case Tower tower:
				Item(tower.Stats.Ammo, $"Ammo: {BuildingVisuals.ItemName(tower.Stats.Ammo).ToLowerInvariant()}  (1 = {tower.Stats.ShotsPerItem} skott)");
				Text($"Skott kvar: {tower.Shots}/{tower.Stats.MaxShots}", tower.Shots > 0 ? Good : Missing);
				Text($"Räckvidd {tower.Stats.RangeTiles} rutor, skada {tower.Stats.Damage}", Dim);
				break;

			case Extractor extractor:
				Item(extractor.Output, $"Gör: {BuildingVisuals.ItemCount(extractor.Output, 1)} var {Extractor.ProductionTicks / World.TicksPerSecond}:a sekund");
				Text($"I lager: {extractor.Stored}/{Extractor.MaxStored}", extractor.Stored >= Extractor.MaxStored ? Missing : Dim);
				Text("Lämnar till band på alla sidor (utom band som pekar in i den).", Dim);
				break;

			case Sorter sorter:
				Item(sorter.Filter, $"{BuildingVisuals.ItemName(sorter.Filter)} rakt fram, allt annat åt sidorna");
				Text("Klicka för att byta sort.", Dim);
				break;

			case CropField field:
				if (field.IsRipe)
					Item(ItemType.Crop, $"Mogen! {BuildingVisuals.ItemCount(ItemType.Crop, CropField.Yield)} väntar på en bonde", Good);
				else
				{
					int left = (CropField.GrowTicks - field.Growth + World.TicksPerSecond - 1) / World.TicksPerSecond;
					Item(ItemType.Crop, $"Växer: {field.Growth * 100 / CropField.GrowTicks}%, mogen om {left} s");
					Progress(field.Growth, CropField.GrowTicks);
				}
				Text("Bönder skördar och bär morötterna till ett kök eller lager.", Dim);
				break;

			case Core:
				Text("Tar emot allt från banden. Det betalar dina byggen.", Dim);
				Storage(building.Owner);
				break;

			case Warehouse:
				Text($"Lagrar allt från banden, +{PlayerState.WarehouseCapacity} plats per sort.", Dim);
				Storage(building.Owner);
				break;

			default:
				var description = BuildingVisuals.Description(building.Type);
				if (description.Length > 0)
					Text(description, Dim);
				break;
		}
	}

	/// <summary>The shared stock of toybox + warehouses: what's stored and how much room there is.</summary>
	private void Storage(int owner)
	{
		var player = _world.Players[owner];
		Text($"Förråd (leksakslåda + {player.Warehouses} lager):", Dim);
		foreach (var item in Items.All)
		{
			int count = player.GetCount(item), capacity = player.Capacity(item);
			if (count == 0 && item is not (ItemType.Brick or ItemType.Plastic or ItemType.Battery))
				continue;
			Item(item, $"{count}/{capacity}", count >= capacity ? Missing : Colors.White);
		}
	}

	private void Needs(ItemStack[] recipe, Crafter crafter)
	{
		foreach (var input in recipe)
		{
			int have = crafter.Stock(input.Type);
			Item(input.Type, $"{BuildingVisuals.ItemCount(input.Type, input.Amount)}   (har {have})",
				have >= input.Amount ? Good : Missing);
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

	private void Title(string text)
	{
		var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", 18);
		_rows.AddChild(label);
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
		row.AddChild(new TextureRect
		{
			Texture = BuildingVisuals.GetItemTexture(item),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		row.AddChild(new Label { Text = text, Modulate = color ?? Colors.White, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore });
		_rows.AddChild(row);
	}
}
