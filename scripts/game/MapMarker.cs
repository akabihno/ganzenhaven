using Godot;

/// <summary>
/// A place on the town map. Size the rectangle over the drawing of the place (the church, the
/// oak, the piers) in the editor. Places you can travel to are lit softly; hovering brightens
/// the light and writes the name below; click to travel. Where you are now is ringed with a
/// quick quill circle, drawn in as the map opens.
/// </summary>
[Tool]
[GlobalClass]
public partial class MapMarker : Control
{
	[Export] public string LocationId { get; set; } = "";

	/// <summary>Shown on the map while this holds (empty = always).</summary>
	[Export] public string ShowIf { get; set; } = "";
	[Export] public string HideIf { get; set; } = "";

	/// <summary>The light pulses to draw the eye while this holds (something new is happening there).</summary>
	[Export] public string AttentionIf { get; set; } = "";

	private static readonly Color Light = new(1f, 0.93f, 0.68f);
	private static readonly Color Ink = new(0.2f, 0.1f, 0.04f);

	private float _hover, _hoverTarget;
	private float _time;
	private float _drawn;   // 0..1: how much of the quill circle is drawn so far
	private bool _attention;
	private bool _isHere;
	private Label _label;
	private Vector2[] _circle;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		_label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = Engine.IsEditorHint(),
		};
		_label.AddThemeFontOverride("font", GD.Load<Font>("res://assets/fonts/MarckScript-Regular.ttf"));
		_label.AddThemeFontSizeOverride("font_size", 46);
		_label.AddThemeColorOverride("font_color", Ink);
		_label.AddThemeColorOverride("font_outline_color", new Color(0.97f, 0.91f, 0.76f, 0.95f));
		_label.AddThemeConstantOverride("outline_size", 12);
		AddChild(_label, false, InternalMode.Front);
		Resized += PlaceLabel;
		PlaceLabel();
		_label.Text = Engine.IsEditorHint() ? LocationId : Tr($"loc_{LocationId}");

		if (Engine.IsEditorHint())
			return;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		MouseEntered += () => { _hoverTarget = 1f; _label.Show(); };
		MouseExited += () => { _hoverTarget = 0f; _label.Hide(); };
		GuiInput += OnGuiInput;
		Refresh();
	}

	private void PlaceLabel()
	{
		_label.Size = new Vector2(Mathf.Max(Size.X + 240, 420), 64);
		_label.Position = new Vector2((Size.X - _label.Size.X) / 2f, Size.Y + 4);
		_circle = null;
	}

	public void Refresh()
	{
		var state = GameState.Instance;
		Visible = state.Check(ShowIf) && (string.IsNullOrEmpty(HideIf) || !state.Check(HideIf));
		_attention = !string.IsNullOrEmpty(AttentionIf) && state.Check(AttentionIf);
		_isHere = state.Location == LocationId;
		_label.Text = Tr($"loc_{LocationId}");
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_hover = Mathf.MoveToward(_hover, _hoverTarget, dt * 6f);
		if (_isHere && _drawn < 1f)
			_drawn = Mathf.Min(1f, _drawn + dt / 0.7f);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Engine.IsEditorHint())
		{
			// Placement guide, editor only.
			DrawRect(new Rect2(Vector2.Zero, Size), new Color(1f, 0.8f, 0.3f, 0.15f));
			DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.6f, 0.3f, 0.1f, 0.9f), false, 2f);
			return;
		}

		// The light: soft layered ovals, as if a candle were held over that part of the map.
		Vector2 c = Size / 2f;
		float pulse = 0.5f + 0.5f * Mathf.Sin(_time * (_attention ? 4.5f : 1.6f));
		float strength = 1f + 1.1f * _hover + (_attention ? 0.6f * pulse : 0.15f * pulse);
		for (int i = 16; i >= 1; i--)
		{
			float k = i / 16f;
			DrawColoredPolygon(Oval(c, c * (0.3f + 0.9f * k), 48), new Color(Light, 0.055f * strength * (1.2f - k)));
		}

		if (_isHere && _drawn > 0f)
			DrawQuillCircle();
	}

	/// <summary>
	/// A loose ink ring around the place: a little lopsided, overrunning its start, thick in
	/// the middle of the stroke and thin where the quill touched down and lifted off.
	/// </summary>
	private void DrawQuillCircle()
	{
		if (_circle == null)
		{
			var rng = new RandomNumberGenerator { Seed = (ulong)GD.Hash(LocationId) };
			float wobble1 = rng.RandfRange(0f, Mathf.Tau), wobble2 = rng.RandfRange(0f, Mathf.Tau);
			float start = rng.RandfRange(0.6f, 1.4f) * Mathf.Pi;   // starts top-left-ish
			const int steps = 90;
			const float sweep = Mathf.Tau * 1.12f;                    // overruns the start a bit
			Vector2 c = Size / 2f, r = Size / 2f * 1.18f + new Vector2(14, 12);
			_circle = new Vector2[steps + 1];
			for (int i = 0; i <= steps; i++)
			{
				float t = (float)i / steps;
				float a = start + sweep * t;
				float s = 1f + 0.05f * Mathf.Sin(a * 2f + wobble1) + 0.025f * Mathf.Sin(a * 5f + wobble2) + 0.06f * t;
				_circle[i] = c + new Vector2(Mathf.Cos(a) * r.X, Mathf.Sin(a) * r.Y) * s;
			}
		}

		int last = Mathf.Max(1, (int)((_circle.Length - 1) * _drawn));
		for (int i = 0; i < last; i++)
		{
			float t = (float)i / (_circle.Length - 1);
			float width = 1.5f + 3.5f * Mathf.Sin(Mathf.Pi * Mathf.Clamp(t * 1.05f, 0f, 1f));
			DrawLine(_circle[i], _circle[i + 1], new Color(Ink, 0.82f), width, true);
		}
	}

	private static Vector2[] Oval(Vector2 centre, Vector2 radius, int points)
	{
		var result = new Vector2[points];
		for (int p = 0; p < points; p++)
		{
			float a = Mathf.Tau * p / points;
			result[p] = centre + new Vector2(Mathf.Cos(a) * radius.X, Mathf.Sin(a) * radius.Y);
		}
		return result;
	}

	private void OnGuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			AcceptEvent();
			Game.Instance.GoToLocation(LocationId);
		}
	}
}
