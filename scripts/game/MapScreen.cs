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

		var title = new Label { Text = Tr("map_title"), HorizontalAlignment = HorizontalAlignment.Center };
		title.SetAnchorsPreset(LayoutPreset.CenterTop);
		title.OffsetLeft = -400;
		title.OffsetRight = 400;
		title.OffsetTop = 18;
		title.OffsetBottom = 90;
		title.AddThemeFontSizeOverride("font_size", 56);
		title.AddThemeColorOverride("font_color", new Color(0.2f, 0.11f, 0.05f));
		title.AddThemeColorOverride("font_shadow_color", new Color(0.98f, 0.93f, 0.8f, 0.8f));
		AddChild(title);

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
