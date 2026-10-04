using FactoryTD.Net;
using FactoryTD.Sim;
using FactoryTD.View.Net;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Root of the match scene: takes the World (local, or from the online session in MatchSetup), runs its fixed
/// tick (online: only when the lockstep turn is there) and wires the views to it.
/// </summary>
public partial class Game : Node2D
{
	private const double TickSeconds = 1.0 / World.TicksPerSecond;
	private const int MaxTicksPerFrame = 5; // don't spiral if a frame takes very long

	[Export] public MapView Map;
	[Export] public BuildingView Buildings;
	[Export] public ItemView Items;
	[Export] public UnitView Units;
	[Export] public BuildController Builder;
	[Export] public BuildMenu Menu;
	[Export] public ResourceBar Resources;
	[Export] public CombatView Combat;
	[Export] public InfoPanel Info;

	/// <summary>F2 sends an enemy wave. For testing.</summary>
	[Export] public bool DebugKeys = true;

	/// <summary>Let a simple bot play the other side.</summary>
	[Export] public bool EnableBot = true;

	private BotPlayer _bot, _localBot;
	private CameraController _camera;
	private float _countdown;
	private CraneView _cranes;
	private MatchSession _session;
	private ICommandSink _commands;
	private MatchOverlay _overlay;
	private bool _desyncLogged;
	private double _quitIn = -1;
	private PowerView _power;
	private FogView _fog;
	private MinimapView _minimap;

	private int LocalPlayer;

	private double _accumulator;

	public World World { get; private set; }

	/// <summary>
	/// Set before the node enters the tree: a bot-against-bot match with full sight and none of the player's
	/// UI or input, running a little fast with a drifting camera (the start menu's background, UI/MenuPreview).
	/// </summary>
	public bool Preview { get; set; }

	private float _previewTime;

	// Children are ready before their parent, so everything below exists here.
	public override void _Ready()
	{
		_session = Preview ? null : MatchSetup.Session;
		if (Preview)
		{
			World = World.CreateMatch(theme: MatchSetup.Theme);
			World.FullVision = true;
			_commands = new DirectCommands(World);
			_bot = new BotPlayer(1);
			_localBot = new BotPlayer(0);
			DebugKeys = false;
		}
		else if (_session != null)
		{
			World = _session.World;
			LocalPlayer = _session.LocalPlayer;
			_commands = _session.Commands;
			DebugKeys = false; // a debug wave would exist on one machine only
		}
		else
		{
			World = World.CreateMatch(theme: MatchSetup.Theme);
			_commands = new DirectCommands(World);
			if (EnableBot)
				_bot = new BotPlayer(World.EnemyOf(LocalPlayer), MatchSetup.BotArmyDelayTicks);
		}
		if (MatchSetup.LocalBot && !Preview)
			_localBot = new BotPlayer(LocalPlayer);
		Builder.Commands = _commands;

		Map.Render(World.Map);
		Buildings.Bind(World);
		Items.Bind(World);
		Units.Bind(World);
		Combat.Bind(World);
		Builder.Init(World);
		Builder.LocalPlayer = LocalPlayer;
		Resources.Bind(World, LocalPlayer);
		Menu.Bind(World.Players[LocalPlayer]);
		Info.Bind(World, Builder, LocalPlayer);

		Buildings.LocalPlayer = Items.LocalPlayer = Units.LocalPlayer = Combat.LocalPlayer = LocalPlayer;
		var treadmills = new TreadmillView { Name = "Treadmills" };
		AddChild(treadmills);
		treadmills.Bind(World, LocalPlayer);
		// Shadows from the light sources: over the floor, under buildings and units.
		var shadows = new ShadowView { Name = "Shadows" };
		AddChild(shadows);
		MoveChild(shadows, Buildings.GetIndex());
		shadows.Bind(World, LocalPlayer);
		_cranes = new CraneView { Name = "Cranes" };
		AddChild(_cranes);
		_cranes.Bind(World, LocalPlayer);
		var animator = new BuildingAnimator { Name = "BuildingParts" };
		AddChild(animator);
		animator.Bind(World, LocalPlayer);
		var toys = new ObstacleView { Name = "Obstacles" };
		AddChild(toys);
		toys.Bind(World);
		_fog = new FogView { Name = "Fog" };
		AddChild(_fog);
		_fog.Bind(World, LocalPlayer);
		Builder.ZIndex = 4; // the placement ghost stays visible over the fog
		_minimap = new MinimapView { Name = "Minimap" };
		AddChild(_minimap);
		_minimap.Bind(World, LocalPlayer, GetNode<Camera2D>("Camera"));

		_power = new PowerView { Name = "Power" };
		AddChild(_power);
		_power.Bind(World, Builder, LocalPlayer);

		// The camera starts over the player's own toybox (and flies in from the room during the countdown).
		_camera = GetNode<CameraController>("Camera");
		// A few tiles in from the toybox towards the middle, so the view stays inside the room by the outer wall.
		var core = BuildingVisuals.FootprintCenter(World.GetCore(LocalPlayer));
		float inwards = core.X < World.Map.Width * BuildingVisuals.TileSize / 2f ? 1 : -1;
		_camera.StartAt(core + new Vector2(inwards * 4 * BuildingVisuals.TileSize, 0), UI.Countdown.CloseZoom);
		if (MatchSetup.QuitAfterSteps > 0)
			SkipCountdown(); // smoke tests play straight away
		_overlay = new MatchOverlay { Name = "Overlay" };
		AddChild(_overlay);
		_overlay.Bind(World, _session, LeaveMatch);

		Menu.SelectionChanged += Builder.Select;
		Builder.SelectionCleared += Menu.ClearSelection;
		Builder.StatusChanged += Menu.ShowStatus;

		// One look for every UI layer (and the window's tooltips).
		var theme = UiTheme.Create();
		GetWindow().Theme = theme;
		UiTheme.ApplyTo(this, theme);
		if (Preview)
			StartPreview();
	}

	/// <summary>The menu background: no player UI or input, the bases already standing, the camera drifting.</summary>
	private void StartPreview()
	{
		foreach (var layer in new Node[] { Menu, Resources, Info, _minimap, _overlay, Builder })
		{
			layer.ProcessMode = ProcessModeEnum.Disabled;
			if (layer is CanvasLayer canvas)
				canvas.Visible = false;
			else if (layer is CanvasItem item)
				item.Visible = false;
		}
		_camera.SetProcess(false);
		_camera.SetProcessUnhandledInput(false);
		SkipCountdown();
		for (int t = 0; t < UI.MenuPreview.HeadStart * World.TicksPerSecond; t++)
		{
			_bot.Tick(World);
			_localBot.Tick(World, _commands);
			World.Tick();
		}
	}

	public override void _Process(double delta)
	{
		_session?.Update((long)Time.GetTicksMsec());
		LogDesync();
		if (QuitWhenDone(delta) || _overlay.PausesGame)
			return;
		if (!UI.Countdown.Started(_countdown))
		{
			// 3, 2, 1: the match waits (look round, plan, place) while the camera flies in to the toybox.
			_countdown += (float)delta;
			_camera.Zoom = Vector2.One * UI.Countdown.Zoom(_countdown);
			_overlay.ShowCountdown(_countdown);
			return;
		}
		_overlay.ShowCountdown(_countdown += (float)delta);

		if (Preview)
		{
			_previewTime += (float)delta;
			const float ts = BuildingVisuals.TileSize;
			var (x, y, zoom) = UI.MenuPreview.CameraAt(_previewTime, World.Map.Width * ts, World.Map.Height * ts);
			_camera.Position = new Vector2(x, y);
			_camera.Zoom = new Vector2(zoom, zoom);
			delta *= UI.MenuPreview.Speed;
		}
		_accumulator += delta;
		int ticks = 0;
		while (_accumulator >= TickSeconds && ticks < MaxTicksPerFrame && !ReachedQuitStep())
		{
			_bot?.Tick(World);
			_localBot?.Tick(World, _commands);
			if (_session == null)
				World.Tick();
			else if (!_session.TryStep())
			{
				_accumulator = System.Math.Min(_accumulator, TickSeconds); // waiting: don't rush afterwards
				break;
			}
			_accumulator -= TickSeconds;
			ticks++;
		}
		if (ticks == MaxTicksPerFrame)
			_accumulator = 0;

		float alpha = (float)(_accumulator / TickSeconds);
		Items.Alpha = alpha;
		Units.Alpha = alpha;
		Combat.Alpha = alpha;
		_power.Alpha = alpha;
		_cranes.Alpha = alpha;
		_fog.Alpha = alpha;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
			MatchSetup.EndOnline(); // tells the other side and closes the router port
	}

	/// <summary>Starts the match at once, without the countdown (smoke tests, VisualProbe).</summary>
	public void SkipCountdown()
	{
		_countdown = UI.Countdown.Seconds + UI.Countdown.GoSeconds;
		_camera?.StartAt(_camera.Position, UI.Countdown.CloseZoom);
	}

	private void LeaveMatch()
	{
		MatchSetup.EndOnline();
		GetTree().ChangeSceneToFile("res://Scenes/Menu.tscn");
	}

	/// <summary>On a desync, both checksums and the last commands go to user://desync-TICK.txt (once).</summary>
	private void LogDesync()
	{
		if (_desyncLogged || _session is not { EndReason: EndReason.Desync })
			return;
		_desyncLogged = true;
		using var file = FileAccess.Open($"user://desync-{_session.DesyncStep}.txt", FileAccess.ModeFlags.Write);
		file?.StoreString(_session.DesyncReport());
		GD.PrintErr($"desync at tick {_session.DesyncStep}, log in {OS.GetUserDataDir()}");
	}

	private bool ReachedQuitStep() => MatchSetup.QuitAfterSteps > 0 && World.TickCount >= MatchSetup.QuitAfterSteps;

	/// <summary>
	/// Smoke test (--steps N): at step N write "tick checksum" (or why the match ended) and quit a few seconds
	/// later, so the other side also gets its last turns before this one hangs up.
	/// </summary>
	private bool QuitWhenDone(double delta)
	{
		if (MatchSetup.QuitAfterSteps <= 0)
			return false;
		if (_quitIn < 0 && (ReachedQuitStep() || _session is { State: SessionState.Ended }))
		{
			string line = ReachedQuitStep() ? $"{World.TickCount} {World.Checksum():X16}" : $"ended {_session.EndReason} {_session.DesyncStep}";
			using (var file = FileAccess.Open(MatchSetup.ChecksumFile, FileAccess.ModeFlags.Write))
				file?.StoreString(line + "\n");
			_quitIn = 3;
		}
		if (_quitIn < 0)
			return false;
		_quitIn -= delta;
		if (_quitIn <= 0)
		{
			MatchSetup.EndOnline();
			GetTree().Quit();
		}
		return true;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (DebugKeys && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F2 })
			SpawnDebugWave(World.EnemyOf(LocalPlayer), count: 5);
	}

	// Not deterministic input handling: debug only. Real spawns come from factories.
	private void SpawnDebugWave(int owner, int count)
	{
		var core = World.GetCore(owner);
		int x = core.X < World.Map.Width / 2 ? core.X + core.Width + 2 : core.X - 3;
		for (int i = 0; i < count; i++)
			World.SpawnUnit(UnitType.PlasticSoldier, owner, x, core.Y - count / 2 + i);
	}
}
