// A firefly: a tiny bright core in a soft halo, always facing the camera. Brightness per
// firefly comes in INSTANCE_CUSTOM.r (set by FoxDance.cs each frame).
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_mix, fog_disabled, shadows_disabled;

uniform vec3 glow : source_color = vec3(0.86, 1.0, 0.5);

varying float brightness;

void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	brightness = INSTANCE_CUSTOM.r;
}

void fragment() {
	float d = length(UV - vec2(0.5)) * 2.0;
	float core = exp(-d * d * 30.0);
	float halo = exp(-d * d * 5.0) * 0.45;
	ALBEDO = mix(glow, vec3(1.0, 1.0, 0.9), core);
	ALPHA = clamp((core + halo) * brightness, 0.0, 1.0);
}
