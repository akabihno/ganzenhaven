@tool
extends Sprite2D
## A clickable seagull that opens a visual-novel style dialogue box.
##
## The texture is assigned from code rather than stored on the node because the
## Godot MCP bridge has no command for writing a Texture2D onto a property
## (see handle_set_property in addons/godot_mcp/command_handler.gd). The @tool
## annotation makes the sprite show up in the editor as well as at run time.

const SEAGULL_TEXTURE: Texture2D = preload("res://assets/seagull_broken_wing.png")

const LINES: Array = [
	{
		"speaker": "",
		"text": "A herring gull is hunched by the office door. Its right wing hangs at an angle that wings are not built to reach.",
	},
	{
		"speaker": "Seagull",
		"text": "...",
	},
	{
		"speaker": "Seagull",
		"text": "Don't. Don't do the head-tilt thing. I have had quite enough sympathy for one morning.",
	},
	{
		"speaker": "",
		"text": "It shifts its weight, and the wing drags across the boards.",
	},
	{
		"speaker": "Seagull",
		"text": "Trawler winch. I was faster than it, right up until the moment I wasn't.",
	},
	{
		"speaker": "Seagull",
		"text": "So you're the new harbourmaster. Congratulations. Your first problem is standing directly in front of you.",
	},
]

## Typewriter speed. Click or press Space/Enter mid-line to reveal it instantly.
@export var characters_per_second: float = 42.0
## Alpha below this counts as a miss, so clicks land on the bird and not its box.
@export_range(0.0, 1.0) var alpha_hit_threshold: float = 0.15
## Draws a soft harbour-fog backdrop. Turn off if you put a real scene behind.
@export var draw_backdrop: bool = true

var _dialogue_root: MarginContainer
var _speaker_label: Label
var _text_label: Label
var _continue_hint: Label

var _line_index: int = 0
var _revealed: float = 0.0
var _active: bool = false
var _blink: float = 0.0
var _hit_mask: Image


func _ready() -> void:
	if texture == null:
		texture = SEAGULL_TEXTURE
	if Engine.is_editor_hint():
		return
	_cache_hit_mask()
	if draw_backdrop:
		_build_backdrop()
	_build_dialogue_ui()


# --- click detection -------------------------------------------------------

func _cache_hit_mask() -> void:
	if texture == null:
		return
	var image := texture.get_image()
	if image == null:
		return
	if image.is_compressed():
		# get_image() hands back the GPU-side format for imported textures.
		if image.decompress() != OK:
			return
	_hit_mask = image


func _sprite_rect() -> Rect2:
	var size := Vector2(texture.get_size())
	var origin := offset - (size * 0.5 if centered else Vector2.ZERO)
	return Rect2(origin, size)


func _is_on_bird(global_point: Vector2) -> bool:
	if texture == null:
		return false
	var rect := _sprite_rect()
	var local := to_local(global_point)
	if not rect.has_point(local):
		return false
	if _hit_mask == null:
		return true  # no mask available; fall back to the bounding box
	var pixel := Vector2i((local - rect.position).floor())
	pixel.x = clampi(pixel.x, 0, _hit_mask.get_width() - 1)
	pixel.y = clampi(pixel.y, 0, _hit_mask.get_height() - 1)
	return _hit_mask.get_pixelv(pixel).a > alpha_hit_threshold


func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	var clicked: bool = event is InputEventMouseButton \
		and event.pressed \
		and event.button_index == MOUSE_BUTTON_LEFT
	if _active:
		if clicked or event.is_action_pressed("ui_accept"):
			_advance()
			get_viewport().set_input_as_handled()
	elif clicked and _is_on_bird(get_global_mouse_position()):
		_open()
		get_viewport().set_input_as_handled()


func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	if not _active:
		# pointing-hand cursor while the bird is hoverable
		Input.set_default_cursor_shape(
			Input.CURSOR_POINTING_HAND if _is_on_bird(get_global_mouse_position())
			else Input.CURSOR_ARROW)
		return

	var full: String = LINES[_line_index]["text"]
	if _revealed < float(full.length()):
		_revealed = minf(_revealed + characters_per_second * delta, float(full.length()))
		_text_label.visible_characters = int(_revealed)
		_continue_hint.modulate.a = 0.0
	else:
		_blink += delta
		_continue_hint.modulate.a = 0.35 + 0.45 * (sin(_blink * 4.0) * 0.5 + 0.5)


# --- dialogue flow ---------------------------------------------------------

func _open() -> void:
	_line_index = 0
	_active = true
	_dialogue_root.visible = true
	Input.set_default_cursor_shape(Input.CURSOR_ARROW)
	_show_line()


func _advance() -> void:
	if not _active or _line_index >= LINES.size():
		return
	var full: String = LINES[_line_index]["text"]
	if _revealed < float(full.length()):
		_revealed = float(full.length())          # first click completes the line
		_text_label.visible_characters = full.length()
		return
	_line_index += 1
	if _line_index >= LINES.size():
		_close()
	else:
		_show_line()


func _show_line() -> void:
	var line: Dictionary = LINES[_line_index]
	var speaker: String = line["speaker"]
	_speaker_label.text = speaker
	_speaker_label.visible = speaker != ""
	_text_label.text = line["text"]
	_text_label.visible_characters = 0
	_revealed = 0.0
	_blink = 0.0


func _close() -> void:
	_active = false
	_dialogue_root.visible = false


# --- generated UI ----------------------------------------------------------

func _build_backdrop() -> void:
	var layer := CanvasLayer.new()
	layer.name = "Backdrop"
	layer.layer = -10
	add_child(layer)

	var gradient := Gradient.new()
	gradient.set_color(0, Color(0.78, 0.83, 0.87))   # pale harbour sky
	gradient.set_color(1, Color(0.62, 0.67, 0.71))
	var fill := GradientTexture2D.new()
	fill.gradient = gradient
	fill.fill_from = Vector2(0.0, 0.0)
	fill.fill_to = Vector2(0.0, 1.0)

	var rect := TextureRect.new()
	rect.texture = fill
	rect.stretch_mode = TextureRect.STRETCH_SCALE
	rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(rect)


func _panel_style() -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = Color(0.086, 0.110, 0.149, 0.93)
	box.border_color = Color(0.560, 0.630, 0.710, 0.55)
	box.set_border_width_all(2)
	box.set_corner_radius_all(10)
	box.set_content_margin_all(22)
	box.shadow_color = Color(0.0, 0.0, 0.0, 0.35)
	box.shadow_size = 12
	return box


func _build_dialogue_ui() -> void:
	var layer := CanvasLayer.new()
	layer.name = "DialogueLayer"
	layer.layer = 10
	add_child(layer)

	_dialogue_root = MarginContainer.new()
	_dialogue_root.name = "DialogueRoot"
	_dialogue_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_dialogue_root.add_theme_constant_override("margin_left", 44)
	_dialogue_root.add_theme_constant_override("margin_right", 44)
	_dialogue_root.add_theme_constant_override("margin_bottom", 36)
	_dialogue_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_dialogue_root.visible = false
	layer.add_child(_dialogue_root)

	# push the panel to the bottom of the screen
	var bottom := VBoxContainer.new()
	bottom.alignment = BoxContainer.ALIGNMENT_END
	bottom.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_dialogue_root.add_child(bottom)

	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(0.0, 180.0)
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_theme_stylebox_override("panel", _panel_style())
	bottom.add_child(panel)

	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 10)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(column)

	_speaker_label = Label.new()
	_speaker_label.add_theme_font_size_override("font_size", 22)
	_speaker_label.add_theme_color_override("font_color", Color(0.949, 0.706, 0.298))
	column.add_child(_speaker_label)

	_text_label = Label.new()
	_text_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_text_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_text_label.add_theme_font_size_override("font_size", 24)
	_text_label.add_theme_color_override("font_color", Color(0.925, 0.937, 0.949))
	column.add_child(_text_label)

	_continue_hint = Label.new()
	_continue_hint.text = "▼"
	_continue_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_continue_hint.add_theme_font_size_override("font_size", 18)
	_continue_hint.add_theme_color_override("font_color", Color(0.741, 0.796, 0.847))
	column.add_child(_continue_hint)
