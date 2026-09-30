using System;
using System.Collections.Generic;
using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Placing and removing buildings with the mouse.
/// Left click / drag: place the selected building (dragged conveyors face the drag direction). R: rotate.
/// Right click: cancel the selection, or remove a building if nothing is selected. Esc: cancel.
/// Left click with nothing selected: cycle the setting of a sorter / assembler.
/// </summary>
public partial class BuildController : Node2D
{
	private static readonly Color ValidColor = new(0.6f, 1f, 0.6f, 0.7f);
	private static readonly Color InvalidColor = new(1f, 0.4f, 0.4f, 0.7f);

	private static readonly Color HighlightColor = new(0.4f, 1f, 0.4f, 0.8f);
	private static readonly Color ForbiddenZoneColor = new(0.35f, 0f, 0f, 0.35f);

	private World _world;
	private BuildingType? _selected;

	/// <summary>Whether a building is picked for placing (the info panel stays out of the way then).</summary>
	public bool HasSelection => _selected != null;
	private Direction _facing = Direction.East;
	private Sprite2D _ghost;
	private string _status = "";

	private bool _dragging;
	private Vector2I _dragCell;
	private bool _dragPlacedLast; // whether the tile at _dragCell was built by this drag

	// Tiles where the selected building can go, outlined while it needs a specific deposit.
	private readonly List<Vector2I> _highlights = new();

	/// <summary>Raised when the selection is cancelled from here (right click / Esc), so the menu can update.</summary>
	public event Action SelectionCleared;

	/// <summary>Why the hovered tile can't be built on, or "" when it can (or nothing is selected).</summary>
	public event Action<string> StatusChanged;

	// TODO: owner should come from the local player once there is networking.
	public int LocalPlayer { get; set; }

	public void Init(World world)
	{
		_world = world;
		_world.BuildingPlaced += _ => RefreshHighlights();
		_world.BuildingRemoved += _ => RefreshHighlights();
	}

	public override void _Ready()
	{
		_ghost = new Sprite2D { Visible = false };
		AddChild(_ghost);
	}

	public void Select(BuildingType? type)
	{
		_selected = type;
		if (type is { } t)
			_ghost.Texture = BuildingVisuals.GetTexture(t);
		RefreshHighlights();
		UpdateGhost();
	}

	public override void _Draw()
	{
		const int ts = BuildingVisuals.TileSize;

		// While building, shade the rooms this player can't build in.
		if (_selected != null && _world != null)
		{
			foreach (var zone in new[] { Zone.LeftRoom, Zone.RightRoom })
			{
				if (zone == MapLayout.HomeZone(LocalPlayer))
					continue;
				var (x0, y0, x1, y1) = _world.Map.ZoneBounds(zone);
				DrawRect(new Rect2(x0 * ts, y0 * ts, (x1 - x0 + 1) * ts, (y1 - y0 + 1) * ts), ForbiddenZoneColor);
			}
		}

		foreach (var cell in _highlights)
			DrawRect(new Rect2(cell.X * ts + 3, cell.Y * ts + 3, ts - 6, ts - 6), HighlightColor, filled: false, width: 4);
	}

	private void RefreshHighlights()
	{
		_highlights.Clear();
		if (_selected is { } type && _world != null && BuildingRules.RequiredResource(type) != ResourceType.None)
		{
			var map = _world.Map;
			for (int y = 0; y < map.Height; y++)
				for (int x = 0; x < map.Width; x++)
					if (_world.CheckLocation(type, x, y, LocalPlayer) == PlaceError.None)
						_highlights.Add(new Vector2I(x, y));
		}
		QueueRedraw();
	}

	public override void _Process(double delta) => UpdateGhost();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_world == null)
			return;

		switch (@event)
		{
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } when _selected != null:
				StartDrag();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
				var clicked = MouseCell();
				if (!_world.TryConfigure(clicked.X, clicked.Y, LocalPlayer))
					return;
				break;
			case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }:
				_dragging = false;
				return;
			case InputEventMouseMotion when _dragging && _selected != null:
				ContinueDrag();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
				if (_selected != null)
					Cancel();
				else
					RemoveAtMouse();
				break;
			case InputEventKey { Pressed: true, Echo: false, Keycode: Key.R }:
				_facing = _facing.RotatedClockwise();
				break;
			case InputEventKey { Pressed: true, Keycode: Key.Escape } when _selected != null:
				Cancel();
				break;
			default:
				return;
		}
		GetViewport().SetInputAsHandled();
	}

	private void StartDrag()
	{
		_dragging = true;
		_dragCell = MouseCell();
		_dragPlacedLast = _world.TryPlace(_selected.Value, _dragCell.X, _dragCell.Y, _facing, LocalPlayer);
	}

	/// <summary>
	/// Walks tile by tile from the last drag tile to the mouse, so fast mouse moves leave no gaps.
	/// Conveyors turn to face the way you drag, and the previous one turns with them, so dragging
	/// around a corner makes a working turn.
	/// </summary>
	private void ContinueDrag()
	{
		var target = MouseCell();
		while (_dragCell != target)
		{
			var step = _dragCell.X != target.X
				? new Vector2I(Math.Sign(target.X - _dragCell.X), 0)
				: new Vector2I(0, Math.Sign(target.Y - _dragCell.Y));
			var previous = _dragCell;
			_dragCell += step;

			if (_selected == BuildingType.Conveyor)
			{
				_facing = DirectionOf(step);
				if (_dragPlacedLast)
				{
					// Re-place the conveyor we just built so it points at the new one.
					_world.TryRemove(previous.X, previous.Y, LocalPlayer);
					_world.TryPlace(BuildingType.Conveyor, previous.X, previous.Y, _facing, LocalPlayer);
				}
			}
			_dragPlacedLast = _world.TryPlace(_selected.Value, _dragCell.X, _dragCell.Y, _facing, LocalPlayer);
		}
	}

	private static Direction DirectionOf(Vector2I step) => step switch
	{
		{ X: > 0 } => Direction.East,
		{ X: < 0 } => Direction.West,
		{ Y: > 0 } => Direction.South,
		_ => Direction.North,
	};

	private void RemoveAtMouse()
	{
		var cell = MouseCell();
		_world.TryRemove(cell.X, cell.Y, LocalPlayer);
	}

	private void Cancel()
	{
		Select(null);
		SelectionCleared?.Invoke();
	}

	private void UpdateGhost()
	{
		if (_selected is not { } type || _world == null)
		{
			_ghost.Visible = false;
			SetStatus("");
			return;
		}

		var cell = MouseCell();
		var error = _world.CheckPlace(type, cell.X, cell.Y, LocalPlayer);
		_ghost.Visible = true;
		var (w, h) = BuildingRules.Size(type);
		_ghost.Position = new Vector2((cell.X + w / 2f) * BuildingVisuals.TileSize, (cell.Y + h / 2f) * BuildingVisuals.TileSize);
		_ghost.Rotation = BuildingVisuals.Rotation(_facing);
		_ghost.Modulate = error == PlaceError.None ? ValidColor : InvalidColor;
		SetStatus(BuildingVisuals.ErrorText(type, error));
	}

	private void SetStatus(string status)
	{
		if (status == _status)
			return;
		_status = status;
		StatusChanged?.Invoke(status);
	}

	private Vector2I MouseCell() => BuildingVisuals.WorldToCell(GetGlobalMousePosition());
}
