using System;
using System.Collections.Generic;
using System.Linq;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Placing and removing buildings with the mouse.
/// Left click / drag: place the selected building. Belts: click the start, then click the end: the planned way
/// (round buildings, crossing our own belts with junctions) follows the mouse until the second click. R: rotate.
/// Right click: cancel the selection, or remove a building if nothing is selected. Space (handled by the build menu's
/// hotkeys) drops the building and closes the category; Esc is the pause menu's.
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

	/// <summary>What is being placed, if anything.</summary>
	public BuildingType? Selected => _selected;

	/// <summary>The tile under the mouse.</summary>
	public Vector2I HoverCell => MouseCell();
	private Direction _facing = Direction.East;
	private Sprite2D _ghost;
	private string _status = "";

	private bool _dragging;
	private Vector2I _dragCell;

	// Belts are drawn click, click: the first click sets the start, the planned way follows the mouse
	// (UI/BeltPlanner: round buildings, crossing our own belts with junctions), the second click builds it.
	private Vector2I? _beltStart;
	private Vector2I _beltEnd = new(-1, -1);
	private List<BeltStep> _beltPlan;
	private static readonly Color PlanColor = new(0.6f, 1f, 0.6f, 0.75f);
	private static readonly Color PlanJunctionColor = new(1f, 0.85f, 0.4f, 0.9f);

	// Online, a placement lands a few ticks after the click: drawn faintly until then.
	private readonly List<(BuildingType Type, Vector2I Cell, Direction Facing, long Until)> _sent = new();
	private static readonly Color SentColor = new(1f, 1f, 1f, 0.45f);

	// Tiles where the selected building can go, outlined while it needs a specific deposit.
	private readonly List<Vector2I> _highlights = new();

	/// <summary>Raised when the selection is cancelled from here (right click), so the menu can update.</summary>
	public event Action SelectionCleared;

	/// <summary>Why the hovered tile can't be built on, or "" when it can (or nothing is selected).</summary>
	public event Action<string> StatusChanged;

	public int LocalPlayer { get; set; }

	/// <summary>Where placing/removing/configuring goes (straight into the world, or into the online match).</summary>
	public ICommandSink Commands { get; set; }

	public void Init(World world)
	{
		_world = world;
		Commands ??= new DirectCommands(world);
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
		ClearBelt();
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

		if (_selected == BuildingType.ClawCrane)
		{
			// The rail it would run out over the field, and the extractors it would serve.
			var cell = MouseCell();
			var rail = BuildingVisuals.GetPartTexture("crane_rail");
			for (int d = 1; d <= CraneStats.Reach; d++)
			{
				var tile = new Vector2((cell.X + _facing.DX() * d + 0.5f) * ts, (cell.Y + _facing.DY() * d + 0.5f) * ts);
				DrawSetTransform(tile, BuildingVisuals.Rotation(_facing));
				DrawTexture(rail, -rail.GetSize() / 2, PlanColor);
			}
			DrawSetTransform(Vector2.Zero, 0);
			foreach (var extractor in ClawCrane.Served(_world, cell.X, cell.Y, _facing, LocalPlayer))
				DrawRect(new Rect2(extractor.X * ts + 3, extractor.Y * ts + 3, ts - 6, ts - 6), HighlightColor, filled: false, width: 4);
		}

		if (_beltPlan != null)
		{
			// Drawn like built belts: curves where it turns (UI/ConveyorLook), junctions where it crosses.
			foreach (var (step, look) in BeltPlanner.Looks(_beltPlan))
			{
				var at = new Vector2((step.X + 0.5f) * ts, (step.Y + 0.5f) * ts);
				var texture = step.Junction ? BuildingVisuals.GetTexture(BuildingType.Junction)
					: look.Curve ? BuildingVisuals.ConveyorCurveTexture : BuildingVisuals.GetTexture(BuildingType.Conveyor);
				var turn = step.Junction ? 0 : look.QuarterTurns * Mathf.Pi / 2f;
				var flip = look.FlipV && !step.Junction ? new Vector2(1, -1) : Vector2.One;
				DrawSetTransformMatrix(new Transform2D(turn, flip, 0, at));
				DrawTexture(texture, -texture.GetSize() / 2, step.Junction ? PlanJunctionColor : PlanColor);
			}
			DrawSetTransform(Vector2.Zero, 0);
		}

		foreach (var (type, cell, facing, _) in _sent)
		{
			var texture = BuildingVisuals.GetTexture(type);
			var (w, h) = BuildingRules.Size(type);
			DrawSetTransform(new Vector2((cell.X + w / 2f) * ts, (cell.Y + h / 2f) * ts), BuildingVisuals.Rotation(facing));
			DrawTexture(texture, -texture.GetSize() / 2, SentColor);
		}
		DrawSetTransform(Vector2.Zero, 0);
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

	public override void _Process(double delta)
	{
		UpdateGhost();
		RefreshBelt();
		if (_selected == BuildingType.ClawCrane)
			QueueRedraw(); // the rail preview follows the mouse
		if (_sent.Count == 0)
			return;
		_sent.RemoveAll(s => _world.TickCount > s.Until || _world.GetBuilding(s.Cell.X, s.Cell.Y) != null);
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_world == null)
			return;

		switch (@event)
		{
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } when _selected == BuildingType.Conveyor:
				if (_beltStart == null)
				{
					_beltStart = MouseCell();
					RefreshBelt(force: true);
				}
				else
					BuildBelt();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } when _selected != null:
				StartDrag();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
				var clicked = MouseCell();
				if (_world.GetBuilding(clicked.X, clicked.Y)?.Owner != LocalPlayer)
					return;
				Commands.Send(PlayerCommand.Configure(LocalPlayer, clicked.X, clicked.Y));
				break;
			case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }:
				_dragging = false;
				return;
			case InputEventMouseMotion when _dragging && _selected != null:
				ContinueDrag();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } when _beltStart != null:
				ClearBelt();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
				if (_selected != null)
					Cancel();
				else
					RemoveAtMouse();
				break;
			case InputEventKey { Pressed: true, Echo: false, Keycode: Key.R }:
				_facing = _facing.RotatedClockwise();
				RefreshBelt(force: true);
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
		Place(_selected.Value, _dragCell, _facing);
	}

	/// <summary>Sends a placement; whether it will work is judged now (it may land a few ticks later online).</summary>
	private bool Place(BuildingType type, Vector2I cell, Direction facing)
	{
		if (BeltReplace.CanReplace(_world, type, cell.X, cell.Y, LocalPlayer))
		{
			// A splitter, sorter or junction straight onto one of our belts: the belt makes way.
			foreach (var command in BeltReplace.Commands(type, cell.X, cell.Y, facing, LocalPlayer))
				Commands.Send(command);
		}
		else if (_world.CanPlace(type, cell.X, cell.Y, LocalPlayer))
			Commands.Send(PlayerCommand.Place(LocalPlayer, type, cell.X, cell.Y, facing));
		else
			return false;
		if (Commands.DelayTicks > 0)
			_sent.Add((type, cell, facing, _world.TickCount + Commands.DelayTicks + World.TicksPerSecond));
		return true;
	}

	/// <summary>
	/// Walks tile by tile from the last drag tile to the mouse, so fast mouse moves leave no gaps
	/// (dragging places a row of the selected building; belts are drawn click, click instead).
	/// </summary>
	private void ContinueDrag()
	{
		var target = MouseCell();
		foreach (var (x, y, _) in DragPath.Walk(_dragCell.X, _dragCell.Y, target.X, target.Y))
		{
			_dragCell = new Vector2I(x, y);
			Place(_selected.Value, _dragCell, _facing);
		}
	}

	private void RemoveAtMouse()
	{
		var cell = MouseCell();
		Commands.Send(PlayerCommand.Remove(LocalPlayer, cell.X, cell.Y));
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
		if (type == BuildingType.Conveyor && _beltStart != null)
		{
			// Drawing a belt: the planned way is drawn instead of one ghost; the status says what it costs.
			_ghost.Visible = false;
			int junctions = _beltPlan?.Count(s => s.Junction) ?? 0;
			SetStatus(Texts.BeltPlanText(_beltStart != null, (_beltPlan?.Count ?? 0) - junctions, junctions, _beltPlan != null));
			return;
		}
		var error = _world.CheckPlace(type, cell.X, cell.Y, LocalPlayer);
		bool replacing = BeltReplace.CanReplace(_world, type, cell.X, cell.Y, LocalPlayer);
		if (replacing)
			error = PlaceError.None; // goes in place of our belt
		_ghost.Visible = true;
		var (w, h) = BuildingRules.Size(type);
		_ghost.Position = new Vector2((cell.X + w / 2f) * BuildingVisuals.TileSize, (cell.Y + h / 2f) * BuildingVisuals.TileSize);
		_ghost.Rotation = BuildingVisuals.Rotation(_facing);
		_ghost.Modulate = error == PlaceError.None ? ValidColor : InvalidColor;
		SetStatus(replacing ? Texts.ReplacesBelt(type)
			: type == BuildingType.Conveyor && error == PlaceError.None ? Texts.BeltPlanText(false, 0, 0, false)
			: Texts.ErrorText(type, error));
	}

	/// <summary>Plans the belt from the start to the mouse again when the mouse moved to another tile.</summary>
	private void RefreshBelt(bool force = false)
	{
		if (_beltStart is not { } start)
			return;
		var end = MouseCell();
		if (!force && end == _beltEnd)
			return;
		_beltEnd = end;
		_beltPlan = BeltPlanner.Plan(_world, LocalPlayer, (start.X, start.Y), (end.X, end.Y), _facing);
		QueueRedraw();
	}

	/// <summary>The second click: build the whole planned way (junctions where it crosses our belts).</summary>
	private void BuildBelt()
	{
		RefreshBelt(force: true);
		if (_beltPlan == null)
			return;
		foreach (var command in BeltPlanner.Commands(_beltPlan, LocalPlayer))
		{
			Commands.Send(command);
			if (Commands.DelayTicks > 0 && command.Kind == CommandKind.Place)
				_sent.Add((command.Type, new Vector2I(command.X, command.Y), command.Facing, _world.TickCount + Commands.DelayTicks + World.TicksPerSecond));
		}
		ClearBelt();
	}

	private void ClearBelt()
	{
		_beltStart = null;
		_beltPlan = null;
		_beltEnd = new Vector2I(-1, -1);
		QueueRedraw();
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
