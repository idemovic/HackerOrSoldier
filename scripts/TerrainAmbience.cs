using System;
using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Quiet noise of live terrain, heard only when the agent is near. Each source
// is a table row: a sound, the cells it comes from and how often it fires.
public partial class TerrainAmbience : Node2D
{
    public const float Range = 5 * TileSetBuilder.TileSize;

    private sealed class Source
    {
        public Sound Sound;
        public IReadOnlyList<Vector2I> Cells;
        public Func<Vector2I, bool> Live;
        public float MinGap, MaxGap, Timer;
    }

    private readonly List<Source> _sources = new();

    public PlaySession Session { get; set; }

    public void AddTimed(Sound sound, IReadOnlyList<Vector2I> cells, Func<Vector2I, bool> live, float minGap, float maxGap) =>
        _sources.Add(new Source { Sound = sound, Cells = cells, Live = live, MinGap = minGap, MaxGap = maxGap, Timer = (float)GD.RandRange(minGap, maxGap) });

    // The center of the cell nearest the point within range, or null.
    public static Vector2? NearestWithin(IEnumerable<Vector2I> cells, Vector2 point, float range, Func<Vector2I, bool> where = null)
    {
        Vector2? best = null;
        float bestSq = range * range;
        foreach (var cell in cells)
        {
            if (where != null && !where(cell))
                continue;
            var center = (new Vector2(cell.X, cell.Y) + new Vector2(0.5f, 0.5f)) * TileSetBuilder.TileSize;
            float distSq = center.DistanceSquaredTo(point);
            if (distSq > bestSq)
                continue;
            best = center;
            bestSq = distSq;
        }
        return best;
    }

    public void OnLavaSpat(Vector2I cell) => Hear(Sound.LavaBubble, new[] { cell });

    public override void _Process(double delta)
    {
        foreach (var source in _sources)
        {
            source.Timer -= (float)delta;
            if (source.Timer > 0f)
                continue;
            source.Timer = (float)GD.RandRange(source.MinGap, source.MaxGap);
            Hear(source.Sound, source.Cells, source.Live);
        }
    }

    private void Hear(Sound sound, IEnumerable<Vector2I> cells, Func<Vector2I, bool> live = null)
    {
        if (Session.Agent == null || Session.AgentDown)
            return;
        if (NearestWithin(cells, Session.Agent.Position, Range, live) is Vector2 at)
            Sfx.PlayAt(sound, at, Range);
    }
}
