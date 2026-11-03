using Godot;

namespace HackerOrSoldier;

// The cave-rock background, drawn as one seamless face Span tiles on a
// side, like CircuitBoard: a cell shows the tile its coordinates pick out
// of the face, so the pattern repeats every Span tiles. A flat stone body
// carries wandering cracks: deep ones with a lit lower-right lip (light
// from the top left) that branch into hairlines, dotted hairlines on their
// own, and stippling that gathers in patches and along the cracks. The face
// wraps at its edges so cracks cross tile seams freely.
public static class RockFace
{
    public const int Span = 8;
    private const int TileSize = TileSetBuilder.TileSize;
    private const int Size = Span * TileSize;
    private const uint Seed = 0x6d2b79f5;

    // Crack, hairline, body, lit lip.
    private static readonly Color Deep = new("1a1c23");
    private static readonly Color Hair = new("24262d");
    private static readonly Color Body = new("32353e");
    private static readonly Color Lip = new("3e424c");

    private static Image _face;

    public static Image Tile(int column, int row)
    {
        _face ??= new Painter().Render();
        return _face.GetRegion(new Rect2I(column * TileSize, row * TileSize, TileSize, TileSize));
    }

    private static int Wrap(int v) => ((v % Size) + Size) % Size;

    private enum Mark : byte { Body, Dot, Hair, Deep }

    private sealed class Painter
    {
        private readonly Mark[,] _marks = new Mark[Size, Size];
        private uint _rng = Seed;

        public Image Render()
        {
            Stipple();
            for (int i = 0; i < Span * Span / 3; i++)
                Crack(new Vector2(Next(Size), Next(Size)), Angle(), Range(30, 80), deep: true);
            for (int i = 0; i < Span * Span / 2; i++)
                Crack(new Vector2(Next(Size), Next(Size)), Angle(), Range(8, 30), deep: false);

            var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    Color c = _marks[x, y] switch
                    {
                        Mark.Deep => Deep,
                        Mark.Hair or Mark.Dot => Hair,
                        _ => Body,
                    };
                    // The groove's lower-right wall faces the light.
                    if (_marks[x, y] == Mark.Body && At(x - 1, y - 1) == Mark.Deep
                        && At(x - 1, y) != Mark.Deep && At(x, y - 1) != Mark.Deep)
                        c = Lip;
                    img.SetPixel(x, y, c);
                }
            return img;
        }

        private Mark At(int x, int y) => _marks[Wrap(x), Wrap(y)];

        private void Set(int x, int y, Mark mark)
        {
            x = Wrap(x);
            y = Wrap(y);
            if (mark > _marks[x, y])
                _marks[x, y] = mark;
        }

        // Sparse dots everywhere, denser in a few soft patches.
        private void Stipple()
        {
            var patches = new Vector2[Span * Span / 6];
            for (int i = 0; i < patches.Length; i++)
                patches[i] = new Vector2(Next(Size), Next(Size));
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float density = 0.006f;
                    foreach (var p in patches)
                    {
                        float dx = Mathf.Abs(x - p.X), dy = Mathf.Abs(y - p.Y);
                        dx = Mathf.Min(dx, Size - dx);
                        dy = Mathf.Min(dy, Size - dy);
                        density += 0.05f * Mathf.Max(0f, 1f - (dx * dx + dy * dy) / (24f * 24f));
                    }
                    if (Next(10000) < density * 10000f)
                        Set(x, y, Mark.Dot);
                }
        }

        // A crack runs one pixel per step in near-straight runs with sharp
        // kinks between them, never turning far from its first heading.
        // Deep cracks run solid, sometimes two pixels
        // wide, and throw off hairline branches; hairlines break into dots.
        // Both shed stray dots beside them.
        private void Crack(Vector2 pos, float angle, int length, bool deep)
        {
            bool wide = false;
            float heading = angle;
            for (int step = 0; step < length; step++)
            {
                angle += (Next(1000) / 1000f - 0.5f) * 0.15f;
                if (Chance(0.1f))
                    angle += (Chance(0.5f) ? 1 : -1) * Range(5, 10) / 10f;
                angle = Mathf.Clamp(angle, heading - 0.9f, heading + 0.9f);
                pos += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int x = Mathf.RoundToInt(pos.X), y = Mathf.RoundToInt(pos.Y);
                if (deep)
                {
                    Set(x, y, Mark.Deep);
                    if (Chance(0.05f))
                        wide = !wide;
                    if (wide && step > 3 && step < length - 3)
                        Set(x + (Mathf.Abs(Mathf.Sin(angle)) > 0.7f ? 1 : 0), y + (Mathf.Abs(Mathf.Sin(angle)) > 0.7f ? 0 : 1), Mark.Deep);
                    if (Chance(0.025f))
                        Crack(pos, angle + (Chance(0.5f) ? 1f : -1f), Range(6, 20), deep: false);
                }
                else if (!Chance(0.3f))
                    Set(x, y, Mark.Hair);
                if (Chance(0.08f))
                {
                    var side = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * Range(2, 3) * (Chance(0.5f) ? 1 : -1);
                    Set(x + Mathf.RoundToInt(side.X), y + Mathf.RoundToInt(side.Y), Mark.Dot);
                }
            }
        }

        private float Angle() => Next(6283) / 1000f;

        private int Next(int n)
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (int)(_rng % (uint)n);
        }

        private int Range(int lo, int hi) => lo + Next(hi - lo + 1);

        private bool Chance(float p) => Next(1000) < p * 1000f;
    }
}
