// Night over the table screens (map, journal, ledger, inventory): one candle somewhere near
// the middle, the corners falling off into cold dark. Multiplies what's underneath.
shader_type canvas_item;
render_mode blend_mul;

uniform vec2 candle = vec2(0.42, 0.55);   // where the light is, in UV
uniform float reach = 0.75;

void fragment() {
	vec2 d = (UV - candle) * vec2(1.6, 1.0);
	float light = 1.0 - smoothstep(0.0, reach, length(d));
	vec3 warm = vec3(1.0, 0.88, 0.7);
	vec3 cold = vec3(0.16, 0.18, 0.3);
	COLOR = vec4(mix(cold, warm * 0.92, light), 1.0);
}
