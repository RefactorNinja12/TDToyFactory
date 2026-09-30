using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Draws the power grid: cords between linked nodes (both players), the local player's powered area
/// (V toggles it; it also shows while placing a power building or something that needs power), a preview
/// of the cords a pylon/charger being placed would get, no-power icons and golem/car charge bars.
/// What to draw comes from UI.PowerOverlay; this only turns it into Godot drawing calls.
/// </summary>
public partial class PowerView : Node2D
{
	private const float T = BuildingVisuals.TileSize;

	private static readonly Color[] TeamCord = { new(0.35f, 0.65f, 1f), new(1f, 0.45f, 0.4f) };
	private static readonly Color CordOutline = new(0.17f, 0.13f, 0.25f);
	private static readonly Color Charged = new(0.35f, 0.85f, 1f, 0.16f);
	private static readonly Color ChargedEdge = new(0.35f, 0.85f, 1f, 0.85f);
	private static readonly Color Flat = new(1f, 0.35f, 0.3f, 0.16f);
	private static readonly Color FlatEdge = new(1f, 0.35f, 0.3f, 0.85f);
	private static readonly Color Preview = new(1f, 1f, 1f, 0.7f);

	private World _world;
	private BuildController _builder;
	private int _localPlayer;
	/// <summary>Show the powered area (V toggles it).</summary>
	public bool ShowOverlay { get; set; }
	private Texture2D _noPower;

	/// <summary>Interpolation between the last two ticks, for unit positions (set by Game).</summary>
	public float Alpha { get; set; }

	public void Bind(World world, BuildController builder, int localPlayer)
	{
		_world = world;
		_builder = builder;
		_localPlayer = localPlayer;
		_noPower = GD.Load<Texture2D>("res://Assets/Sprites/Power/no_power.png");
		ZIndex = 5; // above buildings and units
		TextureFilter = TextureFilterEnum.Nearest;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.V })
		{
			ShowOverlay = !ShowOverlay;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (_world == null)
			return;
		var selected = _builder?.Selected;
		if (ShowOverlay || (selected is { } type && UsesPower(type)))
			DrawCoverage();
		// Enemy cords only where both ends are in sight.
		for (int player = 0; player < _world.Players.Count; player++)
			foreach (var cord in PowerOverlay.Cords(_world, player, include: player == _localPlayer ? null : b => Knowledge.ShowBuilding(_world, _localPlayer, b)))
				DrawCord(cord, TeamCord[player % TeamCord.Length]);
		if (selected is BuildingType.Pylon or BuildingType.BatteryCharger)
			DrawPlacementPreview(selected.Value);
		DrawNoPowerIcons();
		DrawChargeBars();
	}

	/// <summary>Power buildings and everything that needs power: show the grid while placing them.</summary>
	private static bool UsesPower(BuildingType type) => type is BuildingType.Pylon or BuildingType.BatteryCharger
		or BuildingType.Assembler or BuildingType.SoldierFactory or BuildingType.GolemWorkshop or BuildingType.CarFactory
		or BuildingType.FoamTower or BuildingType.Catapult or BuildingType.WaterTower or BuildingType.LaserTower;

	private void DrawCoverage()
	{
		foreach (var circle in PowerOverlay.Circles(_world, _localPlayer))
		{
			var center = new Vector2(circle.X, circle.Y) * T;
			float radius = circle.Radius * T;
			DrawCircle(center, radius, circle.Charged ? Charged : Flat);
			DrawArc(center, radius, 0, Mathf.Tau, 64, circle.Charged ? ChargedEdge : FlatEdge, 2f);
		}
	}

	private void DrawCord((float X, float Y)[] points, Color color)
	{
		var line = new Vector2[points.Length];
		for (int i = 0; i < points.Length; i++)
			line[i] = new Vector2(points[i].X, points[i].Y) * T;
		DrawPolyline(line, CordOutline, 6f, true);
		DrawPolyline(line, color, 3f, true);
		// Little curls along the cord, like a coiled phone cable.
		for (int i = 1; i < line.Length - 1; i++)
			DrawArc(line[i], 4f, 0, Mathf.Tau, 8, color.Lightened(0.3f), 1.5f);
	}

	private void DrawPlacementPreview(BuildingType type)
	{
		var cell = _builder.HoverCell;
		var center = new Vector2(cell.X + 0.5f, cell.Y + 0.5f) * T;
		foreach (var node in PowerOverlay.PreviewLinks(_world, _localPlayer, cell.X, cell.Y))
		{
			var to = new Vector2(node.CenterX, node.CenterY) / UnitStats.SubTile * T;
			DrawDashedLine(center, to, Preview, 2f, 10f);
		}
		if (type == BuildingType.Pylon)
			DrawArc(center, PowerStats.PylonRadius * T, 0, Mathf.Tau, 64, Preview, 2f);
		DrawArc(center, PowerStats.LinkRange * T, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.2f), 1f);
	}

	private void DrawNoPowerIcons()
	{
		// Blink so it catches the eye.
		if (_noPower == null || (Time.GetTicksMsec() / 400) % 2 == 1)
			return;
		foreach (var building in _world.Buildings)
		{
			if (building.Owner != _localPlayer || !building.NoPower)
				continue;
			var corner = new Vector2(building.X + building.Width, building.Y) * T;
			DrawTexture(_noPower, corner + new Vector2(-26, 2));
		}
	}

	private void DrawChargeBars()
	{
		const float width = 24, height = 4;
		foreach (var unit in _world.Units)
		{
			int max = PowerStats.MaxCharge(unit.Type);
			if (max == 0 || (unit.Charge >= max && unit.PowerState == UnitPower.Normal))
				continue;
			var position = new Vector2(
				Mathf.Lerp(unit.PrevX, unit.X, Alpha),
				Mathf.Lerp(unit.PrevY, unit.Y, Alpha)) / UnitStats.SubTile * T;
			var origin = position + new Vector2(-width / 2, -30);
			float share = unit.Charge / (float)max;
			var fill = share > 0.5f ? new Color(0.45f, 1f, 0.45f) : share > PowerStats.ReturnPercent / 100f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.3f);
			DrawRect(new Rect2(origin - Vector2.One, new Vector2(width + 2, height + 2)), CordOutline);
			DrawRect(new Rect2(origin, new Vector2(width * share, height)), fill);
			if (unit.PowerState == UnitPower.Charging)
				DrawString(ThemeDB.FallbackFont, origin + new Vector2(width + 3, height + 1), "⚡", HorizontalAlignment.Left, -1, 12, new Color(1f, 0.95f, 0.4f));
		}
	}
}
