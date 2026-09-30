// Fog of war over the map. The texture has one texel per tile (alpha: 1 never seen, ~0.55 explored,
// 0 lit). Instead of stretching it (blocky 64 px steps), every tile is split into small fog pixels, each
// takes a soft (gaussian) average of the tiles around it, and the result is rounded to a few opacity
// steps: a pixel-art gradient from light into darkness.
shader_type canvas_item;

/// Fog pixels per tile side (8 = 8x8 px fog pixels on 64 px tiles).
uniform float pixels_per_tile = 8.0;
/// Opacity steps from fully lit to fully dark.
uniform float levels = 6.0;
/// How far the soft edge reaches, in tiles.
uniform float blur_tiles = 1.6;

void fragment() {
	vec2 tiles = 1.0 / TEXTURE_PIXEL_SIZE;
	// Snap to the centre of this fog pixel, so the gradient comes in small square steps.
	vec2 pos = (floor(UV * tiles * pixels_per_tile) + 0.5) / pixels_per_tile;

	float sum = 0.0;
	float weight = 0.0;
	float sigma2 = blur_tiles * blur_tiles * 0.35;
	for (int y = -2; y <= 2; y++) {
		for (int x = -2; x <= 2; x++) {
			vec2 offset = vec2(float(x), float(y)) * (blur_tiles * 0.5);
			float w = exp(-dot(offset, offset) / sigma2);
			sum += texture(TEXTURE, (pos + offset) * TEXTURE_PIXEL_SIZE).a * w;
			weight += w;
		}
	}
	float a = sum / weight;
	a = round(a * (levels - 1.0)) / (levels - 1.0);
	COLOR = vec4(texture(TEXTURE, UV).rgb, a);
}
