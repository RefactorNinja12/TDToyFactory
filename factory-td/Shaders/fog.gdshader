// Fog of war over the map. The texture has one texel per tile: alpha 1 never seen, ~0.55 explored,
// 0 lit; red 1 on wall tiles. Instead of stretching it (blocky 64 px steps), every tile is split into
// small fog pixels, each takes a soft (gaussian) average of the tiles around it, and the result is
// rounded to a few opacity steps: a pixel-art gradient from light into dark blue. Wall edges facing
// the floor are drawn as a lighter outline, so the shape of the map shows through the fog.
shader_type canvas_item;

/// Fog pixels per tile side (8 = 8x8 px fog pixels on 64 px tiles).
uniform float pixels_per_tile = 8.0;
/// Opacity steps from fully lit to fully dark.
uniform float levels = 6.0;
/// How far the soft edge reaches, in tiles.
uniform float blur_tiles = 1.6;
uniform vec4 fog_color : source_color = vec4(0.03, 0.06, 0.19, 1.0);
uniform vec4 outline_color : source_color = vec4(0.22, 0.36, 0.72, 1.0);

bool is_wall(sampler2D tex, ivec2 tile, ivec2 size) {
	if (tile.x < 0 || tile.y < 0 || tile.x >= size.x || tile.y >= size.y)
		return true;
	return texelFetch(tex, tile, 0).r > 0.5;
}

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

	// Wall outline: the outermost fog pixel of a wall tile, on each side that borders floor.
	ivec2 size = ivec2(tiles);
	ivec2 tile = ivec2(floor(pos));
	vec2 inside = fract(pos);
	float edge = 1.0 / pixels_per_tile;
	float outline = 0.0;
	if (is_wall(TEXTURE, tile, size)) {
		if ((inside.x < edge && !is_wall(TEXTURE, tile + ivec2(-1, 0), size)) ||
			(inside.x > 1.0 - edge && !is_wall(TEXTURE, tile + ivec2(1, 0), size)) ||
			(inside.y < edge && !is_wall(TEXTURE, tile + ivec2(0, -1), size)) ||
			(inside.y > 1.0 - edge && !is_wall(TEXTURE, tile + ivec2(0, 1), size)))
			outline = 1.0;
	}
	COLOR = vec4(mix(fog_color.rgb, outline_color.rgb, outline), a);
}
