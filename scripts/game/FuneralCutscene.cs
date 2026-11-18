using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The Tavern Keeper's funeral, the night he dies (<c>@cutscene funeral</c>): four painted shots
/// with slow camera moves and crossfades, drifting fog, flickering lanterns, a swinging bell
/// and falling earth, with the author reading the captions from
/// dialogs/&lt;language&gt;/cutscenes/funeral.txt (one caption per line).
/// </summary>
/// <remarks>
/// Pictures are in assets/art/cutscenes/funeral/ (1376x768; their sources in
/// assets/art/night_src/funeral/). Coordinates below are pixels of those pictures. Sounds
/// that aren't there yet are simply skipped, so they can be added later
/// (see <see cref="Sfx"/>). Esc skips the whole scene (handled by <see cref="Game"/>).
/// </remarks>
public partial class FuneralCutscene : Control
{
	[Signal] public delegate void FinishedEventHandler();

	private const string Art = "res://assets/art/cutscenes/funeral/";
	private static readonly Vector2 Picture = new(1376, 768);
	private static readonly Color LanternLight = new(1f, 0.66f, 0.32f);

	/// <summary>One shot: a picture, where the camera starts and ends (centre in picture pixels,
	/// zoom 1 = the whole picture), and its lanterns (x, y, glow radius).</summary>
	private sealed record Shot(string Image, Vector2 From, float FromZoom, Vector2 To, float ToZoom, Vector3[] Lanterns);

	private static readonly Shot BellShot = new("bell.png", new(688, 430), 1.0f, new(700, 330), 1.3f,
		new Vector3[] { new(768, 592, 46) });

	private static readonly Shot ProcessionShot = new("procession.png", new(560, 420), 1.22f, new(760, 390), 1.04f,
		new Vector3[]
		{
			new(747, 549, 30), new(859, 555, 30), new(973, 544, 28), new(1088, 513, 24), new(1141, 495, 22),
			new(1237, 434, 18), new(1210, 399, 16), new(1130, 372, 14), new(1099, 352, 14), new(987, 292, 12),
			new(997, 226, 26),
		});

	private static readonly Shot GraveShot = new("grave.png", new(680, 400), 1.0f, new(560, 440), 1.22f,
		new Vector3[] { new(340, 463, 40), new(605, 413, 44), new(966, 313, 30) });

	private static readonly Shot EarthShot = new("earth.png", new(690, 420), 1.06f, new(760, 410), 1.24f,
		new Vector3[]
		{
			new(1068, 488, 60), new(1162, 331, 7), new(1166, 340, 7), new(1156, 362, 8), new(1148, 374, 8),
			new(1146, 385, 9), new(1146, 399, 10), new(1123, 407, 11), new(1096, 430, 12), new(1058, 428, 12),
			new(1256, 411, 11), new(597, 433, 10), new(638, 451, 10),
		});

	// Sound effects, played on the Sfx bus when the file exists.
	private static class Sfx
	{
		public const string Bell = "res://audio/sfx/funeral_bell.ogg";
		public const string Wind = "res://audio/sfx/night_wind_loop.ogg";
		public const string WindFallback = "res://audio/sfx/harbour_fog_loop.ogg";
		public const string Steps = "res://audio/sfx/procession_steps.ogg";
		public const string Ropes = "res://audio/sfx/coffin_ropes.ogg";
		public const string Shovel = "res://audio/sfx/shovel_earth.ogg";
		public const string Fox = "res://audio/sfx/fox_bark.ogg";
	}

	private readonly List<string> _captions = new();
	private Control _stage;
	private Control _current;
	private RichTextLabel _caption;
	private AudioStreamPlayer _voice;
	private AudioStreamPlayer _wind;
	private Sprite2D _bell;
	private float _bellSwing;      // amplitude, radians; each toll sets it, then it dies away
	private float _bellPhase;
	private SoilFall _soil;
	private bool _ended;
	private float _time;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;

		var black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
		black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(black);

		_stage = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_stage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_stage);

		var fog = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/cutscene_fog.gdshader") } };
		fog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(fog);

		// A little shade along the bottom, so the captions read over bright fog.
		var shade = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Gradient = new Gradient { Colors = new[] { new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.55f) } },
				FillFrom = new Vector2(0, 0),
				FillTo = new Vector2(0, 1),
				Width = 4,
				Height = 64,
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		shade.OffsetTop = -300;
		AddChild(shade);

		_caption = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0),
		};
		_caption.AddThemeFontOverride("normal_font", GD.Load<Font>("res://assets/fonts/EBGaramond-Italic.ttf"));
		_caption.AddThemeFontSizeOverride("normal_font_size", 40);
		_caption.AddThemeColorOverride("default_color", new Color(0.95f, 0.91f, 0.82f));
		_caption.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
		_caption.AddThemeConstantOverride("shadow_offset_x", 0);
		_caption.AddThemeConstantOverride("shadow_offset_y", 2);
		_caption.AddThemeConstantOverride("shadow_outline_size", 10);
		_caption.SetAnchorsPreset(LayoutPreset.CenterBottom);
		_caption.OffsetLeft = -860;
		_caption.OffsetRight = 860;
		_caption.OffsetTop = -190;
		_caption.OffsetBottom = -70;
		_caption.GrowHorizontal = GrowDirection.Both;
		AddChild(_caption);

		_voice = new AudioStreamPlayer { Bus = "Voice" };
		_voice.Finished += () => Game.Instance?.Music.Duck(false);
		AddChild(_voice);

		LoadCaptions();
		Run();
	}

	public override void _ExitTree()
	{
		_ended = true;
		Game.Instance?.Music.Duck(false);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		if (_bell != null)
		{
			_bellPhase += dt * Mathf.Tau / 2.6f;
			_bellSwing = Mathf.MoveToward(_bellSwing, 0f, dt * 0.012f);
			_bell.Rotation = Mathf.Sin(_bellPhase) * _bellSwing;
		}
	}

	// ---------------------------------------------------------------- the scene

	private async void Run()
	{
		Game.Instance?.Music.Play("graveyard");
		_wind = Loop(ResourceLoader.Exists(Sfx.Wind) ? Sfx.Wind : Sfx.WindFallback, -10f);

		// 1. The bell tower. The knell starts the scene and tolls on into the next shot.
		float d1 = ShotLength(0, 11f);
		Cut(BellShot, d1 + 1.6f);
		BuildBell();
		Knell(d1 + 6f);
		if (!await Wait(1.4f)) return;
		if (!await Say(0)) return;
		if (!await Wait(d1 - 1.4f - VoiceLength(0))) return;

		// 2. The procession up the hill.
		float d2 = ShotLength(1, 10f);
		Cut(ProcessionShot, d2 + 1.6f);
		var steps = Loop(Sfx.Steps, -14f);
		if (!await Wait(1.2f)) return;
		if (!await Say(1)) return;
		if (!await Wait(d2 - 1.2f - VoiceLength(1))) return;
		FadeOut(steps, 1.5f);

		// 3. The grave.
		float d3 = ShotLength(2, 10f);
		Cut(GraveShot, d3 + 1.6f);
		if (!await Wait(1.0f)) return;
		if (!await Say(2)) return;
		PlayOnce(Sfx.Ropes, -8f);
		if (!await Wait(d3 - 1.0f - VoiceLength(2))) return;

		// 4. The gravedigger alone; the fox.
		float d4 = ShotLength(3, 9f) + VoiceLength(4) + 3.5f;
		Cut(EarthShot, d4 + 1f);
		_soil = new SoilFall { From = new Vector2(700, 512) };
		_current.AddChild(_soil);
		ShovelLoop(d4);
		if (!await Wait(1.2f)) return;
		if (!await Say(3)) return;
		if (!await Wait(1.6f)) return;
		PlayOnce(Sfx.Fox, -6f);
		if (!await Wait(0.8f)) return;
		if (!await Say(4)) return;
		if (!await Wait(2.8f)) return;

		HideCaption();
		FadeOut(_wind, 1.4f);
		if (!await Wait(1.0f)) return;
		_ended = true;
		EmitSignal(SignalName.Finished);
	}

	/// <summary>Seconds a shot lasts: its caption read out with some room around it, but at
	/// least <paramref name="least"/>.</summary>
	private float ShotLength(int caption, float least) => Mathf.Max(least, VoiceLength(caption) + 3.4f);

	private async Task<bool> Wait(float seconds)
	{
		if (seconds > 0f)
			await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return !_ended && IsInsideTree();
	}

	// ---------------------------------------------------------------- pictures

	/// <summary>Brings in a shot over the last one and moves the camera across it.</summary>
	private void Cut(Shot shot, float seconds)
	{
		var previous = _current;
		var plate = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = Picture, Modulate = new Color(1, 1, 1, 0) };
		plate.AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>(Art + shot.Image),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Size = Picture,
			MouseFilter = MouseFilterEnum.Ignore,
		});
		foreach (var lantern in shot.Lanterns)
			plate.AddChild(new LanternGlow { Position = new Vector2(lantern.X, lantern.Y), Radius = lantern.Z, Tint = LanternLight });
		_stage.AddChild(plate);
		_current = plate;

		Place(plate, shot.From, shot.FromZoom);
		var move = CreateTween().SetParallel();
		move.TweenMethod(Callable.From<float>(k =>
		{
			float e = 0.5f - 0.5f * Mathf.Cos(Mathf.Pi * k);
			Place(plate, shot.From.Lerp(shot.To, e), Mathf.Lerp(shot.FromZoom, shot.ToZoom, e));
		}), 0f, 1f, seconds);
		move.TweenProperty(plate, "modulate:a", 1f, previous == null ? 0.8f : 1.6f);
		if (previous != null)
			move.Chain().TweenCallback(Callable.From(() =>
			{
				if (previous == _bell?.GetParent())
					_bell = null;
				previous.QueueFree();
			}));
	}

	/// <summary>Scales and moves a picture so <paramref name="centre"/> is in the middle of the
	/// screen; never so far that an edge of the picture shows.</summary>
	private void Place(Control plate, Vector2 centre, float zoom)
	{
		Vector2 screen = Size == Vector2.Zero ? GetViewportRect().Size : Size;
		float scale = Mathf.Max(screen.X / Picture.X, screen.Y / Picture.Y) * Mathf.Max(1f, zoom);
		Vector2 half = screen / 2f / scale;
		centre = new Vector2(Mathf.Clamp(centre.X, half.X, Picture.X - half.X), Mathf.Clamp(centre.Y, half.Y, Picture.Y - half.Y));
		plate.Scale = Vector2.One * scale;
		plate.Position = screen / 2f - centre * scale;
	}

	/// <summary>The bell in the tower, cut out of the picture so it can swing.</summary>
	private void BuildBell()
	{
		// bell_swing.png is the bell from (658, 192) to (729, 270); it hangs from its yoke at (693, 204).
		var pivot = new Vector2(693, 204);
		_bell = new Sprite2D
		{
			Texture = GD.Load<Texture2D>(Art + "bell_swing.png"),
			Centered = false,
			Position = pivot,
			Offset = new Vector2(658, 192) - pivot,
		};
		_current.AddChild(_bell);
	}

	/// <summary>Tolls the bell every few seconds for <paramref name="seconds"/>, quieter as it goes on.</summary>
	private async void Knell(float seconds)
	{
		if (!await Wait(0.4f))
			return;
		for (float t = 0f; ; t += 5.6f)
		{
			PlayOnce(Sfx.Bell, t < 10f ? -2f : -7f);
			_bellSwing = 0.13f;
			_bellPhase = 0f;
			if (t + 5.6f >= seconds || !await Wait(5.6f))
				return;
		}
	}

	private async void ShovelLoop(float seconds)
	{
		if (!await Wait(0.8f))
			return;
		var shovel = ResourceLoader.Exists(Sfx.Shovel) ? Loop(Sfx.Shovel, -10f) : null;
		for (float t = 0.8f; t < seconds - 2f; t += 3.4f)
		{
			_soil?.Throw();
			if (!await Wait(3.4f))
				return;
		}
		FadeOut(shovel, 1.2f);
	}

	// ---------------------------------------------------------------- captions and voice

	private void LoadCaptions()
	{
		string language = GameState.Instance?.Language ?? "en";
		string path = $"res://dialogs/{language}/cutscenes/funeral.txt";
		if (!FileAccess.FileExists(path))
			path = "res://dialogs/en/cutscenes/funeral.txt";
		if (!FileAccess.FileExists(path))
			return;
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		foreach (var raw in file.GetAsText().TrimStart('﻿').Split('\n'))
		{
			string line = raw.Trim();
			if (line.Length > 0 && !line.StartsWith('#'))
				_captions.Add(line);
		}
		_language = language == "en" || FileAccess.FileExists($"res://dialogs/{language}/cutscenes/funeral.txt") ? language : "en";
	}

	private string _language = "en";

	private string VoicePath(int i) => i < _captions.Count
		? $"res://audio/voice/{_language}/cutscenes/funeral/{DialogueRunner.VoiceKey("", _captions[i])}.mp3"
		: "";

	private readonly Dictionary<int, AudioStream> _voices = new();

	private AudioStream VoiceOf(int i)
	{
		if (_voices.TryGetValue(i, out var stream))
			return stream;
		string path = VoicePath(i);
		stream = Settings.VoiceOn && path.Length > 0 && ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
		_voices[i] = stream;
		return stream;
	}

	/// <summary>How long caption <paramref name="i"/> takes: its recording, or reading time.</summary>
	private float VoiceLength(int i)
	{
		if (VoiceOf(i) is { } stream)
			return (float)stream.GetLength();
		return i < _captions.Count ? Mathf.Clamp(_captions[i].Length * 0.06f, 2.5f, 7f) : 0f;
	}

	/// <summary>Shows caption <paramref name="i"/> and reads it out; returns once it has been read.</summary>
	private async Task<bool> Say(int i)
	{
		if (i >= _captions.Count)
			return !_ended;
		_caption.Text = $"[center]{_captions[i]}[/center]";
		var tween = CreateTween();
		tween.TweenProperty(_caption, "modulate:a", 1f, 0.6f);
		if (VoiceOf(i) is { } stream)
		{
			_voice.Stream = stream;
			_voice.Play();
			Game.Instance?.Music.Duck(true);
		}
		if (!await Wait(VoiceLength(i) + 0.4f))
			return false;
		HideCaption();
		return true;
	}

	private void HideCaption() => CreateTween().TweenProperty(_caption, "modulate:a", 0f, 0.8f);

	// ---------------------------------------------------------------- sounds

	private AudioStreamPlayer PlayOnce(string path, float db)
	{
		if (!ResourceLoader.Exists(path))
			return null;
		var player = new AudioStreamPlayer { Bus = "Sfx", Stream = GD.Load<AudioStream>(path), VolumeDb = db };
		AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
		return player;
	}

	private AudioStreamPlayer Loop(string path, float db)
	{
		if (!ResourceLoader.Exists(path))
			return null;
		var stream = GD.Load<AudioStream>(path);
		if (stream is AudioStreamOggVorbis ogg)
			ogg.Loop = true;
		else if (stream is AudioStreamMP3 mp3)
			mp3.Loop = true;
		var player = new AudioStreamPlayer { Bus = "Sfx", Stream = stream, VolumeDb = -40f };
		AddChild(player);
		player.Play();
		CreateTween().TweenProperty(player, "volume_db", db, 1.5f);
		return player;
	}

	private void FadeOut(AudioStreamPlayer player, float seconds)
	{
		if (player == null || !IsInstanceValid(player))
			return;
		var tween = CreateTween();
		tween.TweenProperty(player, "volume_db", -60f, seconds);
		tween.TweenCallback(Callable.From(player.QueueFree));
	}

	// ---------------------------------------------------------------- little moving things

	/// <summary>The warm light of a lantern: a soft glow that breathes and flickers.</summary>
	private sealed partial class LanternGlow : Node2D
	{
		public float Radius = 30f;
		public Color Tint = Colors.Orange;
		private float _t, _seed;
		private static Texture2D _glow;

		public override void _Ready()
		{
			_seed = GD.Randf() * 100f;
			_glow ??= MakeGlow();
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		}

		public override void _Process(double delta)
		{
			_t += (float)delta;
			QueueRedraw();
		}

		public override void _Draw()
		{
			float flicker = 0.78f + 0.12f * Mathf.Sin(_t * 7.3f + _seed) + 0.08f * Mathf.Sin(_t * 17.1f + _seed * 2f)
				+ 0.06f * Mathf.Sin(_t * 2.1f + _seed * 3f);
			float r = Radius * (0.96f + 0.06f * flicker);
			DrawTextureRect(_glow, new Rect2(-r * 2f, -r * 2f, r * 4f, r * 4f), false, new Color(Tint, 0.42f * flicker));
			DrawTextureRect(_glow, new Rect2(-r * 0.6f, -r * 0.6f, r * 1.2f, r * 1.2f), false, new Color(1f, 0.85f, 0.6f, 0.35f * flicker));
		}

		private static Texture2D MakeGlow()
		{
			var gradient = new Gradient();
			gradient.SetColor(0, Colors.White);
			gradient.SetColor(1, new Color(1, 1, 1, 0));
			gradient.AddPoint(0.25f, new Color(1, 1, 1, 0.45f));
			gradient.AddPoint(0.6f, new Color(1, 1, 1, 0.08f));
			return new GradientTexture2D
			{
				Gradient = gradient,
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f),
				FillTo = new Vector2(1f, 0.5f),
				Width = 128,
				Height = 128,
			};
		}
	}

	/// <summary>Clods of wet earth tipping off the gravedigger's spade.</summary>
	private sealed partial class SoilFall : Node2D
	{
		public Vector2 From;
		private readonly List<(Vector2 Pos, Vector2 Vel, float Size, float Life)> _clods = new();

		public void Throw()
		{
			for (int i = 0; i < 26; i++)
			{
				var pos = From + new Vector2((float)GD.RandRange(-14.0, 6.0), (float)GD.RandRange(-3.0, 3.0));
				var vel = new Vector2((float)GD.RandRange(-38.0, -8.0), (float)GD.RandRange(-30.0, 6.0));
				_clods.Add((pos, vel, (float)GD.RandRange(1.0, 3.0), (float)GD.RandRange(0.0, 0.5)));
			}
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			for (int i = _clods.Count - 1; i >= 0; i--)
			{
				var c = _clods[i];
				if (c.Life > 0f)
				{
					// still on the spade a moment
					_clods[i] = c with { Life = c.Life - dt };
					continue;
				}
				c.Vel += new Vector2(0, 260f) * dt;
				c.Pos += c.Vel * dt;
				if (c.Pos.Y > From.Y + 90f)
					_clods.RemoveAt(i);
				else
					_clods[i] = c;
			}
			QueueRedraw();
		}

		public override void _Draw()
		{
			foreach (var c in _clods)
				if (c.Life <= 0f)
					DrawCircle(c.Pos, c.Size, new Color(0.11f, 0.08f, 0.06f, 0.95f));
		}
	}
}
