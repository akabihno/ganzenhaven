using Godot;

/// <summary>
/// A clickable thing in a location (the crane, the hollow in the oak, the ledger…).
/// Size the rectangle over the object in the editor; hovering shows a soft glow and the
/// label, clicking runs <see cref="Dialogue"/>.
/// </summary>
/// <remarks>
/// Something the player can take away is cut out of the background into <see cref="Picture"/>
/// (drawn to fill the rectangle) with the gap in the background painted over, and the hotspot
/// gets <c>HideIf = item:&lt;id&gt;</c>: once picked up, it's gone from the scene.
/// </remarks>
[Tool]
[GlobalClass]
public partial class Hotspot : Control
{
	/// <summary>Translation key (translations/ui.csv) or plain text for the hover label.</summary>
	[Export] public string LabelKey { get; set; } = "";

	/// <summary>Dialogue script id, e.g. "harbor/crane".</summary>
	[Export] public string Dialogue { get; set; } = "";

	/// <summary>Instead of a dialogue, opens a book: "ledger", "journal" or "inventory".</summary>
	[Export] public string Opens { get; set; } = "";

	private bool Clickable => Interactive && (!string.IsNullOrEmpty(Dialogue) || !string.IsNullOrEmpty(Opens));

	/// <summary>Optional picture drawn over the background, filling the rectangle.</summary>
	[Export] public Texture2D Picture
	{
		get => _picture;
		set { _picture = value; QueueRedraw(); }
	}
	private Texture2D _picture;

	[Export] public string ShowIf { get; set; } = "";
	[Export] public string HideIf { get; set; } = "";

	public bool Interactive { get; set; } = true;

	private float _glow;
	private float _target;
	private Label _label;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		if (Engine.IsEditorHint())
			return;
		MouseEntered += () => SetHover(true);
		MouseExited += () => SetHover(false);
		GuiInput += OnGuiInput;
	}

	public void Refresh()
	{
		var state = GameState.Instance;
		Visible = state.Check(ShowIf) && (string.IsNullOrEmpty(HideIf) || !state.Check(HideIf));
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float before = _glow;
		_glow = Mathf.MoveToward(_glow, _target, (float)delta * 5f);
		if (!Mathf.IsEqualApprox(before, _glow))
			QueueRedraw();
	}

	public override void _Draw()
	{
		if (_picture != null)
		{
			// At night: its night picture if it has one (cut from the night background), or moonlit.
			bool night = !Engine.IsEditorHint() && Night.IsNight;
			var texture = night ? Night.Variant(_picture, out bool own) : _picture;
			bool tint = night && texture == _picture;
			DrawTextureRect(texture, new Rect2(Vector2.Zero, Size), false, tint ? Night.Prop : Colors.White);
		}
		if (Engine.IsEditorHint())
		{
			// Placement guide, editor only.
			DrawRect(new Rect2(Vector2.Zero, Size), new Color(1f, 0.8f, 0.3f, 0.12f));
			DrawRect(new Rect2(Vector2.Zero, Size), new Color(1f, 0.8f, 0.3f, 0.8f), false, 2f);
			return;
		}
		if (_glow <= 0.01f)
			return;
		// A soft oval of light: concentric ellipses fading outward.
		Vector2 c = Size / 2f;
		for (int i = 12; i >= 1; i--)
		{
			float k = i / 12f;
			var points = new Vector2[40];
			for (int p = 0; p < points.Length; p++)
			{
				float a = Mathf.Tau * p / points.Length;
				points[p] = c + new Vector2(Mathf.Cos(a) * c.X * k, Mathf.Sin(a) * c.Y * k);
			}
			DrawColoredPolygon(points, new Color(1f, 0.9f, 0.65f, 0.035f * _glow));
		}
	}

	private void SetHover(bool on)
	{
		bool active = on && Clickable;
		_target = active ? 1f : 0f;
		MouseDefaultCursorShape = active ? CursorShape.PointingHand : CursorShape.Arrow;
		if (active)
		{
			if (_label == null)
			{
				_label = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
				_label.AddThemeFontSizeOverride("font_size", 34);
				_label.AddThemeColorOverride("font_color", new Color(0.97f, 0.87f, 0.62f));
				AddChild(_label);
			}
			_label.Text = Tr(LabelKey);
			_label.Size = new Vector2(Size.X + 300, 50);
			_label.Position = new Vector2(-150, Size.Y / 2f - 25);
			_label.Show();
		}
		else
			_label?.Hide();
	}

	private void OnGuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Clickable)
		{
			SetHover(false);
			AcceptEvent();
			switch (Opens)
			{
				case "ledger": Game.Instance.Hud.OpenLedger(); break;
				case "journal": Game.Instance.Hud.OpenJournal(); break;
				case "inventory": Game.Instance.Hud.OpenInventory(); break;
				default: Game.Instance.RunDialogue(Dialogue); break;
			}
		}
	}
}
