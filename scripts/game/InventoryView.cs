using Godot;
using System.Collections.Generic;

/// <summary>
/// The inventory: everything the player carries, laid out on the oak table (the same table
/// the map and the journal lie on), each thing with a handwritten paper tag. Pointing at a
/// thing (or clicking it) shows its description on a slip at the bottom; clicking the table,
/// Esc or Enter puts it all away. Opened from the HUD (<see cref="GameHud"/>).
/// </summary>
/// <remarks>
/// Click one thing, then another, to use them together: that runs the script
/// dialogs/&lt;language&gt;/combine/&lt;a&gt;_&lt;b&gt;.txt (the two ids in alphabetical order, e.g.
/// combine/cloth_scissors), which does the rest with <c>$item</c>. No such script: they don't
/// go together. The purse (<see cref="Money"/>) is written in the top-right corner. Clicking a
/// thing also has the author read its name and description, from
/// audio/voice/&lt;language&gt;/items/&lt;id&gt;.mp3 (tools/elevenlabs_voice.py items).
/// </remarks>
public partial class InventoryView : Control
{
	// Laid out in a 1920x1080 frame over assets/art/inventory/table.png, scaled with the screen.
	private const int Columns = 4;
	private static readonly Vector2 GridOrigin = new(250, 170);
	private static readonly Vector2 Cell = new(370, 360);
	private static readonly Vector2 PictureSize = new(300, 250);

	private static readonly Color Ink = new(0.16f, 0.09f, 0.04f);
	private static readonly Color Chalk = new(0.95f, 0.88f, 0.72f);

	private Control _frame;
	private Control _items;
	private PanelContainer _slip;
	private RichTextLabel _slipText;
	private Label _title, _closeHint, _purse;
	private readonly System.Collections.Generic.Dictionary<string, Control> _things = new();
	private string _note = "";
	private AudioStreamPlayer _voice;   // a line under the slip text (e.g. "these don't go together")
	private Font _hand;
	private string _selected = "";

	public override void _Ready()
	{
		Theme = Game.UiTheme;
		MouseFilter = MouseFilterEnum.Stop;
		// Already in the tree here, so offsets must be reset along with the anchors.
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_hand = GD.Load<Font>("res://assets/fonts/MarckScript-Regular.ttf");

		GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Close();
		};

		var table = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/art/inventory/table.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		table.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(table);

		_frame = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(1920, 1080) };
		AddChild(_frame);
		Resized += FitFrame;

		_title = new Label { Position = new Vector2(150, 48), Size = new Vector2(900, 110), MouseFilter = MouseFilterEnum.Ignore };
		_title.AddThemeFontOverride("font", _hand);
		_title.AddThemeFontSizeOverride("font_size", 84);
		_title.AddThemeColorOverride("font_color", Chalk);
		_title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
		_title.AddThemeConstantOverride("shadow_offset_x", 3);
		_title.AddThemeConstantOverride("shadow_offset_y", 4);
		_frame.AddChild(_title);

		_items = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(1920, 1080) };
		_frame.AddChild(_items);

		// The slip at the bottom with the name and description of the thing pointed at.
		_slip = Paper(4f);
		_slip.Position = new Vector2(380, 900);
		_slip.Size = new Vector2(1160, 110);
		_slip.Visible = false;
		_slipText = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(1100, 0),
		};
		_slipText.AddThemeFontOverride("normal_font", _hand);
		_slipText.AddThemeFontOverride("bold_font", _hand);
		_slipText.AddThemeFontSizeOverride("bold_font_size", 42);
		_slipText.AddThemeFontSizeOverride("normal_font_size", 36);
		_slipText.AddThemeColorOverride("default_color", Ink);
		_slip.AddChild(_slipText);
		_frame.AddChild(_slip);

		_closeHint = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			Position = new Vector2(880, 1030),
			Size = new Vector2(1000, 46),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_closeHint.AddThemeFontSizeOverride("font_size", 28);
		_closeHint.AddThemeColorOverride("font_color", new Color(0.92f, 0.84f, 0.68f, 0.75f));
		_frame.AddChild(_closeHint);

		_purse = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			Position = new Vector2(1100, 66),
			Size = new Vector2(680, 70),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_purse.AddThemeFontOverride("font", _hand);
		_purse.AddThemeFontSizeOverride("font_size", 44);
		_purse.AddThemeColorOverride("font_color", Chalk);
		_purse.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
		_purse.AddThemeConstantOverride("shadow_offset_x", 2);
		_purse.AddThemeConstantOverride("shadow_offset_y", 3);
		_frame.AddChild(_purse);

		_voice = new AudioStreamPlayer { Bus = "Voice" };
		_voice.Finished += () => Game.Instance.Music.Duck(false);
		AddChild(_voice);
		VisibilityChanged += () => { if (!Visible) StopVoice(); };

		GameState.Instance.InventoryUpdated += (_, _) => { if (Visible) Fill(); };
		GameState.Instance.Changed += () => { if (Visible) UpdatePurse(); };

		_candle = Night.Candlelight(new Vector2(0.45f, 0.42f));
		AddChild(_candle);
		FitFrame();
	}

	private void UpdatePurse()
	{
		string lang = GameState.Instance.Language;
		string coins = Money.Format(Money.Stuivers, lang);
		_purse.Text = coins.Length == 0 ? Tr("purse_empty") : $"{Tr("purse")}: {coins}";
	}

	private ColorRect _candle;

	public void Open()
	{
		_candle.Visible = Night.IsNight;
		_selected = "";
		Fill();
		Show();
	}

	public void Close() => Hide();

	/// <summary>Lays the carried things out on the table, in the order they were picked up.</summary>
	public void Fill()
	{
		string lang = GameState.Instance.Language;
		_title.Text = Tr("inventory_title");
		_closeHint.Text = Tr("inventory_hint");
		UpdatePurse();
		foreach (var child in _items.GetChildren())
			child.QueueFree();
		_things.Clear();
		if (!GameState.Instance.HasItem(_selected))
			_selected = "";
		ShowSlip(_selected);

		var inventory = GameState.Instance.Inventory;
		if (inventory.Count == 0)
		{
			var tag = Tag(Tr("inventory_empty"), -2f);
			tag.Position = new Vector2(760, 470);
			_items.AddChild(tag);
			return;
		}

		for (int i = 0; i < inventory.Count; i++)
		{
			string id = inventory[i];
			var origin = GridOrigin + new Vector2(i % Columns * Cell.X, i / Columns * Cell.Y);
			// Each thing lies at its own slight angle, the same every time.
			float tilt = (float)(GD.Hash(id) % 1000) / 1000f * 12f - 6f;
			var thing = Thing(id, origin, tilt, lang);
			_things[id] = thing;
			_items.AddChild(thing);
		}
		ShowSelection();
	}

	private Control Thing(string id, Vector2 origin, float tilt, string lang)
	{
		var thing = new Control
		{
			Position = origin,
			Size = Cell - new Vector2(30, 20),
			PivotOffset = (Cell - new Vector2(30, 20)) / 2f,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};

		var texture = ItemText.Picture(id);
		if (texture != null)
		{
			var pictureRect = new Rect2((thing.Size.X - PictureSize.X) / 2f, 0, PictureSize.X, PictureSize.Y);
			// A soft shadow under it, as if lying on the table.
			thing.AddChild(Picture(texture, pictureRect with { Position = pictureRect.Position + new Vector2(10, 14) },
				tilt, new Color(0, 0, 0, 0.45f)));
			thing.AddChild(Picture(texture, pictureRect, tilt, Colors.White));
		}

		var tag = Tag(ItemText.Name(id, lang), -tilt * 0.4f);
		tag.Position = new Vector2(thing.Size.X / 2f - 120, PictureSize.Y + 6);
		thing.AddChild(tag);

		thing.MouseEntered += () =>
		{
			Lift(thing, 1.06f);
			ShowSlip(id);
		};
		thing.MouseExited += () =>
		{
			Lift(thing, id == _selected ? 1.1f : 1f);
			ShowSlip(_selected);
		};
		thing.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				thing.AcceptEvent();   // a click on a thing doesn't put the inventory away
				Clicked(id);
			}
		};
		return thing;
	}

	private static TextureRect Picture(Texture2D texture, Rect2 area, float tilt, Color modulate) => new()
	{
		Texture = texture,
		ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
		StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		Position = area.Position,
		Size = area.Size,
		PivotOffset = area.Size / 2f,
		Rotation = Mathf.DegToRad(tilt),
		Modulate = modulate,
		MouseFilter = MouseFilterEnum.Ignore,
	};

	/// <summary>First click picks a thing up; a click on another uses the two together.</summary>
	private void Clicked(string id)
	{
		_note = "";
		if (_selected.Length == 0 || _selected == id)
		{
			_selected = _selected == id ? "" : id;
			ShowSelection();
			ShowSlip(id);
			if (_selected.Length > 0)
				Say(id);
			return;
		}

		string first = _selected;
		_selected = "";
		StopVoice();
		ShowSelection();
		string script = CombineScript(first, id);
		if (script != null)
		{
			ShowSlip("");
			Game.Instance.RunDialogue(script);
		}
		else
		{
			_note = Tr("inventory_no_combine");
			ShowSlip(id);
		}
	}

	/// <summary>The author reads the thing's name and description.</summary>
	private void Say(string id)
	{
		StopVoice();
		string path = $"res://audio/voice/{GameState.Instance.Language}/items/{id}.mp3";
		if (!Settings.VoiceOn || !ResourceLoader.Exists(path))
			return;
		_voice.Stream = GD.Load<AudioStream>(path);
		_voice.Play();
		Game.Instance.Music.Duck(true);
	}

	private void StopVoice()
	{
		if (_voice == null || !_voice.Playing)
			return;
		_voice.Stop();
		Game.Instance.Music.Duck(false);
	}

	/// <summary>combine/&lt;a&gt;_&lt;b&gt; for the two ids in alphabetical order, if it exists.</summary>
	public static string CombineScript(string a, string b)
	{
		var pair = string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);
		string id = $"combine/{pair.Item1}_{pair.Item2}";
		string lang = GameState.Instance.Language;
		return FileAccess.FileExists($"res://dialogs/{lang}/{id}.txt") || FileAccess.FileExists($"res://dialogs/en/{id}.txt")
			? id
			: null;
	}

	private void ShowSelection()
	{
		foreach (var (id, thing) in _things)
		{
			bool on = id == _selected;
			Lift(thing, on ? 1.1f : 1f);
			thing.Modulate = on ? new Color(1.12f, 1.06f, 0.92f) : Colors.White;
		}
	}

	private void Lift(Control thing, float scale)
	{
		var tween = thing.CreateTween();
		tween.TweenProperty(thing, "scale", Vector2.One * scale, 0.12f);
	}

	private void ShowSlip(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			_slip.Hide();
			return;
		}
		string lang = GameState.Instance.Language;
		string description = ItemText.Description(id, lang);
		string text = string.IsNullOrEmpty(description)
			? $"[b]{ItemText.Name(id, lang)}[/b]"
			: $"[b]{ItemText.Name(id, lang)}.[/b]  {description}";
		string fade = new Color(0.4f, 0.25f, 0.12f).ToHtml(false);
		if (_note.Length > 0)
			text += $"\n[color=#{fade}]{_note}[/color]";
		else if (id == _selected)
			text += $"\n[color=#{fade}]{Tr("inventory_use_with")}[/color]";
		_slipText.Text = text;
		_slip.Show();
	}

	/// <summary>A small paper tag with a handwritten word on it.</summary>
	private Control Tag(string text, float tilt)
	{
		var tag = Paper(2f);
		tag.CustomMinimumSize = new Vector2(240, 0);
		tag.Rotation = Mathf.DegToRad(tilt);
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		label.AddThemeFontOverride("font", _hand);
		label.AddThemeFontSizeOverride("font_size", 34);
		label.AddThemeColorOverride("font_color", Ink);
		label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
		tag.AddChild(label);
		return tag;
	}

	private static PanelContainer Paper(float corner)
	{
		var paper = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		paper.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.87f, 0.8f, 0.64f),
			BorderColor = new Color(0.55f, 0.42f, 0.26f),
			BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
			CornerRadiusTopLeft = (int)corner, CornerRadiusTopRight = (int)corner,
			CornerRadiusBottomLeft = (int)corner, CornerRadiusBottomRight = (int)corner,
			ShadowColor = new Color(0, 0, 0, 0.45f),
			ShadowSize = 8,
			ShadowOffset = new Vector2(3, 5),
			ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 6, ContentMarginBottom = 8,
		});
		return paper;
	}

	private void FitFrame()
	{
		Vector2 screen = Size == Vector2.Zero ? new Vector2(1920, 1080) : Size;
		float scale = Mathf.Max(screen.X / 1920f, screen.Y / 1080f);
		_frame.Scale = Vector2.One * scale;
		_frame.Position = (screen - new Vector2(1920, 1080) * scale) / 2f;
	}
}
