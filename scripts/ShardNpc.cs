using System.Collections.Generic;
using Godot;

/// <summary>
/// A procedurally assembled figure built entirely from long pointy shards.
/// </summary>
/// <remarks>
/// Local origin sits at the figure's ground contact, so a spawner only has to place it
/// on the floor. Anatomy is four bands stacked upward -- mantle, torso, limbs, crown --
/// which keeps the silhouette reading as a <em>thing that stands</em> no matter how the
/// randomisation lands.
/// </remarks>
[GlobalClass]
public partial class ShardNpc : Node2D
{
    private static readonly string[] ShardPaths =
    {
        "res://assets/shards/shard_01.png",
        "res://assets/shards/shard_02.png",
        "res://assets/shards/shard_03.png",
        "res://assets/shards/shard_04.png",
        "res://assets/shards/shard_05.png",
        "res://assets/shards/shard_06.png",
    };

    private static Texture2D[] _shardTextures;

    // loaded lazily: static field initialisers can run before the resource
    // system is ready in some editor/tool contexts
    private static Texture2D[] ShardTextures =>
        _shardTextures ??= System.Array.ConvertAll(ShardPaths, GD.Load<Texture2D>);

    private static readonly (string Name, Color Base, Color Accent, Color Eye)[] Palettes =
    {
        ("drowned", new Color(0.129f, 0.204f, 0.176f),
            new Color(0.392f, 0.529f, 0.353f), new Color(0.694f, 0.937f, 0.518f)),
        ("bruised", new Color(0.176f, 0.129f, 0.235f),
            new Color(0.463f, 0.322f, 0.545f), new Color(0.855f, 0.588f, 0.980f)),
        ("abyssal", new Color(0.106f, 0.145f, 0.220f),
            new Color(0.322f, 0.435f, 0.596f), new Color(0.541f, 0.812f, 0.980f)),
        ("rust", new Color(0.212f, 0.125f, 0.106f),
            new Color(0.545f, 0.298f, 0.196f), new Color(0.980f, 0.639f, 0.361f)),
        ("ashen", new Color(0.180f, 0.184f, 0.196f),
            new Color(0.514f, 0.518f, 0.490f), new Color(0.902f, 0.914f, 0.855f)),
    };

    /// <summary>Nominal standing height in local units, before the node's own scale.</summary>
    [Export] public float FigureHeight { get; set; } = 250.0f;

    /// <summary>Amplitude of the idle sway, in radians. Small values read as breathing.</summary>
    [Export] public float SwayAmount { get; set; } = 0.030f;

    [Export] public float SwaySpeed { get; set; } = 0.9f;

    public string PaletteName { get; private set; } = "";

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<Sprite2D> _shards = new();
    private readonly List<Sprite2D> _eyes = new();
    private readonly List<float> _baseRotation = new();
    private readonly List<float> _swayPhase = new();
    private readonly List<float> _swayScale = new();

    private Vector2 _crownPoint = Vector2.Zero;
    private float _time;
    private bool _settled;

    public override void _Process(double delta)
    {
        _time += (float)delta;

        for (int i = 0; i < _shards.Count; i++)
        {
            _shards[i].Rotation = _baseRotation[i]
                + Mathf.Sin(_time * SwaySpeed + _swayPhase[i]) * SwayAmount * _swayScale[i];
        }

        if (!_settled)
            return;

        for (int i = 0; i < _eyes.Count; i++)
        {
            Color c = _eyes[i].Modulate;
            c.A = 0.72f + 0.28f * Mathf.Sin(_time * 2.1f + i * 1.7f);
            _eyes[i].Modulate = c;
        }
    }

    // --- generation --------------------------------------------------------

    public void Generate(int generationSeed)
    {
        _rng.Seed = (ulong)generationSeed;

        foreach (Node child in GetChildren())
            child.Free();

        _shards.Clear();
        _eyes.Clear();
        _baseRotation.Clear();
        _swayPhase.Clear();
        _swayScale.Clear();

        var palette = Palettes[_rng.RandiRange(0, Palettes.Length - 1)];
        PaletteName = palette.Name;

        float h = FigureHeight;
        float lean = _rng.RandfRange(-8.0f, 8.0f); // the whole figure leans
        var core = new Vector2(lean, -h * _rng.RandfRange(0.48f, 0.58f));
        var torso = new Vector2(lean * 1.3f, -h * _rng.RandfRange(0.60f, 0.68f));
        var shoulder = new Vector2(lean * 1.5f, -h * _rng.RandfRange(0.70f, 0.78f));
        _crownPoint = new Vector2(shoulder.X + _rng.RandfRange(-9.0f, 9.0f),
            -h * _rng.RandfRange(0.80f, 0.88f));

        BuildMantle(palette, core);
        BuildTorso(palette, torso);
        BuildLimbs(palette, shoulder);
        BuildCrown(palette);
        BuildEyes(palette);
    }

    private void AddShard(Vector2 at, float rot, float length,
        (string Name, Color Base, Color Accent, Color Eye) palette,
        float mix, int layer, float girth = 1.0f)
    {
        Texture2D tex = ShardTextures[_rng.RandiRange(0, ShardTextures.Length - 1)];
        Vector2 size = tex.GetSize();

        float unit = length / size.Y;
        float flip = _rng.Randf() < 0.5f ? -1.0f : 1.0f;

        Color tint = palette.Base.Lerp(palette.Accent, mix);
        tint = tint.Lerp(Colors.Black, _rng.RandfRange(0.0f, 0.22f));
        tint.A = 0.0f; // FadeIn() raises this

        var s = new Sprite2D
        {
            Texture = tex,
            Centered = false,
            Offset = new Vector2(-size.X * 0.5f, -size.Y), // pivot at the base, tip up
            Position = at,
            Rotation = rot,
            ZIndex = layer,
            Scale = new Vector2(unit * girth * _rng.RandfRange(0.8f, 1.25f) * flip, unit),
            Modulate = tint,
        };

        AddChild(s);
        _shards.Add(s);
        _baseRotation.Add(rot);
        _swayPhase.Add(_rng.RandfRange(0.0f, Mathf.Tau));
        // shards reaching further from the body sway more
        _swayScale.Add(Mathf.Clamp(length / FigureHeight, 0.2f, 1.6f));
    }

    /// <summary>
    /// Broad shards falling from the core to the floor: the standing mass. Kept fat and
    /// dark so the base reads as one silhouette, not separate legs.
    /// </summary>
    private void BuildMantle((string Name, Color Base, Color Accent, Color Eye) palette,
        Vector2 core)
    {
        int count = _rng.RandiRange(6, 9);
        for (int i = 0; i < count; i++)
        {
            float splay = _rng.RandfRange(-0.5f, 0.5f);
            Vector2 origin = core + new Vector2(_rng.RandfRange(-16.0f, 16.0f),
                _rng.RandfRange(-8.0f, 14.0f));
            // reach the ground plane even when splayed out to the side
            float drop = Mathf.Abs(origin.Y) / Mathf.Max(Mathf.Cos(splay), 0.42f);
            AddShard(origin, Mathf.Pi + splay, drop * _rng.RandfRange(0.95f, 1.08f),
                palette, _rng.RandfRange(0.0f, 0.22f), -4 + i,
                _rng.RandfRange(1.8f, 3.4f));
        }
    }

    /// <summary>
    /// Short, very wide shards stacked at the centre so the figure has a body rather
    /// than a hole between its limbs.
    /// </summary>
    private void BuildTorso((string Name, Color Base, Color Accent, Color Eye) palette,
        Vector2 torso)
    {
        int count = _rng.RandiRange(4, 6);
        for (int i = 0; i < count; i++)
        {
            // biased upward so the mass always reaches the crown instead of leaving
            // the head cluster floating over a gap
            bool up = _rng.Randf() < 0.68f;
            float angle = _rng.RandfRange(-0.35f, 0.35f) + (up ? 0.0f : Mathf.Pi);
            float length = FigureHeight * (up
                ? _rng.RandfRange(0.24f, 0.40f)
                : _rng.RandfRange(0.18f, 0.30f));
            Vector2 origin = torso + new Vector2(_rng.RandfRange(-12.0f, 12.0f),
                _rng.RandfRange(-14.0f, 14.0f));
            AddShard(origin, angle, length, palette,
                _rng.RandfRange(0.0f, 0.30f), _rng.RandiRange(-1, 2),
                _rng.RandfRange(2.2f, 4.0f));
        }
    }

    /// <summary>
    /// Radiating spines, weighted to one side so a figure is never symmetric. Angles
    /// stay under ~72 degrees off vertical to keep the pose upright.
    /// </summary>
    private void BuildLimbs((string Name, Color Base, Color Accent, Color Eye) palette,
        Vector2 shoulder)
    {
        int count = _rng.RandiRange(3, 6);
        float bias = _rng.Randf() < 0.5f ? -1.0f : 1.0f;
        for (int i = 0; i < count; i++)
        {
            float side = _rng.Randf() < 0.7f ? bias : -bias;
            float angle = side * _rng.RandfRange(0.55f, 1.25f);
            float length = FigureHeight * _rng.RandfRange(0.26f, 0.50f);
            Vector2 origin = shoulder + new Vector2(side * _rng.RandfRange(0.0f, 15.0f),
                _rng.RandfRange(-14.0f, 16.0f));
            AddShard(origin, angle, length, palette,
                _rng.RandfRange(0.30f, 0.95f), _rng.RandiRange(1, 4),
                _rng.RandfRange(0.9f, 1.7f));
        }
    }

    /// <summary>Upward spikes where a head ought to be.</summary>
    private void BuildCrown((string Name, Color Base, Color Accent, Color Eye) palette)
    {
        int count = _rng.RandiRange(3, 5);
        for (int i = 0; i < count; i++)
        {
            float angle = _rng.RandfRange(-0.55f, 0.55f);
            float length = FigureHeight * _rng.RandfRange(0.20f, 0.48f);
            Vector2 origin = _crownPoint + new Vector2(_rng.RandfRange(-10.0f, 10.0f),
                _rng.RandfRange(-6.0f, 10.0f));
            AddShard(origin, angle, length, palette,
                _rng.RandfRange(0.4f, 1.0f), _rng.RandiRange(0, 3),
                _rng.RandfRange(0.8f, 1.6f));
        }
    }

    private void BuildEyes((string Name, Color Base, Color Accent, Color Eye) palette)
    {
        GradientTexture2D tex = GlowTexture();
        int count = _rng.RandiRange(1, 3);
        for (int i = 0; i < count; i++)
        {
            float radius = _rng.RandfRange(4.5f, 10.0f);
            Color glow = palette.Eye;
            glow.A = 0.0f;

            var e = new Sprite2D
            {
                Texture = tex,
                Position = _crownPoint + new Vector2(_rng.RandfRange(-18.0f, 18.0f),
                    _rng.RandfRange(-6.0f, 26.0f)),
                Scale = Vector2.One * (radius * 2.0f / tex.GetSize().X),
                Modulate = glow,
                ZIndex = 6,
            };
            AddChild(e);
            _eyes.Add(e);
        }
    }

    private static GradientTexture2D GlowTexture()
    {
        var g = new Gradient
        {
            Offsets = new float[] { 0.0f, 0.30f, 1.0f },
            Colors = new[]
            {
                new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0),
            },
        };

        return new GradientTexture2D
        {
            Gradient = g,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1.0f, 0.5f),
            Width = 64,
            Height = 64,
        };
    }

    // --- appearance --------------------------------------------------------

    /// <summary>Shards drift in from outside the silhouette and resolve one by one.</summary>
    public void FadeIn(float duration = 1.3f, float stagger = 0.055f)
    {
        Tween tween = CreateTween().SetParallel(true);

        for (int i = 0; i < _shards.Count; i++)
        {
            Sprite2D s = _shards[i];
            Vector2 home = s.Position;
            Vector2 outward = (home - _crownPoint).Normalized();
            if (outward == Vector2.Zero)
                outward = Vector2.Right;

            s.Position = home + outward * _rng.RandfRange(24.0f, 70.0f);

            tween.TweenProperty(s, "modulate:a", 1.0, duration * 0.55f)
                .SetDelay(i * stagger);
            tween.TweenProperty(s, "position", home, duration)
                .SetDelay(i * stagger)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }

        float eyeDelay = _shards.Count * stagger + duration * 0.35f;
        foreach (Sprite2D e in _eyes)
            tween.TweenProperty(e, "modulate:a", 1.0, 0.5f).SetDelay(eyeDelay);

        tween.Chain().TweenCallback(Callable.From(() => _settled = true));
    }
}
