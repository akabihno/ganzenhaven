using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Spawns <see cref="ShardNpc"/> figures onto the office floor, one at a time, fading in.
/// </summary>
/// <remarks>
/// The spawn area is a screen-space band across the floorboards; the y position within
/// that band drives both draw order and scale, so figures further back are smaller and
/// are overlapped by the ones in front.
/// </remarks>
[GlobalClass]
public partial class NpcSpawner : Node2D
{
    [Export] public int NpcCount { get; set; } = 4;
    [Export] public float SpawnInterval { get; set; } = 1.9f;
    [Export] public float FirstDelay { get; set; } = 0.7f;

    /// <summary>0 picks a fresh cast on every run; any other value reproduces the same one.</summary>
    [Export] public int GenerationSeed { get; set; }

    [Export] public bool AutoSpawn { get; set; } = true;

    [ExportGroup("Placement")]
    /// <summary>
    /// Screen-space band of open floorboards, tuned to harbourmaster_office_final.
    /// Retune these two corners if you swap the background out.
    /// </summary>
    [Export] public Vector2 SpawnAreaMin { get; set; } = new(120.0f, 548.0f);

    [Export] public Vector2 SpawnAreaMax { get; set; } = new(1040.0f, 622.0f);
    [Export] public float FarScale { get; set; } = 0.80f;
    [Export] public float NearScale { get; set; } = 1.15f;

    /// <summary>
    /// Minimum horizontal gap between figures, so a crowd does not collapse into one
    /// unreadable mass.
    /// </summary>
    [Export] public float MinSeparation { get; set; } = 150.0f;

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<ShardNpc> _spawned = new();

    public IReadOnlyList<ShardNpc> Spawned => _spawned;

    public override void _Ready()
    {
        if (GenerationSeed == 0)
            _rng.Randomize();
        else
            _rng.Seed = (ulong)GenerationSeed;

        if (AutoSpawn)
            _ = SpawnSequenceAsync();
    }

    private async Task SpawnSequenceAsync()
    {
        await ToSignal(GetTree().CreateTimer(FirstDelay), SceneTreeTimer.SignalName.Timeout);

        for (int i = 0; i < NpcCount; i++)
        {
            // the scene can be torn down mid-wait
            if (!IsInstanceValid(this) || !IsInsideTree())
                return;

            SpawnOne();

            if (i < NpcCount - 1)
            {
                await ToSignal(GetTree().CreateTimer(SpawnInterval),
                    SceneTreeTimer.SignalName.Timeout);
            }
        }
    }

    public ShardNpc SpawnOne()
    {
        Vector2 at = PickSpot();

        var npc = new ShardNpc { Position = at };
        float depth = Mathf.Clamp(
            Mathf.InverseLerp(SpawnAreaMin.Y, SpawnAreaMax.Y, at.Y), 0.0f, 1.0f);
        npc.Scale = Vector2.One * Mathf.Lerp(FarScale, NearScale, depth);
        npc.ZIndex = (int)at.Y; // nearer figures occlude further ones
        AddChild(npc);

        npc.Generate((int)_rng.Randi());
        npc.FadeIn();
        _spawned.Add(npc);
        return npc;
    }

    /// <summary>
    /// Best of several candidates: keeps figures apart without ever failing to place
    /// one, however crowded the band already is.
    /// </summary>
    private Vector2 PickSpot()
    {
        Vector2 best = Vector2.Zero;
        float bestGap = -1.0f;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            var candidate = new Vector2(
                _rng.RandfRange(SpawnAreaMin.X, SpawnAreaMax.X),
                _rng.RandfRange(SpawnAreaMin.Y, SpawnAreaMax.Y));

            float gap = float.PositiveInfinity;
            foreach (ShardNpc npc in _spawned)
            {
                if (IsInstanceValid(npc))
                    gap = Mathf.Min(gap, Mathf.Abs(npc.Position.X - candidate.X));
            }

            if (float.IsPositiveInfinity(gap) || gap >= MinSeparation)
                return candidate;

            if (gap > bestGap)
            {
                bestGap = gap;
                best = candidate;
            }
        }

        return best;
    }

    public void ClearAll()
    {
        foreach (ShardNpc npc in _spawned)
        {
            if (IsInstanceValid(npc))
                npc.QueueFree();
        }
        _spawned.Clear();
    }
}
