using System.Collections.Generic;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Keeps one Sprite2D per building in the World, in sync via its events.
/// Conveyors fed only from one side are drawn with the curve sprite.
/// </summary>
public partial class BuildingView : Node2D
{
	private readonly Dictionary<Building, Sprite2D> _sprites = new();
	private readonly Dictionary<Building, Sprite2D> _icons = new(); // sorter filter / assembler product
	private World _world;

	/// <summary>Enemy buildings are only drawn while this player can see them (remembered ones: FogView).</summary>
	public int LocalPlayer { get; set; }

	public void Bind(World world)
	{
		_world = world;
		_world.BuildingPlaced += OnPlaced;
		_world.BuildingRemoved += OnRemoved;
		_world.BuildingChanged += RefreshIcon;
		foreach (var building in _world.Buildings)
			OnPlaced(building);
	}

	public override void _ExitTree()
	{
		if (_world == null)
			return;
		_world.BuildingPlaced -= OnPlaced;
		_world.BuildingRemoved -= OnRemoved;
		_world.BuildingChanged -= RefreshIcon;
	}

	private void OnPlaced(Building building)
	{
		var sprite = new Sprite2D
		{
			Texture = BuildingVisuals.GetTexture(building.Type),
			Position = BuildingVisuals.FootprintCenter(building),
			// Splitters and sorters work the same from every side, so they are never rotated.
			Rotation = building is Splitter or Sorter or Junction ? 0 : BuildingVisuals.Rotation(building.Facing),
		};
		AddChild(sprite);
		_sprites[building] = sprite;
		RefreshIcon(building);
		RefreshConveyorsAround(building.X, building.Y);
	}

	private void OnRemoved(Building building)
	{
		if (_sprites.Remove(building, out var sprite))
			sprite.QueueFree();
		if (_icons.Remove(building, out var icon))
			icon.QueueFree();
		RefreshConveyorsAround(building.X, building.Y);
	}

	private static readonly Color UnderConstruction = new(1f, 1f, 1f, 0.45f);

	// Construction sites are see-through until finished; towers turn to face whatever they last shot at.
	public override void _Process(double delta)
	{
		foreach (var (building, sprite) in _sprites)
		{
			sprite.Visible = Knowledge.ShowBuilding(_world, LocalPlayer, building);
			if (_icons.TryGetValue(building, out var icon))
				icon.Visible = sprite.Visible;
			sprite.Modulate = building.IsBuilt ? Colors.White : UnderConstruction;
			if (building is CropField field)
				sprite.Texture = BuildingVisuals.CropFieldTexture(field.IsBuilt ? field.Stage : 0);
			if (building is Tower tower)
				sprite.Rotation = Mathf.Atan2(tower.AimY, tower.AimX);
		}
	}

	/// <summary>Small unrotated item icon on top of buildings that have a setting.</summary>
	private void RefreshIcon(Building building)
	{
		var item = building switch
		{
			Sorter sorter => sorter.Filter,
			Assembler assembler => assembler.Recipe.Output,
			_ => ItemType.None,
		};
		if (item == ItemType.None)
			return;

		if (!_icons.TryGetValue(building, out var icon))
		{
			icon = new Sprite2D { Position = BuildingVisuals.FootprintCenter(building), Scale = new Vector2(0.45f, 0.45f) };
			AddChild(icon);
			_icons[building] = icon;
		}
		icon.Texture = BuildingVisuals.GetItemTexture(item);
	}

	// A building's look can depend on its neighbours, so update the tile and the four around it.
	private void RefreshConveyorsAround(int x, int y)
	{
		RefreshConveyor(x, y);
		RefreshConveyor(x + 1, y);
		RefreshConveyor(x - 1, y);
		RefreshConveyor(x, y + 1);
		RefreshConveyor(x, y - 1);
	}

	private void RefreshConveyor(int x, int y)
	{
		if (_world.GetBuilding(x, y) is not Conveyor conveyor || !_sprites.TryGetValue(conveyor, out var sprite))
			return;

		var look = ConveyorLook.For(_world, conveyor);
		sprite.Texture = look.Curve ? BuildingVisuals.ConveyorCurveTexture : BuildingVisuals.GetTexture(BuildingType.Conveyor);
		sprite.FlipV = look.FlipV;
		sprite.Rotation = look.QuarterTurns * Mathf.Pi / 2f;
	}
}
