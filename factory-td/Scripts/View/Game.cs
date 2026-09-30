using FactoryTD.Sim;
using Godot;

namespace FactoryTD.View;

/// <summary>Root of the main scene: creates the World, runs its fixed tick and wires the views to it.</summary>
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

	private BotPlayer _bot;
	private PowerView _power;
	private FogView _fog;
	private MinimapView _minimap;

	// TODO: comes from the network session later.
	private const int LocalPlayer = 0;

	private double _accumulator;

	public World World { get; private set; }

	// Children are ready before their parent, so everything below exists here.
	public override void _Ready()
	{
		World = World.CreateMatch();
		if (EnableBot)
			_bot = new BotPlayer(World.EnemyOf(LocalPlayer));

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

		Menu.SelectionChanged += Builder.Select;
		Builder.SelectionCleared += Menu.ClearSelection;
		Builder.StatusChanged += Menu.ShowStatus;
	}

	public override void _Process(double delta)
	{
		_accumulator += delta;
		int ticks = 0;
		while (_accumulator >= TickSeconds && ticks < MaxTicksPerFrame)
		{
			_bot?.Tick(World);
			World.Tick();
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
		_fog.Alpha = alpha;
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
