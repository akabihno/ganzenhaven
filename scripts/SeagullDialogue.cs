using Godot;

/// <summary>
/// A clickable seagull that opens a visual-novel style dialogue box.
/// </summary>
/// <remarks>
/// The texture is assigned from code rather than stored on the node because the Godot
/// MCP bridge has no command for writing a Texture2D onto a property (see
/// handle_set_property in addons/godot_mcp/command_handler.gd). The [Tool] attribute
/// makes the sprite show up in the editor as well as at run time.
/// </remarks>
[Tool]
[GlobalClass]
public partial class SeagullDialogue : Sprite2D
{
	private const string SeagullTexturePath = "res://assets/seagull_broken_wing_lifelike.png";

	private static readonly (string Speaker, string Text)[] Lines =
	{
		("", "A herring gull is hunched by the office door. Its right wing hangs at an "
			+ "angle that wings are not built to reach."),
		("Seagull", "..."),
		("Seagull", "Don't. Don't do the head-tilt thing. I have had quite enough "
			+ "sympathy for one morning."),
		("", "It shifts its weight, and the wing drags across the boards."),
		("Seagull", "Trawler winch. I was faster than it, right up until the moment I wasn't."),
		("Seagull", "So you're the new harbourmaster. Congratulations. Your first problem "
			+ "is standing directly in front of you."),
	};

	/// <summary>Typewriter speed. Click or press Space/Enter mid-line to reveal it instantly.</summary>
	[Export] public float CharactersPerSecond { get; set; } = 42.0f;

	/// <summary>Alpha below this counts as a miss, so clicks land on the bird and not its box.</summary>
	[Export(PropertyHint.Range, "0,1")] public float AlphaHitThreshold { get; set; } = 0.15f;

	/// <summary>Draws a soft harbour-fog backdrop. Turn off if you put a real scene behind.</summary>
	[Export] public bool DrawBackdrop { get; set; } = true;

	private MarginContainer _dialogueRoot;
	private Label _speakerLabel;
	private Label _textLabel;
	private Label _continueHint;

	private int _lineIndex;
	private float _revealed;
	private bool _active;
	private float _blink;
	private Image _hitMask;

	public override void _Ready()
	{
		Texture ??= GD.Load<Texture2D>(SeagullTexturePath);

		if (Engine.IsEditorHint())
			return;

		CacheHitMask();
		if (DrawBackdrop)
			BuildBackdrop();
		BuildDialogueUi();
	}

	// --- click detection ---------------------------------------------------

	private void CacheHitMask()
	{
		if (Texture == null)
			return;

		Image image = Texture.GetImage();
		if (image == null)
			return;

		// GetImage() hands back the GPU-side format for imported textures.
		if (image.IsCompressed() && image.Decompress() != Error.Ok)
			return;

		_hitMask = image;
	}

	private Rect2 SpriteRect()
	{
		Vector2 size = Texture.GetSize();
		Vector2 origin = Offset - (Centered ? size * 0.5f : Vector2.Zero);
		return new Rect2(origin, size);
	}

	private bool IsOnBird(Vector2 globalPoint)
	{
		if (Texture == null)
			return false;

		Rect2 rect = SpriteRect();
		Vector2 local = ToLocal(globalPoint);
		if (!rect.HasPoint(local))
			return false;

		if (_hitMask == null)
			return true; // no mask available; fall back to the bounding box

		Vector2 pixel = (local - rect.Position).Floor();
		int x = Mathf.Clamp((int)pixel.X, 0, _hitMask.GetWidth() - 1);
		int y = Mathf.Clamp((int)pixel.Y, 0, _hitMask.GetHeight() - 1);
		return _hitMask.GetPixel(x, y).A > AlphaHitThreshold;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint())
			return;

		bool clicked = @event is InputEventMouseButton
		{
			Pressed: true, ButtonIndex: MouseButton.Left
		};

		if (_active)
		{
			if (clicked || @event.IsActionPressed("ui_accept"))
			{
				Advance();
				GetViewport().SetInputAsHandled();
			}
		}
		else if (clicked && IsOnBird(GetGlobalMousePosition()))
		{
			Open();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		if (!_active)
		{
			// pointing-hand cursor while the bird is hoverable
			Input.SetDefaultCursorShape(IsOnBird(GetGlobalMousePosition())
				? Input.CursorShape.PointingHand
				: Input.CursorShape.Arrow);
			return;
		}

		string full = Lines[_lineIndex].Text;
		Color hint = _continueHint.Modulate;

		if (_revealed < full.Length)
		{
			_revealed = Mathf.Min(_revealed + CharactersPerSecond * (float)delta, full.Length);
			_textLabel.VisibleCharacters = (int)_revealed;
			hint.A = 0.0f;
		}
		else
		{
			_blink += (float)delta;
			hint.A = 0.35f + 0.45f * (Mathf.Sin(_blink * 4.0f) * 0.5f + 0.5f);
		}

		_continueHint.Modulate = hint;
	}

	// --- dialogue flow -----------------------------------------------------

	private void Open()
	{
		_lineIndex = 0;
		_active = true;
		_dialogueRoot.Visible = true;
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
		ShowLine();
	}

	private void Advance()
	{
		if (!_active || _lineIndex >= Lines.Length)
			return;

		string full = Lines[_lineIndex].Text;
		if (_revealed < full.Length)
		{
			_revealed = full.Length; // first click completes the line
			_textLabel.VisibleCharacters = full.Length;
			return;
		}

		_lineIndex++;
		if (_lineIndex >= Lines.Length)
			Close();
		else
			ShowLine();
	}

	private void ShowLine()
	{
		(string speaker, string text) = Lines[_lineIndex];
		_speakerLabel.Text = speaker;
		_speakerLabel.Visible = speaker.Length > 0;
		_textLabel.Text = text;
		_textLabel.VisibleCharacters = 0;
		_revealed = 0.0f;
		_blink = 0.0f;
	}

	private void Close()
	{
		_active = false;
		_dialogueRoot.Visible = false;
	}

	// --- generated UI ------------------------------------------------------

	private void BuildBackdrop()
	{
		var layer = new CanvasLayer { Name = "Backdrop", Layer = -10 };
		AddChild(layer);

		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.78f, 0.83f, 0.87f)); // pale harbour sky
		gradient.SetColor(1, new Color(0.62f, 0.67f, 0.71f));

		var fill = new GradientTexture2D
		{
			Gradient = gradient,
			FillFrom = new Vector2(0.0f, 0.0f),
			FillTo = new Vector2(0.0f, 1.0f),
		};

		var rect = new TextureRect
		{
			Texture = fill,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(rect);
	}

	private static StyleBoxFlat PanelStyle()
	{
		var box = new StyleBoxFlat
		{
			BgColor = new Color(0.086f, 0.110f, 0.149f, 0.93f),
			BorderColor = new Color(0.560f, 0.630f, 0.710f, 0.55f),
			ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.35f),
			ShadowSize = 12,
		};
		box.SetBorderWidthAll(2);
		box.SetCornerRadiusAll(10);
		box.SetContentMarginAll(22);
		return box;
	}

	private void BuildDialogueUi()
	{
		var layer = new CanvasLayer { Name = "DialogueLayer", Layer = 10 };
		AddChild(layer);

		_dialogueRoot = new MarginContainer
		{
			Name = "DialogueRoot",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		_dialogueRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_dialogueRoot.AddThemeConstantOverride("margin_left", 44);
		_dialogueRoot.AddThemeConstantOverride("margin_right", 44);
		_dialogueRoot.AddThemeConstantOverride("margin_bottom", 36);
		layer.AddChild(_dialogueRoot);

		// push the panel to the bottom of the screen
		var bottom = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_dialogueRoot.AddChild(bottom);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0.0f, 180.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", PanelStyle());
		bottom.AddChild(panel);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		_speakerLabel = new Label();
		_speakerLabel.AddThemeFontSizeOverride("font_size", 22);
		_speakerLabel.AddThemeColorOverride("font_color", new Color(0.949f, 0.706f, 0.298f));
		column.AddChild(_speakerLabel);

		_textLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_textLabel.AddThemeFontSizeOverride("font_size", 24);
		_textLabel.AddThemeColorOverride("font_color", new Color(0.925f, 0.937f, 0.949f));
		column.AddChild(_textLabel);

		_continueHint = new Label
		{
			Text = "▼",
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		_continueHint.AddThemeFontSizeOverride("font_size", 18);
		_continueHint.AddThemeColorOverride("font_color", new Color(0.741f, 0.796f, 0.847f));
		column.AddChild(_continueHint);
	}
}
