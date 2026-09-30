// Fog of war over the map. The texture has one texel per tile: alpha 1 never seen, ~0.6 explored,
// 0 lit; red 1 on wall tiles. Instead of stretching it (blocky 64 px steps), every tile is split into
// small fog pixels, each takes a soft (gaussian) average of the tiles around it, and the result is
// rounded to a few opacity steps: a pixel-art gradient from light into dark blue.
// To make it feel alive: the edges sway slowly in waves, and the darkness itself drifts with faint
// patches of lighter/darker, purple/teal shades. Wall edges facing the floor are drawn as a lighter
// outline, so the shape of the map shows through the fog.
shader_type canvas_item;

/// Fog pixels per tile side (8 = 8x8 px fog pixels on 64 px tiles).
uniform float pixels_per_tile = 8.0;
/// Opacity steps from fully lit to fully dark.
uniform float levels = 6.0;
/// How far the soft edge reaches, in tiles.
uniform float blur_tiles = 3.0;
/// How far (in tiles) the edges sway, and how fast.
uniform float wave_tiles = 0.45;
uniform float wave_speed = 0.6;
/// How much the darkness varies (0 = one flat colour), how big the patches are (tiles) and how fast they drift.
uniform float shade_amount = 0.5;
uniform float shade_scale = 5.0;
uniform float shade_speed = 0.04;
uniform vec4 fog_color : source_color = vec4(0.03, 0.06, 0.19, 1.0);
uniform vec4 shade_light : source_color = vec4(0.10, 0.10, 0.30, 1.0);
uniform vec4 shade_teal : source_color = vec4(0.02, 0.13, 0.22, 1.0);
uniform vec4 outline_color : source_color = vec4(0.22, 0.36, 0.72, 1.0);

bool is_wall(sampler2D tex, ivec2 tile, ivec2 size) {
	if (tile.x < 0 || tile.y < 0 || tile.x >= size.x || tile.y >= size.y)
		return true;
	return texelFetch(tex, tile, 0).r > 0.5;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Smooth value noise, 0..1.
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec2 tiles = 1.0 / TEXTURE_PIXEL_SIZE;
	// Snap to the centre of this fog pixel, so everything comes in small square steps.
	vec2 pos = (floor(UV * tiles * pixels_per_tile) + 0.5) / pixels_per_tile;

	// Slow waves push the place the fog is read from back and forth: the edges sway.
	float t = TIME * wave_speed;
	vec2 sway = vec2(sin(pos.y * 0.7 + t) + 0.5 * sin(pos.y * 1.9 - t * 1.3),
		cos(pos.x * 0.6 + t * 0.9) + 0.5 * cos(pos.x * 1.7 + t * 1.1)) * (wave_tiles / 1.5);
	vec2 at = pos + sway;

	float sum = 0.0;
	float weight = 0.0;
	float sigma2 = blur_tiles * blur_tiles * 0.3;
	for (int y = -3; y <= 3; y++) {
		for (int x = -3; x <= 3; x++) {
			vec2 offset = vec2(float(x), float(y)) * (blur_tiles / 3.0);
			float w = exp(-dot(offset, offset) / sigma2);
			sum += texture(TEXTURE, (at + offset) * TEXTURE_PIXEL_SIZE).a * w;
			weight += w;
		}
	}
	float a = sum / weight;
	a = round(a * (levels - 1.0)) / (levels - 1.0);

	// The darkness isn't one flat colour: two slowly drifting noise layers, rounded to a few steps.
	vec2 drift = vec2(TIME * shade_speed, -TIME * shade_speed * 0.7);
	float light = noise(pos / shade_scale + drift) * 0.65 + noise(pos / (shade_scale * 0.45) - drift * 1.6) * 0.35;
	float hue = noise(pos / (shade_scale * 1.7) + drift.yx + 17.0);
	light = round(light * 4.0) / 4.0;
	hue = round(hue * 3.0) / 3.0;
	vec3 shade = mix(fog_color.rgb, shade_teal.rgb, hue * shade_amount);
	shade = mix(shade, shade_light.rgb, light * shade_amount);

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
	COLOR = vec4(mix(shade, outline_color.rgb, outline), a);
}
