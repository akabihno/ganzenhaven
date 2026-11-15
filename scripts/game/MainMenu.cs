using Godot;

/// <summary>Title screen: New game, Continue, language (English / Русский), Quit.</summary>
[GlobalClass]
public partial class MainMenu : Control
{
	private VBoxContainer _buttons;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		Build();
		Game.Instance.Music.Play("map");
		GameState.Instance.LanguageChanged += OnLanguageChanged;
	}

	public override void _ExitTree()
	{
		if (GameState.Instance != null)
			GameState.Instance.LanguageChanged -= OnLanguageChanged;
	}

	private void OnLanguageChanged(string language) => Build();

	private void Build()
	{
		_buttons?.QueueFree();
		_buttons = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		_buttons.AddThemeConstantOverride("separation", 18);
		_buttons.SetAnchorsPreset(LayoutPreset.CenterLeft);
		_buttons.OffsetLeft = 160;
		_buttons.OffsetRight = 640;
		_buttons.OffsetTop = -120;
		_buttons.OffsetBottom = 360;
		AddChild(_buttons);

		Add(Tr("menu_new"), () => Game.Instance.NewGame());
		var cont = Add(Tr("menu_continue"), () => Game.Instance.ContinueGame());
		cont.Disabled = !GameState.HasSave;

		var langs = new HBoxContainer();
		langs.AddThemeConstantOverride("separation", 12);
		foreach (var (code, name) in new[] { ("en", "English"), ("ru", "Русский") })
		{
			var b = new Button
			{
				Text = name,
				ToggleMode = true,
				ButtonPressed = GameState.Instance.Language == code,
				CustomMinimumSize = new Vector2(234, 64),
			};
			b.Pressed += () => GameState.Instance.SetLanguage(code);
			langs.AddChild(b);
		}
		_buttons.AddChild(langs);

		Add(Tr("menu_quit"), () => Game.Instance.Quit());
		((Control)_buttons.GetChild(0)).CallDeferred(Control.MethodName.GrabFocus);
	}

	private Button Add(string text, System.Action action)
	{
		var b = new Button { Text = text, CustomMinimumSize = new Vector2(480, 72) };
		b.AddThemeFontSizeOverride("font_size", 38);
		b.Pressed += action;
		_buttons.AddChild(b);
		return b;
	}
}
