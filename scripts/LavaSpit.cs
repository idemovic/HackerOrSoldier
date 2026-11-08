using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Lava now and then spits a few glowing droplets into the air above its
// surface; they arc up, cool from white-hot to dark red and drop back in.
// Purely visual: droplets do not hurt anything. LevelGrids tells it which
// cells are the top of a lava pool.
public partial class LavaSpit : Node2D
{
    private const int Size = TileSetBuilder.TileSize;
    private const float SpitsPerCellPerSecond = 0.25f;
    private const float Gravity = 520f;
    private const float MaxLife = 1.6f;

    private static readonly Color[] Cooling =
    {
        new("fff6c0"), new("ffe066"), new("f2a030"), new("e85a20"), new("b02a1a"), new("6e1410"),
    };

    private record struct Drop(Vector2 Pos, Vector2 Vel, float Age, float FloorY);

    private readonly List<Vector2I> _surface = new();
    private readonly HashSet<Vector2I> _surfaceSet = new();
    private readonly List<Drop> _drops = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _budget;

    // Returns whether the cell above blocks the spit; set by LevelGrids.
    public System.Func<Vector2I, bool> Covered { get; set; } = _ => false;

    public int DropCount => _drops.Count;   // for tests

    public override void _Ready() => _rng.Randomize();

    public void SetSurface(Vector2I cell, bool isSurface)
    {
        if (isSurface ? _surfaceSet.Add(cell) : _surfaceSet.Remove(cell))
        {
            if (isSurface)
                _surface.Add(cell);
            else
                _surface.Remove(cell);
        }
    }

    public void Clear()
    {
        _surface.Clear();
        _surfaceSet.Clear();
        _drops.Clear();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _budget += _surface.Count * SpitsPerCellPerSecond * dt;
        while (_budget >= 1f)
        {
            _budget -= 1f;
            var cell = _surface[_rng.RandiRange(0, _surface.Count - 1)];
            if (!Covered(cell))
                Spit(cell);
        }
        if (_drops.Count == 0)
            return;
        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var d = _drops[i];
            d.Vel += new Vector2(0, Gravity) * dt;
            d.Pos += d.Vel * dt;
            d.Age += dt;
            if ((d.Vel.Y > 0 && d.Pos.Y >= d.FloorY) || d.Age >= MaxLife)
                _drops.RemoveAt(i);
            else
                _drops[i] = d;
        }
        QueueRedraw();
    }

    // A burst of two to four droplets from one point on the surface.
    private void Spit(Vector2I cell)
    {
        float floor = cell.Y * Size + 7;
        var from = new Vector2(cell.X * Size + _rng.RandfRange(6f, Size - 6f), floor);
        float lean = _rng.RandfRange(-25f, 25f);
        int count = _rng.RandiRange(2, 4);
        for (int i = 0; i < count; i++)
        {
            var vel = new Vector2(lean + _rng.RandfRange(-30f, 30f), -_rng.RandfRange(120f, 230f));
            _drops.Add(new Drop(from, vel, 0f, floor));
        }
    }

    public override void _Draw()
    {
        foreach (var d in _drops)
        {
            int tone = Mathf.Min((int)(d.Age / MaxLife * Cooling.Length * 1.6f), Cooling.Length - 1);
            var at = new Vector2(Mathf.Floor(d.Pos.X), Mathf.Floor(d.Pos.Y));
            // A 2x2 blob with a one-pixel tail the way it came from.
            DrawRect(new Rect2(at, new Vector2(2, 2)), Cooling[tone]);
            var tail = at - new Vector2(Mathf.Sign(d.Vel.X), Mathf.Sign(d.Vel.Y)) * 2f;
            DrawRect(new Rect2(tail, Vector2.One), Cooling[Mathf.Min(tone + 2, Cooling.Length - 1)]);
        }
    }
}
