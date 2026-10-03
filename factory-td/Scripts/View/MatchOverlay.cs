using System;
using FactoryTD.Net;
using FactoryTD.Sim;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// Over the match: "waiting for the opponent" while the other side holds up the game, a panel when the
/// match or the connection ends, and the Esc menu (carry on / leave). Leaving goes back to the start menu.
/// </summary>
public partial class MatchOverlay : CanvasLayer
{
	private World _world;
	private MatchSession _session;
	private Action _leave;
	private Label _waiting, _message, _count;
	private PanelContainer _panel;
	private Button _resume;
	private bool _shownEnd;

	public void Bind(World world, MatchSession session, Action leave)
	{
		_world = world;
		_session = session;
		_leave = leave;
	}

	public override void _Ready()
	{
		Layer = 20;
		_waiting = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center, Modulate = UiTheme.Warn };
		_waiting.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
		_waiting.Position += new Vector2(0, 90);
		AddChild(_waiting);

		_count = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Modulate = UiTheme.Accent };
		_count.AddThemeFontSizeOverride("font_size", 96);
		_count.AddThemeConstantOverride("outline_size", 16);
		_count.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_count.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_count);

		var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(center);
		_panel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(420, 0) };
		center.AddChild(_panel);
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 12);
		_panel.AddChild(column);
		_message = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_message.AddThemeFontSizeOverride("font_size", 22);
		column.AddChild(_message);
		_resume = new Button { Text = "Fortsätt" };
		_resume.Pressed += () => _panel.Visible = false;
		column.AddChild(_resume);
		var leave = new Button { Text = "Lämna matchen" };
		leave.Pressed += () => _leave?.Invoke();
		column.AddChild(leave);
	}

	/// <summary>The countdown before the match (UI/Countdown): each number pops up big and settles, then "Kör!".</summary>
	public void ShowCountdown(float elapsed)
	{
		string text = UI.Countdown.Text(elapsed);
		_count.Visible = text != "";
		if (!_count.Visible)
			return;
		_count.Text = text;
		float beat = UI.Countdown.Beat(elapsed);
		_count.PivotOffset = _count.Size / 2;
		_count.Scale = Vector2.One * (1.4f - 0.4f * Mathf.Min(1, beat * 3));
		_count.Modulate = UiTheme.Accent with { A = text == "Kör!" ? 1 - beat : 1 };
	}

	/// <summary>Local play stands still while this panel is open (online can't pause: the other side plays on).</summary>
	public bool PausesGame => _session == null && _panel.Visible && _world.Winner < 0;

	public override void _Process(double delta)
	{
		if (_world == null)
			return;
		_waiting.Visible = _session is { State: SessionState.Playing, IsWaiting: true };
		if (_waiting.Visible)
			_waiting.Text = $"Väntar på {(_session.OpponentName == "" ? "motståndaren" : _session.OpponentName)}…";

		if (_shownEnd)
			return;
		string end = _session is { State: SessionState.Ended } ? MenuModel.EndText(_session)
			: _world.Winner >= 0 ? "Matchen är slut." : null;
		if (end != null)
		{
			_shownEnd = true;
			Show(end, canResume: _session == null || _session.State != SessionState.Ended);
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
			return;
		if (_panel.Visible && _resume.Visible)
			_panel.Visible = false;
		else if (!_panel.Visible)
			Show(_session == null ? "Paus" : "Matchen pågår (online går det inte att pausa).", canResume: true);
		GetViewport().SetInputAsHandled();
	}

	private void Show(string text, bool canResume)
	{
		_message.Text = text;
		_resume.Visible = canResume;
		_panel.Visible = true;
	}
}
