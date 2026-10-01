using Godot;

namespace FactoryTD.View;

/// <summary>
/// The look of every UI element (set on the window, so all CanvasLayers get it): semi-transparent navy
/// panels with a framed border, rounded corners and a soft shadow; buttons that light up warm yellow on
/// hover and when selected; outlined text that reads over the map. Colours from the game's night palette.
/// </summary>
public static class UiTheme
{
	public static readonly Color Panel = new(0.06f, 0.07f, 0.14f, 0.78f);
	public static readonly Color PanelLight = new(0.11f, 0.13f, 0.25f, 0.86f);
	public static readonly Color Frame = new(0.24f, 0.30f, 0.50f, 0.95f);
	public static readonly Color Accent = new(0.95f, 0.71f, 0.29f);
	public static readonly Color Good = new(0.45f, 0.78f, 0.38f);
	public static readonly Color Warn = new(0.95f, 0.71f, 0.29f);
	public static readonly Color Bad = new(0.88f, 0.33f, 0.38f);
	public static readonly Color Text = new(0.90f, 0.92f, 1f);
	public static readonly Color TextDim = new(0.66f, 0.70f, 0.82f);
	public static readonly Color Outline = new(0.02f, 0.02f, 0.06f);

	public const int FontSize = 14;
	public const int OutlineSize = 4;

	public static Theme Create()
	{
		var theme = new Theme { DefaultFontSize = FontSize };

		var panel = Box(Panel, Frame, radius: 8, border: 2, shadow: 6);
		theme.SetStylebox("panel", "PanelContainer", panel);
		theme.SetStylebox("panel", "Panel", panel);
		theme.SetStylebox("panel", "TooltipPanel", Box(new Color(0.05f, 0.06f, 0.12f, 0.95f), Accent, radius: 6, border: 1, shadow: 4));

		theme.SetStylebox("normal", "Button", Box(PanelLight, Frame, radius: 6, border: 2, shadow: 2));
		theme.SetStylebox("hover", "Button", Box(new Color(0.15f, 0.17f, 0.32f, 0.92f), Accent, radius: 6, border: 2, shadow: 2));
		theme.SetStylebox("pressed", "Button", Box(new Color(0.24f, 0.18f, 0.08f, 0.94f), Accent, radius: 6, border: 3, shadow: 2));
		theme.SetStylebox("hover_pressed", "Button", Box(new Color(0.28f, 0.21f, 0.09f, 0.96f), Accent, radius: 6, border: 3, shadow: 2));
		theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
		theme.SetStylebox("disabled", "Button", Box(new Color(0.08f, 0.08f, 0.12f, 0.6f), Frame with { A = 0.4f }, radius: 6, border: 2, shadow: 0));
		theme.SetColor("font_color", "Button", Text);
		theme.SetColor("font_hover_color", "Button", Accent);
		theme.SetColor("font_pressed_color", "Button", Accent);
		theme.SetColor("font_hover_pressed_color", "Button", Accent);
		theme.SetColor("font_outline_color", "Button", Outline);
		theme.SetConstant("outline_size", "Button", OutlineSize);

		theme.SetColor("font_color", "Label", Text);
		theme.SetColor("font_outline_color", "Label", Outline);
		theme.SetConstant("outline_size", "Label", OutlineSize);
		theme.SetColor("font_color", "TooltipLabel", Text);
		theme.SetColor("font_outline_color", "TooltipLabel", Outline);
		theme.SetConstant("outline_size", "TooltipLabel", 3);

		theme.SetStylebox("background", "ProgressBar", Box(new Color(0.03f, 0.03f, 0.07f, 0.85f), Frame with { A = 0.6f }, radius: 3, border: 1, shadow: 0));
		theme.SetStylebox("fill", "ProgressBar", Box(Accent, Accent.Darkened(0.3f), radius: 3, border: 1, shadow: 0));
		return theme;
	}

	/// <summary>A small picture for the UI: kept square, pixel-sharp, and it never takes mouse clicks.</summary>
	public static TextureRect Icon(Texture2D texture, float size) => new()
	{
		Texture = texture,
		ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
		StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		CustomMinimumSize = new Vector2(size, size),
		MouseFilter = Control.MouseFilterEnum.Ignore,
		TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
	};

	/// <summary>
	/// Gives the theme to every UI tree: each Control whose parent isn't a Control (the roots under the
	/// CanvasLayers, which don't pass a theme down from the window). Call after the UI is built.
	/// </summary>
	public static void ApplyTo(Node node, Theme theme)
	{
		if (node is Control control && control.GetParent() is not Control)
			control.Theme = theme;
		foreach (var child in node.GetChildren())
			ApplyTo(child, theme);
	}

	/// <summary>A rounded, framed box; the shadow drops slightly downwards.</summary>
	public static StyleBoxFlat Box(Color background, Color frame, int radius, int border, int shadow)
	{
		var box = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = frame,
			ShadowColor = new Color(0, 0, 0, 0.45f),
			ShadowSize = shadow,
			ShadowOffset = new Vector2(0, shadow / 2f),
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
			AntiAliasing = true,
		};
		box.SetBorderWidthAll(border);
		box.SetCornerRadiusAll(radius);
		return box;
	}
}
