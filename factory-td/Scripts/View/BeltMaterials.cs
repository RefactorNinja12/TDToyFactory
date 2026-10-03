using System.Globalization;
using FactoryTD.UI;
using Godot;

namespace FactoryTD.View;

/// <summary>
/// The shared belt shader: draws tread ridges moving over the rubber of straight belts and curves at the item
/// speed (UI/BeltLook), on the GPU, so a thousand belts cost nothing extra. Works in the sprite's own pixels,
/// so turning and mirroring (left curves) carry the movement along correctly.
/// </summary>
public static class BeltMaterials
{
	private static ShaderMaterial _straight, _curve;

	public static ShaderMaterial Straight => _straight ??= Make(curve: false);

	public static ShaderMaterial Curve => _curve ??= Make(curve: true);

	private static ShaderMaterial Make(bool curve)
	{
		string F(float v) => v.ToString("0.0###", CultureInfo.InvariantCulture);
		var shader = new Shader
		{
			Code = $@"
shader_type canvas_item;
const float SIZE = {F(BeltLook.TileSize)};
const float SPEED = {F(BeltLook.PixelsPerSecond)};
const float SPACING = {F(BeltLook.TreadSpacing)};
const float WIDTH = {F(BeltLook.TreadWidth)};
const float RUBBER_FROM = {F(BeltLook.RubberFrom)};
const float RUBBER_TO = {F(BeltLook.RubberTo)};
const bool CURVE = {(curve ? "true" : "false")};

void fragment() {{
	vec2 p = UV * SIZE;
	float across, along;
	if (CURVE) {{
		// A right turn round the corner (0, SIZE): along = arc length at the middle of the rubber.
		vec2 d = vec2(p.x, SIZE - p.y);
		across = length(d);
		along = (1.5707963 - atan(d.y, d.x)) * (RUBBER_FROM + RUBBER_TO) * 0.5;
	}} else {{
		across = p.y;
		along = p.x;
	}}
	float s = fract((along - TIME * SPEED) / SPACING) * SPACING;
	float ridge = step(s, WIDTH) * step(RUBBER_FROM, across) * step(across, RUBBER_TO - 0.5);
	COLOR.rgb = mix(COLOR.rgb, COLOR.rgb * 1.55 + 0.05, ridge * 0.7);
}}",
		};
		return new ShaderMaterial { Shader = shader };
	}
}
