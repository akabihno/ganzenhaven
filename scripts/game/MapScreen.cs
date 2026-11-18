using Godot;

/// <summary>
/// The town map: the illustrated parchment with a <see cref="MapMarker"/> on every place
/// you can travel to. Esc or "Back" returns to where you were.
/// </summary>
[Tool]
[GlobalClass]
public partial class MapScreen : Control
{
	private Button _back;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		if (Engine.IsEditorHint())
			return;
		Game.Instance.Music.Play("map");

		// The town's name in the parchment's top-left corner, in gothic letters (a Slavonic
		// display face takes over for the Cyrillic name, which the blackletter font lacks).
		var gothic = GD.Load<FontFile>("res://assets/fonts/UnifrakturMaguntia-Regular.woff");
		var slavonic = GD.Load<Font>("res://assets/fonts/RuslanDisplay-Regular.woff");
		var titleFont = (FontFile)gothic.Duplicate();
		titleFont.Fallbacks = new Godot.Collections.Array<Font> { slavonic };
		var title = new Label
		{
			Text = Tr("map_title"),
			Position = new Vector2(236, 30),
			Size = new Vector2(900, 130),
			Rotation = Mathf.DegToRad(-1.5f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		title.AddThemeFontOverride("font", titleFont);
		// As big as fits before the buttons: the Cyrillic face is much wider than the gothic.
		int size = 104;
		while (size > 48 && titleFont.GetStringSize(title.Text, HorizontalAlignment.Left, -1, size).X > 720)
			size -= 4;
		title.AddThemeFontSizeOverride("font_size", size);
		title.AddThemeColorOverride("font_color", new Color(0.22f, 0.11f, 0.05f, 0.92f));
		title.AddThemeColorOverride("font_shadow_color", new Color(0.55f, 0.4f, 0.25f, 0.25f));
		title.AddThemeConstantOverride("shadow_offset_x", 2);
		title.AddThemeConstantOverride("shadow_offset_y", 2);
		AddChild(title);

		// At night the map is read by a single candle.
		AddChild(Night.Candlelight(new Vector2(0.45f, 0.55f)));

		_back = new Button { Text = Tr("btn_back"), CustomMinimumSize = new Vector2(200, 64) };
		_back.SetAnchorsPreset(LayoutPreset.TopRight);
		_back.OffsetLeft = GameHud.ScreenButtonRight - 200;
		_back.OffsetRight = GameHud.ScreenButtonRight;
		_back.OffsetTop = 32;
		_back.OffsetBottom = 96;
		_back.Pressed += Back;
		AddChild(_back);
	}

	private void Back() => Game.Instance.GoToLocation(GameState.Instance.Location);

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Engine.IsEditorHint() && e.IsActionPressed("ui_cancel"))
		{
			Back();
			GetViewport().SetInputAsHandled();
		}
	}
}
