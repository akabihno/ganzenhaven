using Godot;

/// <summary>
/// A place on the town map. Glows softly; on hover it brightens and shows its name; click
/// to travel. Drag it over the drawing of the place in the editor.
/// </summary>
[Tool]
[GlobalClass]
public partial class MapMarker : Control
{
	[Export] public string LocationId { get; set; } = "";

	/// <summary>Shown on the map while this holds (empty = always).</summary>
	[Export] public string ShowIf { get; set; } = "";
	[Export] public string HideIf { get; set; } = "";

	/// <summary>Pulses to draw the eye while this holds (something new is happening there).</summary>
	[Export] public string AttentionIf { get; set; } = "";

	private const float Radius = 46f;
	private float _hover;
	private float _time;
	private bool _attention;
	private bool _isHere;
	private Label _label;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(Radius * 2, Radius * 2);
		Size = CustomMinimumSize;
		PivotOffset = Size / 2f;
		MouseFilter = MouseFilterEnum.Stop;

		_label = GetNodeOrNull<Label>("Name") ?? new Label { Name = "Name" };
		if (_label.GetParent() == null)
			AddChild(_label, false, InternalMode.Front);
		_label.HorizontalAlignment = HorizontalAlignment.Center;
		_label.MouseFilter = MouseFilterEnum.Ignore;
		_label.Size = new Vector2(420, 56);
		_label.Position = new Vector2(Radius - 210, Radius * 2 + 2);
		_label.AddThemeFontSizeOverride("font_size", 38);
		_label.AddThemeColorOverride("font_color", new Color(0.18f, 0.1f, 0.04f));
		_label.AddThemeColorOverride("font_shadow_color", new Color(0.98f, 0.93f, 0.8f, 0.9f));
		_label.AddThemeConstantOverride("shadow_outline_size", 10);
		_label.Text = Engine.IsEditorHint() ? LocationId : Tr($"loc_{LocationId}");

		if (Engine.IsEditorHint())
			return;
		MouseEntered += () => _hover = 1f;
		MouseExited += () => _hover = 0f;
		GuiInput += OnGuiInput;
		Refresh();
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
		_time += (float)delta;
		float target = _hover > 0 ? 1.15f : 1f;
		Scale = Scale.Lerp(Vector2.One * target, 1f - Mathf.Exp(-12f * (float)delta));
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 c = Size / 2f;
		float pulse = 0.5f + 0.5f * Mathf.Sin(_time * (_attention ? 5f : 2f));
		var gold = new Color(1f, 0.82f, 0.4f);

		// Halo
		for (int i = 10; i >= 1; i--)
		{
			float k = i / 10f;
			float alpha = (0.05f + 0.06f * _hover + (_attention ? 0.05f * pulse : 0.02f * pulse)) * (1.1f - k);
			DrawCircle(c, Radius * (0.6f + 0.9f * k), new Color(gold, alpha));
		}
		// Ring and seal
		DrawArc(c, Radius * 0.62f, 0, Mathf.Tau, 48, new Color(0.35f, 0.12f, 0.06f, 0.9f), 5f, true);
		DrawCircle(c, Radius * 0.42f, new Color(0.55f, 0.14f, 0.08f, 0.9f));
		DrawCircle(c, Radius * 0.18f, new Color(0.95f, 0.8f, 0.45f, 0.9f));
		if (_isHere)
			DrawArc(c, Radius * 0.82f, 0, Mathf.Tau, 48, new Color(0.2f, 0.1f, 0.05f, 0.8f), 3f, true);
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
