using Godot;

/// <summary>
/// The buttons that are there on every in-game screen (locations and the map): Journal and
/// Menu, top-right. Also the journal (an open book on the map table) and the
/// "new in the journal" notice.
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

	// ---------------------------------------------------------------- journal

	// Writing areas on the two pages of assets/art/journal/journal_book.png, in the
	// 1920x1080 frame the picture is shown in (covered, like the map).
	private static readonly Rect2 LeftPage = new(330, 160, 560, 790);
	private static readonly Rect2 RightPage = new(1050, 160, 560, 790);
	private static readonly Color Ink = new(0.16f, 0.09f, 0.04f, 0.92f);
	private static readonly Color FadedInk = new(0.3f, 0.2f, 0.12f, 0.6f);

	private RichTextLabel _rightPage;
	private Label _closeHint;

	private void BuildJournal()
	{
		// The journal is an old book lying on the same table as the map; entries are written
		// into it by hand. Click anywhere or press Esc to put it away.
		_journal = new Control { Theme = Game.UiTheme, MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
		_journal.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_journal.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				CloseJournal();
		};
		AddChild(_journal);

		var book = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/art/journal/journal_book.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		book.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_journal.AddChild(book);

		// The writing is laid out in the 1920x1080 frame and scaled with the screen.
		var frame = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(1920, 1080) };
		_journal.AddChild(frame);
		_journal.Resized += () => FitFrame(frame);
		FitFrame(frame);

		var hand = GD.Load<Font>("res://assets/fonts/MarckScript-Regular.ttf");
		_journalText = Page(frame, LeftPage, hand, -0.6f);
		_rightPage = Page(frame, RightPage, hand, 0.5f);

		_closeHint = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			Position = new Vector2(1320, 1018),
			Size = new Vector2(560, 50),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_closeHint.AddThemeFontSizeOverride("font_size", 28);
		_closeHint.AddThemeColorOverride("font_color", new Color(0.92f, 0.84f, 0.68f, 0.75f));
		frame.AddChild(_closeHint);
	}

	private static RichTextLabel Page(Control parent, Rect2 area, Font hand, float tiltDegrees)
	{
		var page = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollActive = false,
			FitContent = false,
			Position = area.Position,
			Size = area.Size,
			// A hand never writes quite level.
			Rotation = Mathf.DegToRad(tiltDegrees),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		if (hand != null)
			page.AddThemeFontOverride("normal_font", hand);
		page.AddThemeFontSizeOverride("normal_font_size", 46);
		page.AddThemeColorOverride("default_color", Ink);
		page.AddThemeConstantOverride("line_separation", 10);
		parent.AddChild(page);
		return page;
	}

	/// <summary>Scales the 1920x1080 writing frame the same way the book picture is
	/// covered, so the text stays on the pages at any window shape.</summary>
	private void FitFrame(Control frame)
	{
		Vector2 screen = _journal.Size == Vector2.Zero ? new Vector2(1920, 1080) : _journal.Size;
		float scale = Mathf.Max(screen.X / 1920f, screen.Y / 1080f);
		frame.Scale = Vector2.One * scale;
		frame.Position = (screen - new Vector2(1920, 1080) * scale) / 2f;
	}

	public void OpenJournal()
	{
		// The notice is about what's now in the book; it would only cover the pages.
		_toastTween?.Kill();
		_toast.Hide();
		FillJournal();
		_journal.Modulate = new Color(1, 1, 1, 0);
		_journal.Show();
		CreateTween().TweenProperty(_journal, "modulate:a", 1f, 0.25f);
	}

	private void CloseJournal() => _journal?.Hide();

	private void FillJournal()
	{
		var state = GameState.Instance;
		string lang = state.Language;
		_closeHint.Text = Tr("journal_close_hint");

		// Tasks are written with their steps under them. A step whose task isn't in the
		// journal is written as a task of its own.
		bool InJournal(string id) => id.Length > 0 && FindEntry(id) != null;
		bool IsTask(GameState.JournalEntry e) => !InJournal(JournalText.Parent(e.Id, lang));

		void WriteTask(System.Text.StringBuilder page, GameState.JournalEntry task, bool crossed)
		{
			string text = JournalText.Get(task.Id, lang);
			if (crossed)
				page.Append($"[color=#{FadedInk.ToHtml()}][s]").Append(text).Append("[/s][/color]\n");
			else
				page.Append("~ ").Append(text).Append('\n');
			foreach (var step in state.Journal)
			{
				if (JournalText.Parent(step.Id, lang) != task.Id)
					continue;
				page.Append("[font_size=38]      ");
				string stepText = JournalText.Get(step.Id, lang);
				if (crossed || step.Done)
					page.Append($"[color=#{FadedInk.ToHtml()}][s]").Append(stepText).Append("[/s][/color]");
				else
					page.Append("– ").Append(stepText);
				page.Append("[/font_size]\n");
			}
		}

		// Left page: the heading and what still needs doing, newest first.
		var left = new System.Text.StringBuilder();
		left.Append("[font_size=68]").Append(Tr("journal_title")).Append("[/font_size]\n\n");
		bool any = false;
		for (int i = state.Journal.Count - 1; i >= 0; i--)
		{
			var entry = state.Journal[i];
			if (entry.Done || !IsTask(entry))
				continue;
			WriteTask(left, entry, false);
			any = true;
		}
		if (!any)
			left.Append($"[color=#{FadedInk.ToHtml()}]").Append(Tr("journal_empty")).Append("[/color]");
		_journalText.Text = left.ToString();

		// Right page: what's been done, crossed out, steps and all.
		var right = new System.Text.StringBuilder();
		for (int i = state.Journal.Count - 1; i >= 0; i--)
		{
			var entry = state.Journal[i];
			if (!entry.Done || !IsTask(entry))
				continue;
			if (right.Length == 0)
				right.Append("[font_size=52]").Append(Tr("journal_done_heading")).Append("[/font_size]\n\n");
			WriteTask(right, entry, true);
		}
		_rightPage.Text = right.ToString();
	}

	private static GameState.JournalEntry FindEntry(string id)
	{
		foreach (var e in GameState.Instance.Journal)
			if (e.Id == id)
				return e;
		return null;
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
		_toast.GrowHorizontal = Control.GrowDirection.Both;   // longer notices widen evenly
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
		string lang = GameState.Instance.Language;
		string entry = JournalText.Get(id, lang);
		// A step is announced with the task it belongs to.
		string task = JournalText.Parent(id, lang);
		if (task.Length > 0 && FindEntry(task) != null)
			entry = $"{JournalText.Get(task, lang)} — {entry}";
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
