// Drifting night fog, a soft vignette and a little film grain over a painted cutscene.
// Put on a full-screen ColorRect above the pictures (its colour is ignored).
shader_type canvas_item;

uniform vec3 fog_color : source_color = vec3(0.56, 0.64, 0.74);
uniform float fog_amount : hint_range(0.0, 1.0) = 0.2;
uniform vec2 drift = vec2(0.012, -0.002);
uniform float vignette : hint_range(0.0, 1.0) = 0.55;
uniform float grain : hint_range(0.0, 0.2) = 0.02;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}

float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
	return v;
}

void fragment() {
	vec2 uv = UV;
	float aspect = 1.0 / SCREEN_PIXEL_SIZE.x * SCREEN_PIXEL_SIZE.y;
	vec2 p = vec2(uv.x * aspect, uv.y) * 3.0;
	float t = TIME;
	// Two banks of fog moving at different speeds; thicker towards the ground.
	float f = fbm(p + drift * t * 10.0) * 0.65 + fbm(p * 1.7 - drift.yx * t * 16.0 + 4.0) * 0.35;
	f = smoothstep(0.38, 0.85, f) * mix(0.55, 1.15, smoothstep(0.25, 1.0, uv.y));
	float fog = f * fog_amount;

	vec2 c = uv - 0.5;
	c.x *= 1.25;
	float v = smoothstep(0.35, 0.95, length(c)) * vignette;
	float g = (hash(uv * 1000.0 + fract(t * 23.0) * 100.0) - 0.5) * grain;

	// Fog lightens, the vignette darkens; premultiplied into one over-colour.
	vec3 col = fog_color * fog / max(fog + v, 1e-4);
	float alpha = clamp(fog + v, 0.0, 1.0);
	COLOR = vec4(col + g, alpha + abs(g));
}
