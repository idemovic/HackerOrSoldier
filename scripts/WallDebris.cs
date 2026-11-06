using Godot;

namespace HackerOrSoldier;

// A destructible wall coming apart, in the same DOS idiom as Explosion: the
// tile's own art breaks into 8px chunks that burst out under gravity and
// blink away, grit flies in 2px specks, and a dithered dust cloud swells
// and thins. Everything takes its colours from the destroyed tile, so each
// wall colour crumbles in its own brick. Purely visual: PlaySession has
// already cleared the cell.
public partial class WallDebris : Node2D
{
    private const int Size = TileSetBuilder.TileSize;
    private const int Chunk = 8;
    private const int Block = 4;
    private const float Duration = 1.1f;
    private const float FlickerStep = 1f / 30f;
    private const float Gravity = 1100f;
    private const float DustTime = 0.7f;

    // The level tile that broke; its art is what flies apart.
    public int TileId { get; init; } = TileDefs.DestructibleWall;

    private record struct Piece(Rect2 Source, Vector2 Pos, Vector2 Vel, float Life);
    private record struct Speck(Vector2 Pos, Vector2 Vel, float Life, Color Color);

    private Texture2D _texture;
    private Piece[] _pieces;
    private Speck[] _grit;
    private Color _dust;
    private Color _dustDark;
    private uint _seed;
    private float _time;

    public float Lifetime => Duration;   // for tests

    public override void _Ready()
    {
        ZIndex = 1;   // over the agent, enemies and pickups
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        _seed = rng.Randi();
        _texture = TileSetBuilder.TileTexture(TileId);
        var image = _texture.GetImage();

        // One piece per 8x8 cell of the tile, flung away from the middle with
        // an upward kick, so the wall bursts rather than slumps.
        int across = Size / Chunk;
        _pieces = new Piece[across * across];
        for (int i = 0; i < _pieces.Length; i++)
        {
            var source = new Rect2(i % across * Chunk, i / across * Chunk, Chunk, Chunk);
            var pos = source.Position - new Vector2(Size, Size) / 2f;
            var away = (pos + new Vector2(Chunk, Chunk) / 2f).Normalized();
            var vel = away * rng.RandfRange(40f, 130f) + new Vector2(rng.RandfRange(-35f, 35f), rng.RandfRange(-230f, -100f));
            _pieces[i] = new Piece(source, pos, vel, rng.RandfRange(0.5f, 0.9f));
        }

        // Grit sampled from the tile's own pixels.
        _grit = new Speck[18];
        for (int i = 0; i < _grit.Length; i++)
        {
            var color = image.GetPixel(rng.RandiRange(0, Size - 1), rng.RandiRange(0, Size - 1));
            var vel = Vector2.FromAngle(rng.RandfRange(-Mathf.Pi, 0f)) * rng.RandfRange(120f, 280f);
            _grit[i] = new Speck(new Vector2(rng.RandfRange(-12f, 12f), rng.RandfRange(-12f, 12f)), vel,
                rng.RandfRange(0.35f, 0.7f), color with { A = 1f });
        }

        // Dust in the tile's average colour, a lighter and a darker tone.
        var sum = Colors.Black;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                sum += image.GetPixel(x, y);
        var average = new Color(sum.R / (Size * Size), sum.G / (Size * Size), sum.B / (Size * Size));
        _dust = average.Lightened(0.15f);
        _dustDark = average.Darkened(0.35f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (_time >= Duration)
        {
            QueueFree();
            return;
        }
        for (int i = 0; i < _pieces.Length; i++)
        {
            ref var p = ref _pieces[i];
            p.Vel += new Vector2(0, Gravity) * dt;
            p.Pos += p.Vel * dt;
        }
        for (int i = 0; i < _grit.Length; i++)
        {
            ref var g = ref _grit[i];
            g.Vel += new Vector2(0, Gravity * 0.8f) * dt;
            g.Vel *= 1f - 2f * dt;
            g.Pos += g.Vel * dt;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        int flicker = (int)(_time / FlickerStep);
        DrawDust();
        for (int i = 0; i < _pieces.Length; i++)
        {
            var p = _pieces[i];
            float age = _time / p.Life;
            // The last stretch blinks on a coarse clock instead of fading.
            if (age >= 1f || (age > 0.7f && ((flicker + i) & 1) == 0))
                continue;
            DrawTextureRectRegion(_texture, new Rect2(Snap(p.Pos), new Vector2(Chunk, Chunk)), p.Source);
        }
        foreach (var g in _grit)
            if (_time < g.Life)
                DrawRect(new Rect2(Snap(g.Pos, 2), new Vector2(2, 2)), g.Color);
    }

    // The cloud thins by dropping blocks from a fixed dither order, as the
    // explosion smoke does.
    private void DrawDust()
    {
        float life = _time / DustTime;
        if (life >= 1f)
            return;
        float grow = 1f - (1f - life) * (1f - life);
        float r = Size * (0.45f + 0.5f * grow);
        var center = new Vector2(0, -10f * grow);
        float density = 0.85f * (1f - life);
        int reach = Mathf.CeilToInt(r / Block);
        for (int y = -reach; y <= reach; y++)
            for (int x = -reach; x <= reach; x++)
            {
                float d = new Vector2(x * Block, y * Block).Length() / r;
                if (d > 1f || Hash(x, y) > density * (1.1f - d * 0.7f))
                    continue;
                var at = Snap(center) + new Vector2(x * Block, y * Block);
                DrawRect(new Rect2(at, new Vector2(Block, Block)), d < 0.5f ? _dust : _dustDark);
            }
    }

    private static Vector2 Snap(Vector2 v, int step = Block) =>
        new(Mathf.Floor(v.X / step) * step, Mathf.Floor(v.Y / step) * step);

    private float Hash(int x, int y)
    {
        uint h = (uint)x * 374761393u + (uint)y * 668265263u + _seed;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }
}
