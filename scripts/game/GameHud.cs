using Godot;

/// <summary>
/// The buttons that are there on every in-game screen (locations and the map): Journal and
/// Menu, top-right. Also the journal panel and the "new in the journal" notice.
/// Owned by <see cref="Game"/>; hidden on the main menu, in cutscenes and during dialogue.
/// </summary>
public partial class GameHud : CanvasLayer
{
	/// <summary>Right edge of the free space left of the HUD buttons, for a screen's own
	/// button (Map, Back): place it at offsets [ScreenButtonRight - 200, ScreenButtonRight].</summary>
	public const float ScreenButtonRight = -480f;

	private Control _buttons;
	private Button _journalButton, _menuButton;
	private Control _journal;
	private RichTextLabel _journalText;
	private Label _journalTitle;
	private Button _closeButton;
	private PanelContainer _toast;
	private Label _toastText;
	private Tween _toastTween;
	private bool _onGameScreen;

	public override void _Ready()
	{
		Layer = 40;
		BuildButtons();
		BuildJournal();
		BuildToast();
		GameState.Instance.JournalUpdated += OnJournalUpdated;
		GameState.Instance.LanguageChanged += _ => Relabel();
		Relabel();
	}

	/// <summary>Called by <see cref="Game"/> whenever the screen changes.</summary>
	public void SetGameScreen(bool on)
	{
		_onGameScreen = on;
		if (!on)
			CloseJournal();
	}

	public override void _Process(double delta)
	{
		_buttons.Visible = _onGameScreen && !Game.Instance.Dialogue.IsActive && !_journal.Visible;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (_journal.Visible && (e.IsActionPressed("ui_cancel") || e.IsActionPressed("ui_accept")))
		{
			CloseJournal();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Relabel()
	{
		_journalButton.Text = Tr("btn_journal");
		_menuButton.Text = Tr("btn_menu");
		_journalTitle.Text = Tr("journal_title");
		_closeButton.Text = Tr("btn_close");
		if (_journal.Visible)
			FillJournal();
	}

	// ---------------------------------------------------------------- buttons

	private void BuildButtons()
	{
		_buttons = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Game.UiTheme };
		_buttons.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_buttons);

		_menuButton = HudButton(-240);
		_menuButton.Pressed += () => Game.Instance.GoToMenu();
		_journalButton = HudButton(-460);
		_journalButton.Pressed += OpenJournal;
	}

	private Button HudButton(float left)
	{
		var b = new Button { CustomMinimumSize = new Vector2(200, 64) };
		b.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		b.OffsetLeft = left;
		b.OffsetRight = left + 200;
		b.OffsetTop = 32;
		b.OffsetBottom = 96;
		b.AddThemeFontSizeOverride("font_size", 34);
		_buttons.AddChild(b);
		return b;
	}

	// ---------------------------------------------------------------- journal panel

	private void BuildJournal()
	{
		_journal = new Control { Theme = Game.UiTheme, MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
		_journal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_journal.GuiInput += e =>
		{
			// A click outside the page closes it.
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				CloseJournal();
		};
		AddChild(_journal);

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_journal.AddChild(dim);

		var page = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
		page.SetAnchorsPreset(Control.LayoutPreset.Center);
		page.OffsetLeft = -520;
		page.OffsetRight = 520;
		page.OffsetTop = -400;
		page.OffsetBottom = 400;
		page.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.86f, 0.8f, 0.66f),
			BorderColor = new Color(0.36f, 0.24f, 0.12f),
			BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 64, ContentMarginRight = 64, ContentMarginTop = 40, ContentMarginBottom = 36,
			ShadowColor = new Color(0, 0, 0, 0.6f), ShadowSize = 24,
		});
		_journal.AddChild(page);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 18);
		page.AddChild(column);

		_journalTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_journalTitle.AddThemeFontSizeOverride("font_size", 58);
		_journalTitle.AddThemeColorOverride("font_color", new Color(0.28f, 0.14f, 0.06f));
		_journalTitle.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0));
		column.AddChild(_journalTitle);

		_journalText = new RichTextLabel
		{
			BbcodeEnabled = true,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			ScrollActive = true,
		};
		_journalText.AddThemeFontSizeOverride("normal_font_size", 38);
		_journalText.AddThemeColorOverride("default_color", new Color(0.2f, 0.12f, 0.06f));
		column.AddChild(_journalText);

		_closeButton = new Button { CustomMinimumSize = new Vector2(240, 64), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		_closeButton.AddThemeFontSizeOverride("font_size", 34);
		_closeButton.Pressed += CloseJournal;
		column.AddChild(_closeButton);
	}

	public void OpenJournal()
	{
		FillJournal();
		_journal.Show();
		_closeButton.GrabFocus();
	}

	private void CloseJournal() => _journal?.Hide();

	private void FillJournal()
	{
		var state = GameState.Instance;
		var text = new System.Text.StringBuilder();
		// Open tasks first, newest at the top; finished ones below, struck through and faded.
		for (int i = state.Journal.Count - 1; i >= 0; i--)
			if (!state.Journal[i].Done)
				text.Append("•  ").Append(JournalText.Get(state.Journal[i].Id, state.Language)).Append('\n');
		for (int i = state.Journal.Count - 1; i >= 0; i--)
			if (state.Journal[i].Done)
				text.Append("[color=#6b5a44][s]").Append(JournalText.Get(state.Journal[i].Id, state.Language)).Append("[/s][/color]\n");
		_journalText.Text = text.Length > 0 ? text.ToString() : $"[i]{Tr("journal_empty")}[/i]";
	}

	// ---------------------------------------------------------------- notice

	private void BuildToast()
	{
		_toast = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Game.UiTheme, Visible = false };
		_toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_toast.OffsetLeft = -420;
		_toast.OffsetRight = 420;
		_toast.OffsetTop = 120;
		_toast.OffsetBottom = 200;
		_toast.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.055f, 0.045f, 0.88f),
			BorderColor = new Color(0.93f, 0.78f, 0.48f, 0.9f),
			BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 10, ContentMarginBottom = 10,
		});
		AddChild(_toast);
		_toastText = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_toastText.AddThemeFontSizeOverride("font_size", 36);
		_toastText.AddThemeColorOverride("font_color", new Color(0.97f, 0.87f, 0.62f));
		_toast.AddChild(_toastText);
	}

	private void OnJournalUpdated(string id, bool done)
	{
		string entry = JournalText.Get(id, GameState.Instance.Language);
		_toastText.Text = $"{Tr(done ? "journal_done" : "journal_new")}: {entry}";
		_toastTween?.Kill();
		_toast.Modulate = new Color(1, 1, 1, 0);
		_toast.Show();
		_toastTween = CreateTween();
		_toastTween.TweenProperty(_toast, "modulate:a", 1f, 0.3f);
		_toastTween.TweenInterval(2.8);
		_toastTween.TweenProperty(_toast, "modulate:a", 0f, 0.6f);
		_toastTween.TweenCallback(Callable.From(_toast.Hide));
	}
}
