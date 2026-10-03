using System;
using FactoryTD.Net;
using FactoryTD.Sim;
using FactoryTD.UI;
using FactoryTD.View.Net;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// The start menu: play locally against the bot, host an online match (port + password) or join one
/// (address + password). Rules and texts are in UI/MenuModel; this only lays out controls and drives the
/// session until the match starts, then loads the match scene. Command-line args (LaunchArgs) skip it.
/// </summary>
public partial class MainMenu : Control
{
	private const string MatchScene = "res://Scenes/Main.tscn";
	private const string SettingsFile = "user://settings.cfg";

	private MenuSettings _settings = new();
	private VBoxContainer _page;
	private Label _status;
	private Button _startMatch;
	private MatchSession _session;
	private ENetTransport _transport;
	private bool _autoStart;
	private int _seed;

	public override void _Ready()
	{
		_settings = LoadSettings();
		var theme = UiTheme.Create();
		GetWindow().Theme = theme;
		Theme = theme;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddPreview();
		// A dark veil over the living match behind, so the menu stays easy to read.
		AddChild(new ColorRect { Color = new Color(0.04f, 0.05f, 0.1f, 0.55f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) };
		center.AddChild(panel);
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 14);
		panel.AddChild(column);
		var title = new Label { Text = "Leksakskrig", HorizontalAlignment = HorizontalAlignment.Center, Modulate = UiTheme.Accent };
		title.AddThemeFontSizeOverride("font_size", 48);
		title.AddThemeConstantOverride("outline_size", 10);
		column.AddChild(title);
		_page = new VBoxContainer();
		_page.AddThemeConstantOverride("separation", 10);
		column.AddChild(_page);

		if (!StartFromCommandLine())
			ShowMain();
	}

	public override void _Process(double delta)
	{
		if (_session == null)
			return;
		_session.Update((long)Time.GetTicksMsec());
		if (_status != null)
		{
			_status.Text = MenuModel.Status(_session, _session is HostSession);
			_status.Modulate = _session.State == SessionState.Ended ? UiTheme.Bad : UiTheme.Text;
		}
		if (_startMatch != null)
			_startMatch.Disabled = _session is not HostSession { CanStart: true };
		if (_autoStart && _session is HostSession { CanStart: true } host)
		{
			_autoStart = false;
			host.Start(_seed);
		}
		if (_session.State == SessionState.Playing)
		{
			MatchSetup.Session = _session;
			MatchSetup.Transport = _transport;
			GetTree().ChangeSceneToFile(MatchScene);
			_session = null;
		}
		else if (_session.State == SessionState.Ended)
		{
			_transport?.Close();
			_session = null;
			_transport = null;
			if (_startMatch != null)
				_startMatch.Disabled = true;
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
			MatchSetup.EndOnline();
	}

	/// <summary>
	/// The background: a bot-against-bot match in its own little viewport (its camera doesn't move the menu),
	/// rendered at half resolution and scaled up, taking no input. Not when running headless (smoke tests).
	/// </summary>
	private void AddPreview()
	{
		if (DisplayServer.GetName() == "headless" || LaunchArgs.Parse(OS.GetCmdlineUserArgs()).Online)
			return;
		var container = new SubViewportContainer { Stretch = true, StretchShrink = 2, MouseFilter = MouseFilterEnum.Ignore };
		container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		container.SetProcessInput(false);
		var viewport = new SubViewport { GuiDisableInput = true, HandleInputLocally = true };
		container.AddChild(viewport);
		var game = GD.Load<PackedScene>(MatchScene).Instantiate<Game>();
		game.Preview = true;
		viewport.AddChild(game);
		AddChild(container);
	}

	// ---- pages ----

	private void ShowMain()
	{
		Clear();
		AddButton("Spela lokalt", ShowLocal);
		AddButton("Hosta match", ShowHost);
		AddButton("Anslut till match", ShowJoin);
		AddButton("Avsluta", () => GetTree().Quit());
	}

	private void ShowLocal()
	{
		Clear();
		AddLabel("Mot datorn. Svårighet:");
		var difficulty = new OptionButton();
		foreach (var (name, _) in MenuModel.Difficulties)
			difficulty.AddItem(name);
		difficulty.Selected = MenuModel.Difficulties.Length - 1;
		_page.AddChild(difficulty);
		AddButton("Starta", () =>
		{
			MatchSetup.EndOnline();
			MatchSetup.BotArmyDelayTicks = MenuModel.Difficulties[difficulty.Selected].ArmyDelayTicks;
			GetTree().ChangeSceneToFile(MatchScene);
		});
		AddButton("Tillbaka", ShowMain);
	}

	private void ShowHost()
	{
		Clear();
		var name = AddField("Ditt namn", _settings.Name);
		var port = AddField("Port", _settings.Port.ToString());
		var password = AddPassword();
		_status = AddLabel("Välj ett lösenord och berätta det för din kompis.");
		var addresses = AddLabel("");
		var internet = AddLabel("");
		internet.Modulate = UiTheme.TextDim;
		_startMatch = null;
		Button host = null;
		host = AddButton("Starta värd", () =>
		{
			if (!MenuModel.TryParsePort(port.Text, out int p, out string error) || (error = MenuModel.PasswordProblem(password.Text)) != null)
			{
				Fail(error);
				return;
			}
			_settings = _settings with { Name = name.Text, Port = p };
			SaveSettings();
			if (!Host(p, password.Text, name.Text))
				return;
			internet.Text = MenuModel.InternetHint(p);
			host.Visible = false;
			var lan = MenuModel.LanAddresses(IP.GetLocalAddresses());
			addresses.Text = lan.Count == 0 ? $"Port {p}." : $"Din kompis ansluter till:\n{string.Join("\n", lan.ConvertAll(a => MenuModel.AddressLine(a, p)))}";
			_startMatch.Visible = true;
		});
		_startMatch = AddButton("Starta matchen", () => (_session as HostSession)?.Start(_seed));
		_startMatch.Visible = false;
		_startMatch.Disabled = true;
		AddButton("Tillbaka", Back);
	}

	private void ShowJoin()
	{
		Clear();
		var name = AddField("Ditt namn", _settings.Name);
		var address = AddField("Värdens adress (ip:port)", _settings.Address);
		var password = AddPassword();
		_status = AddLabel("");
		_startMatch = null;
		AddButton("Anslut", () =>
		{
			if (!MenuModel.TryParseAddress(address.Text, out string ip, out int port, out string error)
				|| (error = MenuModel.PasswordProblem(password.Text)) != null)
			{
				Fail(error);
				return;
			}
			_settings = _settings with { Name = name.Text, Address = address.Text.Trim() };
			SaveSettings();
			Join(ip, port, password.Text, name.Text);
		});
		AddButton("Tillbaka", Back);
	}

	// ---- connecting ----

	private bool Host(int port, string password, string name)
	{
		_transport = ENetTransport.Host(port, out string error);
		if (_transport == null)
		{
			Fail(error);
			return false;
		}
		_seed = (int)(GD.Randi() & 0x7fffffff);
		_session = new HostSession(_transport, password, name, NewWorld);
		return true;
	}

	private void Join(string ip, int port, string password, string name)
	{
		_session?.Leave();
		_transport?.Close();
		_transport = ENetTransport.Join(ip, port, out string error);
		if (_transport == null)
		{
			Fail(error);
			return;
		}
		_session = new ClientSession(_transport, password, name, NewWorld);
	}

	private static World NewWorld(int seed) => World.CreateMatch(obstacles: true, seed);

	private void Back()
	{
		_session?.Leave();
		_transport?.Close();
		_session = null;
		_transport = null;
		ShowMain();
	}

	/// <summary>--host/--join from the command line: straight into hosting or joining (smoke tests).</summary>
	private bool StartFromCommandLine()
	{
		var args = LaunchArgs.Parse(OS.GetCmdlineUserArgs());
		MatchSetup.LocalBot = args.Bot;
		MatchSetup.QuitAfterSteps = args.Steps;
		MatchSetup.ChecksumFile = args.Out;
		if (!args.Online)
			return false;
		Clear();
		_status = AddLabel("");
		if (args.Host)
		{
			if (Host(args.Port, args.Password, args.Name))
				_seed = args.Seed;
			_autoStart = true;
		}
		else if (MenuModel.TryParseAddress(args.Join, out string ip, out int port, out string error))
			Join(ip, port, args.Password, args.Name);
		else
			Fail(error);
		return true;
	}

	// ---- controls ----

	private void Clear()
	{
		foreach (var child in _page.GetChildren())
			child.QueueFree();
		_status = null;
	}

	private Label AddLabel(string text)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center };
		_page.AddChild(label);
		return label;
	}

	private Button AddButton(string text, Action pressed)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
		button.Pressed += pressed;
		_page.AddChild(button);
		return button;
	}

	private LineEdit AddField(string label, string value)
	{
		_page.AddChild(new Label { Text = label, Modulate = UiTheme.TextDim });
		var field = new LineEdit { Text = value };
		_page.AddChild(field);
		return field;
	}

	private LineEdit AddPassword()
	{
		var field = AddField("Lösenord", "");
		field.Secret = true;
		var show = new CheckBox { Text = "Visa lösenordet" };
		show.Toggled += on => field.Secret = !on;
		_page.AddChild(show);
		return field;
	}

	private void Fail(string error)
	{
		_status.Visible = true;
		_status.Text = error;
		_status.Modulate = UiTheme.Bad;
	}

	private static MenuSettings LoadSettings()
	{
		using var file = FileAccess.Open(SettingsFile, FileAccess.ModeFlags.Read);
		return MenuSettings.FromText(file?.GetAsText());
	}

	private void SaveSettings()
	{
		using var file = FileAccess.Open(SettingsFile, FileAccess.ModeFlags.Write);
		file?.StoreString(_settings.ToText());
	}
}
