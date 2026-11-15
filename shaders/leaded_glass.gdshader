// Small diamond panes of greenish glass held in lead, cut to a round-headed arch.
// Drawn on a quad whose UVs span the opening: width x height, round top above `spring`.
shader_type spatial;
render_mode cull_disabled, depth_draw_always;

uniform float width = 0.72;
uniform float height = 1.38;
uniform float spring = 1.02;   // height where the round top begins
uniform float radius = 0.36;
uniform float cell = 0.13;     // diamond size
uniform float lead = 0.05;     // came width, as a share of a cell

void fragment() {
	vec2 p = vec2((UV.x - 0.5) * width, (1.0 - UV.y) * height);
	if (p.y > spring && length(vec2(p.x, p.y - spring)) > radius)
		discard;

	float a = abs(fract((p.x + p.y) / cell) - 0.5);
	float b = abs(fract((p.x - p.y) / cell) - 0.5);
	float came = step(min(a, b), lead);

	// A lead border all round the opening.
	float edge = p.y > spring ? radius - length(vec2(p.x, p.y - spring)) : min(width * 0.5 - abs(p.x), p.y);
	came = max(came, step(edge, 0.02));

	// Old glass is never quite flat: a little waviness in tint and shine.
	float wobble = sin(p.x * 61.0 + p.y * 23.0) * sin(p.y * 47.0 - p.x * 17.0);
	vec3 glass = vec3(0.74, 0.84, 0.77) + 0.04 * wobble;
	ALBEDO = mix(glass, vec3(0.07, 0.07, 0.08), came);
	ROUGHNESS = mix(0.06 + 0.04 * wobble, 0.55, came);
	METALLIC = came * 0.35;
	SPECULAR = 0.65;
	ALPHA = mix(0.2, 1.0, came);
}
