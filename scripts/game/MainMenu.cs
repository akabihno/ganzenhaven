using Godot;

/// <summary>
/// Title screen: New game, Continue, language (English / Русский), Settings, Quit.
/// New game asks first when there is a saved game it would overwrite.
/// Settings opens a panel beside the buttons with the music and sound-effect volumes and the
/// voice-over switch (see <see cref="Settings"/>).
/// </summary>
[GlobalClass]
public partial class MainMenu : Control
{
	private VBoxContainer _buttons;
	private PanelContainer _settings;
	private bool _settingsOpen;
	private Button _settingsButton;
	private AudioStreamPlayer _sfxPreview;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		_sfxPreview = new AudioStreamPlayer { Bus = "Sfx", Stream = GD.Load<AudioStream>("res://audio/sfx/gull_indignant.ogg") };
		AddChild(_sfxPreview);
		Build();
		// The title screen shows the harbour as the saved game left it: by night after the death.
		if (GetNodeOrNull<TextureRect>("Background") is { } background && SavedAfterTheDeath())
			background.Texture = Night.NightOf(background.Texture);
		Game.Instance.Music.Play("map");
		GameState.Instance.LanguageChanged += OnLanguageChanged;
	}

	public override void _ExitTree()
	{
		if (GameState.Instance != null)
			GameState.Instance.LanguageChanged -= OnLanguageChanged;
		_sfxPreview.Stop();
		_sfxPreview.Stream = null;
		Settings.Save();   // slider moves made with the keyboard aren't saved as they happen
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (_confirm != null && e.IsActionPressed("ui_cancel"))
		{
			CloseConfirm();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (_settingsOpen && e.IsActionPressed("ui_cancel"))
		{
			_settingsOpen = false;
			_settings.Hide();
			_settingsButton.SetPressedNoSignal(false);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnLanguageChanged(string language) => Build();

	private static bool SavedAfterTheDeath()
	{
		if (GameState.Instance.IsAfter)
			return true;
		if (!GameState.HasSave)
			return false;
		using var file = FileAccess.Open("user://save.json", FileAccess.ModeFlags.Read);
		return file != null && file.GetAsText().Contains($"\"{GameState.TavernKeeperDead}\"");
	}

	private void Build()
	{
		_buttons?.QueueFree();
		_buttons = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		_buttons.AddThemeConstantOverride("separation", 18);
		_buttons.SetAnchorsPreset(LayoutPreset.CenterLeft);
		_buttons.OffsetLeft = 160;
		_buttons.OffsetRight = 640;
		_buttons.OffsetTop = -160;
		_buttons.OffsetBottom = 400;
		AddChild(_buttons);

		Add(Tr("menu_new"), () =>
		{
			if (GameState.HasSave)
				ConfirmNewGame();
			else
				Game.Instance.NewGame();
		});
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

		_settingsButton = Add(Tr("menu_settings"), () =>
		{
			_settingsOpen = !_settingsOpen;
			_settings.Visible = _settingsOpen;
		});
		_settingsButton.ToggleMode = true;
		_settingsButton.SetPressedNoSignal(_settingsOpen);

		Add(Tr("menu_quit"), () => Game.Instance.Quit());
		((Control)_buttons.GetChild(0)).CallDeferred(Control.MethodName.GrabFocus);

		BuildSettings();
	}

	private Button Add(string text, System.Action action)
	{
		var b = new Button { Text = text, CustomMinimumSize = new Vector2(480, 72) };
		b.AddThemeFontSizeOverride("font_size", 38);
		b.Pressed += action;
		_buttons.AddChild(b);
		return b;
	}

	// ---------------------------------------------------------------- new game warning

	private Control _confirm;

	/// <summary>"Start a new game? Your saved progress will be lost." over a dimmed menu.</summary>
	private void ConfirmNewGame()
	{
		CloseConfirm();
		_confirm = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
		_confirm.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_confirm);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.OffsetLeft = -460;
		panel.OffsetRight = 460;
		panel.OffsetTop = -150;
		panel.OffsetBottom = 150;
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical = GrowDirection.Both;
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.055f, 0.045f, 0.96f),
			BorderColor = new Color(0.93f, 0.78f, 0.48f, 0.9f),
			BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 48, ContentMarginRight = 48, ContentMarginTop = 36, ContentMarginBottom = 36,
		});
		_confirm.AddChild(panel);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", 36);
		panel.AddChild(column);

		var question = new Label
		{
			Text = Tr("new_game_confirm"),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(820, 0),
		};
		question.AddThemeFontSizeOverride("font_size", 38);
		column.AddChild(question);

		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		buttons.AddThemeConstantOverride("separation", 28);
		column.AddChild(buttons);
		Button Choice(string text, System.Action action)
		{
			var b = new Button { Text = text, CustomMinimumSize = new Vector2(300, 68) };
			b.AddThemeFontSizeOverride("font_size", 34);
			b.Pressed += action;
			buttons.AddChild(b);
			return b;
		}
		Choice(Tr("new_game_yes"), () => Game.Instance.NewGame());
		// The safe answer has the focus, so a stray Enter doesn't wipe the save.
		Choice(Tr("new_game_no"), CloseConfirm).CallDeferred(Control.MethodName.GrabFocus);
	}

	private void CloseConfirm()
	{
		_confirm?.QueueFree();
		_confirm = null;
		((Control)_buttons.GetChild(0)).CallDeferred(Control.MethodName.GrabFocus);
	}

	// ---------------------------------------------------------------- settings panel

	private void BuildSettings()
	{
		_settings?.QueueFree();
		_settings = new PanelContainer { Visible = _settingsOpen };
		_settings.SetAnchorsPreset(LayoutPreset.CenterLeft);
		_settings.OffsetLeft = 700;
		_settings.OffsetRight = 1240;
		_settings.OffsetTop = -40;
		_settings.OffsetBottom = 300;
		_settings.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.055f, 0.045f, 0.88f),
			BorderColor = new Color(0.62f, 0.5f, 0.3f, 0.9f),
			BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 36, ContentMarginRight = 36, ContentMarginTop = 24, ContentMarginBottom = 28,
		});
		AddChild(_settings);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		_settings.AddChild(column);

		VolumeRow(column, Tr("settings_music"), Settings.MusicVolume,
			v => Settings.SetMusicVolume(v, save: false), null);
		VolumeRow(column, Tr("settings_sfx"), Settings.SfxVolume,
			v => Settings.SetSfxVolume(v, save: false), () => _sfxPreview.Play());

		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
		var voice = new CheckButton { Text = Tr("settings_voice"), ButtonPressed = Settings.VoiceOn };
		voice.AddThemeFontSizeOverride("font_size", 34);
		voice.Toggled += Settings.SetVoiceOn;
		column.AddChild(voice);
	}

	private static void VolumeRow(VBoxContainer column, string label, float value,
		System.Action<float> onChange, System.Action onRelease)
	{
		var title = new Label { Text = label };
		title.AddThemeFontSizeOverride("font_size", 32);
		column.AddChild(title);

		var slider = new HSlider
		{
			MinValue = 0, MaxValue = 1, Step = 0.01, Value = value,
			CustomMinimumSize = new Vector2(0, 40),
		};
		slider.ValueChanged += v => onChange((float)v);
		// Saved (and, for sound effects, heard) once the slider is let go.
		slider.DragEnded += _ =>
		{
			Settings.Save();
			onRelease?.Invoke();
		};
		column.AddChild(slider);
	}
}
