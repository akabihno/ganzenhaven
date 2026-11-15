using Godot;

/// <summary>
/// Background music: one looping track per location (audio/music/&lt;id&gt;.ogg), crossfaded
/// on travel and pulled down while someone speaks. Owned by <see cref="Game"/>.
/// </summary>
/// <remarks>
/// Locations play the track named after their id unless their MusicId says otherwise; the
/// map and main menu play "map"; cutscenes fade it out. Scripts can switch with
/// <c>@music id</c> / <c>@music stop</c> until the next screen change.
/// </remarks>
public partial class MusicPlayer : Node
{
	private const float CrossfadeSeconds = 1.6f;
	private const float DuckLevel = 0.45f;   // music level under speech

	private readonly AudioStreamPlayer[] _decks = new AudioStreamPlayer[2];
	private readonly float[] _levels = new float[2];
	private readonly Tween[] _fades = new Tween[2];
	private int _active;
	private float _duck = 1f;
	private Tween _duckTween;

	public string Current { get; private set; } = "";

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		for (int i = 0; i < 2; i++)
		{
			_decks[i] = new AudioStreamPlayer { Bus = "Music" };
			AddChild(_decks[i]);
		}
	}

	/// <summary>Crossfades to audio/music/&lt;id&gt;.ogg; an empty id or "stop" fades out.</summary>
	public void Play(string id)
	{
		if (id == "stop")
			id = "";
		if (id == Current)
			return;
		Current = id;

		int from = _active, to = 1 - _active;
		FadeDeck(from, 0f);

		string path = $"res://audio/music/{id}.ogg";
		if (string.IsNullOrEmpty(id) || !ResourceLoader.Exists(path))
		{
			if (!string.IsNullOrEmpty(id))
				GD.PushWarning($"No music at {path}");
			return;
		}

		// Dispose the C# handle; the player keeps its own reference, so nothing lingers at exit.
		using var stream = GD.Load<AudioStream>(path);
		if (stream is AudioStreamOggVorbis ogg)
			ogg.Loop = true;
		_decks[to].Stream = stream;
		_levels[to] = 0f;
		_decks[to].Play();
		FadeDeck(to, 1f);
		_active = to;
	}

	/// <summary>Lowers the music while a voice line plays.</summary>
	public void Duck(bool on)
	{
		_duckTween?.Kill();
		_duckTween = CreateTween();
		_duckTween.TweenMethod(Callable.From<float>(v => _duck = v), _duck, on ? DuckLevel : 1f, on ? 0.25f : 0.8f);
	}

	private void FadeDeck(int deck, float target)
	{
		// Only the latest fade may drive a deck: an older fade-out still running would keep
		// pulling it to silence and then stop the track that just started on it.
		_fades[deck]?.Kill();
		var tween = CreateTween();
		_fades[deck] = tween;
		tween.TweenMethod(Callable.From<float>(v => _levels[deck] = v), _levels[deck], target, CrossfadeSeconds);
		if (target == 0f)
			tween.TweenCallback(Callable.From(() =>
			{
				if (_levels[deck] <= 0.001f)
					_decks[deck].Stop();
			}));
	}

	/// <summary>Stops both decks and lets go of their tracks (before quitting).</summary>
	public void StopAll()
	{
		foreach (var tween in _fades)
			tween?.Kill();
		foreach (var deck in _decks)
		{
			deck.Stop();
			deck.Stream = null;
		}
		Current = "";
	}

	public override void _ExitTree() => StopAll();

	public override void _Process(double delta)
	{
		for (int i = 0; i < 2; i++)
			_decks[i].VolumeDb = Mathf.LinearToDb(Mathf.Max(_levels[i] * _duck, 0.0001f));
	}
}
