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

	[Export] public string Expression { get; set; } = "";

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
		var tex = string.IsNullOrEmpty(_characterId) ? null : CharacterArt.Load(_characterId);
		_sprite.Texture = tex;
		_sprite.FlipH = _flip;
		float width = tex == null ? _height * 0.4f : _height * tex.GetWidth() / tex.GetHeight();

		Size = new Vector2(width, _height);
		PivotOffset = new Vector2(width / 2f, _height);
		_sprite.Position = Vector2.Zero;
		_sprite.Size = Size;
		// In the scene the people sit back in the light of the location.
		_sprite.SelfModulate = new Color(0.86f, 0.84f, 0.82f);
	}

	public void Refresh()
	{
		var state = GameState.Instance;
		Visible = state.Check(ShowIf) && (string.IsNullOrEmpty(HideIf) || !state.Check(HideIf));
	}

	private void SetHover(bool on)
	{
		_hover = on && Interactive && !string.IsNullOrEmpty(Dialogue);
		var tween = CreateTween();
		tween.TweenProperty(_sprite, "self_modulate", _hover ? new Color(1.08f, 1.06f, 1.0f) : new Color(0.86f, 0.84f, 0.82f), 0.15f);
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
