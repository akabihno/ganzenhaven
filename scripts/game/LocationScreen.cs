using Godot;
using System.Collections.Generic;

/// <summary>
/// One location (Harbor, Tavern, …): a background, the people standing around in it
/// (<see cref="CharacterSpot"/>), clickable things (<see cref="Hotspot"/>) and a Map button.
/// Also the stage that dialogue scripts draw on.
/// </summary>
/// <remarks>
/// <para>Make a location by adding a scene at scenes/game/locations/&lt;id&gt;.tscn with this
/// script on the root, then drop CharacterSpot and Hotspot nodes in and place them over the
/// background in the 2D editor. The editor shows the background and the sprites, so
/// placement is WYSIWYG at 1920×1080.</para>
/// <para>The background is assets/art/backgrounds/&lt;BackgroundId&gt;.png; in the second act
/// (after the Tavern Keeper's death) &lt;BackgroundId&gt;_after.png is used when it exists.</para>
/// </remarks>
[Tool]
[GlobalClass]
public partial class LocationScreen : Control, IStage
{
	[Export] public string LocationId { get; set; } = "";

	/// <summary>Background image id; empty = same as <see cref="LocationId"/>.</summary>
	[Export] public string BackgroundId { get; set; } = "";

	/// <summary>Script run on arrival while <see cref="EnterScriptIf"/> holds (e.g. an intro).</summary>
	[Export] public string EnterScript { get; set; } = "";
	[Export] public string EnterScriptIf { get; set; } = "";

	/// <summary>The Map button is shown while this holds.</summary>
	[Export] public string MapButtonIf { get; set; } = "map_unlocked";

	private TextureRect _background;
	private Control _sprites;
	private Button _mapButton;
	private readonly Dictionary<string, TextureRect> _shown = new();

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		_background = GetNodeOrNull<TextureRect>("Background");
		if (_background == null)
		{
			_background = new TextureRect { Name = "Background" };
			AddChild(_background);
			MoveChild(_background, 0);
		}
		_background.SetAnchorsPreset(LayoutPreset.FullRect);
		_background.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		_background.MouseFilter = MouseFilterEnum.Ignore;
		SetBackground(string.IsNullOrEmpty(BackgroundId) ? LocationId : BackgroundId);

		if (Engine.IsEditorHint())
			return;

		_sprites = new Control { Name = "DialogueSprites", MouseFilter = MouseFilterEnum.Ignore };
		_sprites.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_sprites);

		BuildHud();
		Game.Instance.RegisterStage(this);
		GameState.Instance.Changed += Refresh;
		GameState.Instance.LanguageChanged += OnLanguageChanged;
		Refresh();

		// Back from a cutscene in the middle of a conversation: stay in dialogue mode.
		if (Game.Instance.Dialogue.IsActive)
			OnDialogueStarted();
		else if (!string.IsNullOrEmpty(EnterScript) && GameState.Instance.Check(EnterScriptIf))
			CallDeferred(MethodName.RunEnterScript);
	}

	public override void _ExitTree()
	{
		if (Engine.IsEditorHint())
			return;
		Game.Instance?.UnregisterStage(this);
		if (GameState.Instance != null)
		{
			GameState.Instance.Changed -= Refresh;
			GameState.Instance.LanguageChanged -= OnLanguageChanged;
		}
	}

	private void RunEnterScript() => Game.Instance.RunDialogue(EnterScript);

	private void OnLanguageChanged(string language) => Refresh();

	/// <summary>Re-applies ShowIf / HideIf on everything after the story state changed.</summary>
	private void Refresh()
	{
		foreach (var node in FindChildren("*", "", true, false))
		{
			if (node is CharacterSpot spot) spot.Refresh();
			else if (node is Hotspot hotspot) hotspot.Refresh();
		}
		if (_mapButton != null)
		{
			_mapButton.Visible = GameState.Instance.Check(MapButtonIf) && !Game.Instance.Dialogue.IsActive;
			_mapButton.Text = Tr("btn_map");
		}
	}

	private void BuildHud()
	{
		_mapButton = new Button { Name = "MapButton", Text = Tr("btn_map"), CustomMinimumSize = new Vector2(200, 64) };
		_mapButton.SetAnchorsPreset(LayoutPreset.TopRight);
		// Left of the Journal / Menu buttons that GameHud puts on every screen.
		_mapButton.OffsetLeft = GameHud.ScreenButtonRight - 200;
		_mapButton.OffsetRight = GameHud.ScreenButtonRight;
		_mapButton.OffsetTop = 32;
		_mapButton.OffsetBottom = 96;
		_mapButton.AddThemeFontSizeOverride("font_size", 34);
		_mapButton.Pressed += () => Game.Instance.GoToMap();
		AddChild(_mapButton);

		var title = new Label { Name = "LocationTitle", Text = Tr($"loc_{LocationId}") };
		title.Position = new Vector2(48, 28);
		title.AddThemeFontSizeOverride("font_size", 46);
		title.AddThemeColorOverride("font_color", new Color(0.93f, 0.85f, 0.68f));
		AddChild(title);
	}

	// ---------------------------------------------------------------- IStage

	public void SetBackground(string id)
	{
		if (_background == null || string.IsNullOrEmpty(id))
			return;
		string basePath = $"res://assets/art/backgrounds/{id}";
		string path = !Engine.IsEditorHint() && GameState.Instance.IsAfter && ResourceLoader.Exists($"{basePath}_after.png")
			? $"{basePath}_after.png"
			: $"{basePath}.png";
		if (ResourceLoader.Exists(path))
			_background.Texture = GD.Load<Texture2D>(path);
		else
			GD.PushWarning($"Missing background {path}");
	}

	public void ShowCharacter(string id, string expression, string position)
	{
		if (string.IsNullOrEmpty(id))
			return;
		var texture = CharacterArt.Load(id, expression);
		if (texture == null)
			return;

		if (!_shown.TryGetValue(id, out var sprite))
		{
			sprite = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspect,
				MouseFilter = MouseFilterEnum.Ignore,
				Modulate = new Color(1, 1, 1, 0),
			};
			_sprites.AddChild(sprite);
			_shown[id] = sprite;
			CreateTween().TweenProperty(sprite, "modulate:a", 1f, 0.25f);
		}
		sprite.Texture = texture;

		// Dialogue sprites stand large and up close.
		Vector2 screen = GetViewportRect().Size;
		float height = screen.Y * CharacterArt.DialogueScale(id);
		float width = height * texture.GetWidth() / texture.GetHeight();
		float x = ParsePosition(position) * screen.X;
		sprite.Size = new Vector2(width, height);
		sprite.Position = new Vector2(x - width / 2f, screen.Y * CharacterArt.DialogueBaseline(id) - height);

		// Someone standing in the scene steps "forward" into the dialogue.
		foreach (var node in FindChildren("*", "", true, false))
			if (node is CharacterSpot spot && spot.CharacterId == id)
				spot.Visible = false;
	}

	public void HideCharacter(string id)
	{
		var ids = id == "all" || string.IsNullOrEmpty(id) ? new List<string>(_shown.Keys) : new List<string> { id };
		foreach (var key in ids)
		{
			if (!_shown.Remove(key, out var sprite))
				continue;
			var tween = CreateTween();
			tween.TweenProperty(sprite, "modulate:a", 0f, 0.2f);
			tween.TweenCallback(Callable.From(sprite.QueueFree));
		}
	}

	public void OnDialogueStarted()
	{
		if (_mapButton != null) _mapButton.Visible = false;
		SetInteractive(false);
	}

	public void OnDialogueEnded()
	{
		HideCharacter("all");
		SetInteractive(true);
		Refresh();
	}

	private void SetInteractive(bool on)
	{
		foreach (var node in FindChildren("*", "", true, false))
		{
			if (node is CharacterSpot spot) spot.Interactive = on;
			else if (node is Hotspot hotspot) hotspot.Interactive = on;
		}
	}

	private static float ParsePosition(string position) => position switch
	{
		"left" => 0.26f,
		"right" => 0.74f,
		"farleft" => 0.12f,
		"farright" => 0.88f,
		"" or "center" => 0.5f,
		_ => float.TryParse(position, System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out var f) ? Mathf.Clamp(f, 0f, 1f) : 0.5f,
	};
}
