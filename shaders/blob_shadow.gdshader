// A soft dark patch on the ground under someone, so 3D figures over a painted background
// look like they stand on it.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_mix;

uniform float strength = 0.5;

void fragment() {
	float d = length(UV - vec2(0.5)) * 2.0;
	ALBEDO = vec3(0.0);
	ALPHA = strength * (1.0 - smoothstep(0.25, 1.0, d));
}
