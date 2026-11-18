using Godot;

/// <summary>
/// Someone standing in a location. Click them to run their dialogue script.
/// </summary>
/// <remarks>
/// <see cref="Height"/> is how tall they stand on screen (smaller = further away); the box
/// sizes itself to the sprite, so just drag it to where they should stand. Visible in the
/// editor for placement.
/// </remarks>
[Tool]
[GlobalClass]
public partial class CharacterSpot : Control
{
	private string _characterId = "";
	private float _height = 520f;
	private bool _flip;

	[Export]
	public string CharacterId
	{
		get => _characterId;
		set { _characterId = value; Rebuild(); }
	}

	/// <summary>Which sprite to stand with: assets/art/characters/&lt;id&gt;_&lt;expression&gt;.png
	/// (e.g. the fisherman's "blindfold"). Empty for the plain one.</summary>
	[Export]
	public string Expression
	{
		get => _expression;
		set { _expression = value; Rebuild(); }
	}
	private string _expression = "";

	/// <summary>Dialogue script id, e.g. "harbor/fisherman" for dialogs/&lt;lang&gt;/harbor/fisherman.txt.</summary>
	[Export] public string Dialogue { get; set; } = "";

	/// <summary>Only here while this holds (e.g. "!tavern_keeper_dead").</summary>
	[Export] public string ShowIf { get; set; } = "";

	/// <summary>Gone while this holds.</summary>
	[Export] public string HideIf { get; set; } = "";

	[Export(PropertyHint.Range, "40,1100")]
	public float Height
	{
		get => _height;
		set { _height = value; Rebuild(); }
	}

	[Export]
	public bool Flip
	{
		get => _flip;
		set { _flip = value; Rebuild(); }
	}

	/// <summary>A soft patch of shade on the ground under their feet, so they stand in the
	/// picture rather than on it.</summary>
	[Export]
	public bool Shadow
	{
		get => _shadow;
		set { _shadow = value; QueueRedraw(); }
	}
	private bool _shadow;

	public bool Interactive { get; set; } = true;

	private TextureRect _sprite;
	private Label _name;
	private bool _hover;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		Rebuild();
		if (Engine.IsEditorHint())
			return;
		MouseEntered += () => SetHover(true);
		MouseExited += () => SetHover(false);
		GuiInput += OnGuiInput;
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;
		_sprite ??= GetNodeOrNull<TextureRect>("Sprite");
		if (_sprite == null)
		{
			_sprite = new TextureRect
			{
				Name = "Sprite",
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			AddChild(_sprite, false, InternalMode.Front);
		}
		var tex = string.IsNullOrEmpty(_characterId) ? null : CharacterArt.Load(_characterId, _expression);
		_sprite.Texture = tex;
		_sprite.FlipH = _flip;
		float width = tex == null ? _height * 0.4f : _height * tex.GetWidth() / tex.GetHeight();

		Size = new Vector2(width, _height);
		PivotOffset = new Vector2(width / 2f, _height);
		_sprite.Position = Vector2.Zero;
		_sprite.Size = Size;
		// In the scene the people sit back in the light of the location.
		_sprite.SelfModulate = BaseTint;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!_shadow)
			return;
		// Layered ovals under the feet, darkest in the middle; drawn behind the sprite.
		var centre = new Vector2(Size.X / 2f, Size.Y - _height * 0.025f);
		var radius = new Vector2(Mathf.Max(Size.X * 0.62f, _height * 0.17f), _height * 0.05f);
		for (int i = 10; i >= 1; i--)
		{
			float k = i / 10f;
			var points = new Vector2[40];
			for (int p = 0; p < points.Length; p++)
			{
				float a = Mathf.Tau * p / points.Length;
				points[p] = centre + new Vector2(Mathf.Cos(a) * radius.X, Mathf.Sin(a) * radius.Y) * k;
			}
			DrawColoredPolygon(points, new Color(0f, 0f, 0.02f, 0.15f));
		}
	}

	// Back in the location's light; at night, in moonlight (unless painted by night already).
	private Color BaseTint => Engine.IsEditorHint() || !Night.IsNight ? new Color(0.86f, 0.84f, 0.82f)
		: CharacterArt.IsNightLit(_characterId) ? new Color(0.8f, 0.82f, 0.88f) : Night.Figure;
	private Color HoverTint => Engine.IsEditorHint() || !Night.IsNight ? new Color(1.08f, 1.06f, 1.0f)
		: CharacterArt.IsNightLit(_characterId) ? new Color(1.05f, 1.05f, 1.08f) : Night.Figure * 1.45f;

	public void Refresh()
	{
		var state = GameState.Instance;
		Visible = state.Check(ShowIf) && (string.IsNullOrEmpty(HideIf) || !state.Check(HideIf));
		if (!_hover && _sprite != null)
			_sprite.SelfModulate = BaseTint;
	}

	private void SetHover(bool on)
	{
		_hover = on && Interactive && !string.IsNullOrEmpty(Dialogue);
		var tween = CreateTween();
		tween.TweenProperty(_sprite, "self_modulate", _hover ? HoverTint : BaseTint, 0.15f);
		MouseDefaultCursorShape = _hover ? CursorShape.PointingHand : CursorShape.Arrow;

		if (_hover)
		{
			_name ??= MakeNameLabel();
			_name.Text = Tr($"char_{CharacterId}");
			_name.Show();
		}
		else
			_name?.Hide();
	}

	private Label MakeNameLabel()
	{
		var label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(-100, -54),
			Size = new Vector2(Size.X + 200, 50),
		};
		label.AddThemeFontSizeOverride("font_size", 34);
		label.AddThemeColorOverride("font_color", new Color(0.97f, 0.87f, 0.62f));
		AddChild(label);
		return label;
	}

	private void OnGuiInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Interactive && !string.IsNullOrEmpty(Dialogue))
		{
			SetHover(false);
			Game.Instance.RunDialogue(Dialogue);
			AcceptEvent();
		}
	}
}
