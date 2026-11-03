using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// The circuit-board background, drawn as one seamless board Span tiles on a
// side. A cell shows the tile its coordinates pick out of the board, so the
// pattern repeats every Span tiles instead of every tile. Traces run in
// buses with 45-degree bends, out of chip pins or between vias, and keep a
// gap from everything else; the board wraps at its edges so traces cross
// tile seams freely.
public static class CircuitBoard
{
    public const int Span = 12;
    private const int TileSize = TileSetBuilder.TileSize;
    private const int Size = Span * TileSize;
    private const int LaneGap = 4;     // centre-to-centre spacing of bus lanes
    private const int Clearance = 2;   // empty pixels kept around every item
    private const uint Seed = 0x2545f491;

    private static Image _board;

    public static Image Tile(int column, int row)
    {
        _board ??= new Layout().Render();
        return _board.GetRegion(new Rect2I(column * TileSize, row * TileSize, TileSize, TileSize));
    }

    private enum Kind : byte { Board, Trace, Pad, Hole, Chip, ChipEdge, Pin, Part, PartEnd, Led, LedGlint }

    // Pixels an item would occupy. A pixel marked NearParent may sit next to
    // the parent item (a trace leaving its chip's pin) without counting as a
    // clash.
    private readonly record struct Stamp(Vector2I Pos, Kind Kind, bool NearParent = false);

    private static readonly Vector2I[] Directions =
    {
        new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1), new(0, -1), new(1, -1),
    };

    private static int Wrap(int v) => ((v % Size) + Size) % Size;

    // The side of a run its bus lanes stack on: the direction turned a
    // quarter clockwise on screen.
    private static Vector2I LaneSide(Vector2I d) => new(-d.Y, d.X);

    private static int Dot(Vector2I a, Vector2I b) => a.X * b.X + a.Y * b.Y;

    private sealed class Layout
    {
        private readonly Kind[,] _kinds = new Kind[Size, Size];
        private readonly int[,] _owner = new int[Size, Size];
        private uint _rng = Seed;
        private int _nextId = 1;

        public Layout()
        {
            var chips = new List<(int Id, List<Vector2I>[] Rows, int[] Dirs)>();
            for (int attempt = 0; attempt < 50 * Span && chips.Count < Span * Span / 7; attempt++)
            {
                var stamp = ChipStamp(out var rows, out var dirs);
                int id = Place(stamp, parent: 0, clearance: 10);
                if (id != 0)
                    chips.Add((id, rows, dirs));
            }
            foreach (var (id, rows, dirs) in chips)
                for (int side = 0; side < 2; side++)
                    ConnectPins(id, rows[side], dirs[side]);
            for (int attempt = 0; attempt < 32 * Span * Span; attempt++)
            {
                var start = new Vector2I(Next(Size), Next(Size));
                int lanes = Chance(0.35f) ? 1 : Range(2, 4);
                for (int tries = 0; tries < 3; tries++)
                    if (Place(BusStamp(start, Next(4) * 2, lanes, Range(2, 5), fromChip: false), 0, Clearance) != 0)
                        break;
            }
        }

        // A chip with pins on its two long sides, every pin one lane apart.
        private List<Stamp> ChipStamp(out List<Vector2I>[] rows, out int[] dirs)
        {
            int pinCount = Range(4, 9), depth = Range(2, 4);
            bool horizontal = Chance(0.6f);
            int along = pinCount * LaneGap + 1, across = depth * LaneGap + 1;
            var origin = new Vector2I(Next(Size), Next(Size));
            var stamp = new List<Stamp>();
            rows = new[] { new List<Vector2I>(), new List<Vector2I>() };
            dirs = horizontal ? new[] { 6, 2 } : new[] { 4, 0 };   // N/S or W/E
            Vector2I At(int a, int c) => origin + (horizontal ? new Vector2I(a, c) : new Vector2I(c, a));
            for (int c = 0; c < across; c++)
                for (int a = 0; a < along; a++)
                    stamp.Add(new Stamp(At(a, c), c == 0 || a == 2 && c == 2 ? Kind.ChipEdge : Kind.Chip));
            for (int i = 0; i < pinCount; i++)
            {
                int a = 2 + i * LaneGap;
                stamp.Add(new Stamp(At(a, -1), Kind.Pin));
                stamp.Add(new Stamp(At(a, across), Kind.Pin));
                rows[0].Add(At(a, -1));
                rows[1].Add(At(a, across));
            }
            return stamp;
        }

        // Runs buses out of runs of neighbouring pins on one side of a chip,
        // leaving some pins bare.
        private void ConnectPins(int chip, List<Vector2I> row, int dir)
        {
            var side = LaneSide(Directions[dir]);
            for (int i = 0; i < row.Count;)
            {
                int size = Mathf.Min(Range(2, 5), row.Count - i);
                if (Chance(0.8f))
                {
                    // Lanes stack toward the lane side, so lane 0 is the
                    // group's pin furthest from it.
                    var first = row[i];
                    for (int j = i; j < i + size; j++)
                        if (Dot(row[j], side) < Dot(first, side))
                            first = row[j];
                    var start = first + Directions[dir];
                    for (int tries = 0; tries < 8; tries++)
                        if (Place(BusStamp(start, dir, size, Range(1, 5), fromChip: true), chip, Clearance) != 0)
                            break;
                }
                i += size;
            }
        }

        // A bus of parallel lanes following one random path. The path
        // alternates straight runs with short diagonal jogs and never turns
        // more than 90 degrees from where it set off, so it cannot cross
        // itself. Each lane is the path offset to the lane side, mitred at
        // every bend so the lanes stay evenly spaced. Odd lanes run one gap
        // further so their end vias stagger.
        private List<Stamp> BusStamp(Vector2I start, int dir0, int lanes, int segments, bool fromChip)
        {
            var dirs = new List<int>();
            var lengths = new List<int>();
            int dir = dir0, turned = 0;
            for (int s = 0; s < segments; s++)
            {
                bool diagonal = dir % 2 == 1;
                dirs.Add(dir);
                lengths.Add(diagonal ? Range(1, 3) * LaneGap : Range(s == 0 && fromChip ? 3 : 2, 8) * LaneGap);
                int sign = Chance(0.5f) ? 1 : -1;
                if (Mathf.Abs(turned + sign) > 2)
                    sign = -sign;
                turned += sign;
                dir = (dir + sign + 8) % 8;
            }

            // Short free traces read as staples, not wiring.
            int total = 0;
            foreach (int length in lengths)
                total += length;
            if (!fromChip && total < 40)
                return new List<Stamp>();

            var vertices = new List<Vector2I> { start };
            for (int s = 0; s < segments; s++)
                vertices.Add(vertices[s] + Directions[dirs[s]] * lengths[s]);
            var miters = new List<Vector2I> { LaneSide(Directions[dirs[0]]) * LaneGap };
            for (int s = 1; s < segments; s++)
                miters.Add(Miter(LaneSide(Directions[dirs[s - 1]]), LaneSide(Directions[dirs[s]])));
            miters.Add(LaneSide(Directions[dirs[segments - 1]]) * LaneGap);

            var stamp = new List<Stamp>();
            for (int k = 0; k < lanes; k++)
            {
                var points = new List<Vector2I>();
                for (int v = 0; v <= segments; v++)
                    points.Add(vertices[v] + miters[v] * k);
                if (k % 2 == 1)
                    points[segments] += Directions[dirs[segments - 1]] * LaneGap;
                int step = 0;
                for (int s = 0; s < segments; s++)
                {
                    var d = Directions[dirs[s]];
                    int run = Dot(points[s + 1] - points[s], d) / Dot(d, d);
                    if (run < 2)
                        return new List<Stamp>();   // an inner lane folded over
                    for (int t = 0; t < run; t++, step++)
                        stamp.Add(new Stamp(points[s] + d * t, Kind.Trace, step <= Clearance + 1));
                    if (lanes == 1 && dirs[s] % 2 == 0 && run >= 16 && Chance(0.35f))
                        AddPart(stamp, points[s] + d * (run / 2), d);
                }
                var end = points[segments];
                stamp.Add(new Stamp(end, Kind.Trace));
                AddVia(stamp, end, led: lanes == 1 && Chance(0.12f));
                if (!fromChip)
                    AddVia(stamp, points[0], led: false);
            }
            return stamp;
        }

        // The lane offset at a bend: the point one lane gap from the path
        // across both runs that meet there.
        private static Vector2I Miter(Vector2I n1, Vector2I n2)
        {
            int c1 = LaneGap * Dot(n1, n1), c2 = LaneGap * Dot(n2, n2);
            int det = n1.X * n2.Y - n1.Y * n2.X;
            if (det == 0)
                return n1 * LaneGap;
            return new Vector2I((c1 * n2.Y - c2 * n1.Y) / det, (n1.X * c2 - n2.X * c1) / det);
        }

        private static void AddVia(List<Stamp> stamp, Vector2I at, bool led)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var kind = led ? (dx < 1 && dy < 1 && dx + dy > -2 ? Kind.LedGlint : Kind.Led)
                        : dx == 0 && dy == 0 ? Kind.Hole : Kind.Pad;
                    stamp.Add(new Stamp(at + new Vector2I(dx, dy), kind));
                }
        }

        // A surface-mount part soldered across a straight trace: metal caps
        // at both ends and a dark body between them.
        private static void AddPart(List<Stamp> stamp, Vector2I at, Vector2I d)
        {
            var across = LaneSide(d);
            for (int a = -3; a <= 3; a++)
                for (int c = -1; c <= 1; c++)
                    stamp.Add(new Stamp(at + d * a + across * c, a is -3 or 3 ? Kind.PartEnd : Kind.Part));
        }

        // Commits the stamp under a new id when no pixel lands within
        // `clearance` of another item. Returns the id, or 0 when it clashed.
        private int Place(List<Stamp> stamp, int parent, int clearance)
        {
            if (stamp.Count == 0)
                return 0;
            foreach (var s in stamp)
                for (int dy = -clearance; dy <= clearance; dy++)
                    for (int dx = -clearance; dx <= clearance; dx++)
                    {
                        int owner = _owner[Wrap(s.Pos.X + dx), Wrap(s.Pos.Y + dy)];
                        if (owner != 0 && !(owner == parent && s.NearParent))
                            return 0;
                    }
            int id = _nextId++;
            foreach (var s in stamp)
            {
                int x = Wrap(s.Pos.X), y = Wrap(s.Pos.Y);
                _kinds[x, y] = s.Kind;
                _owner[x, y] = id;
            }
            return id;
        }

        public Image Render()
        {
            var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    img.SetPixel(x, y, ColorAt(x, y));
            return img;
        }

        private Color ColorAt(int x, int y)
        {
            switch (_kinds[x, y])
            {
                case Kind.Trace: return new Color("1f4a2c");
                case Kind.Pad: return new Color("2a5f38");
                case Kind.Hole: return new Color("070c09");
                case Kind.Chip: return new Color("0a0c0a");
                case Kind.ChipEdge: return new Color("2a2c33");
                case Kind.Pin: return new Color("4a4d57");
                case Kind.Part: return new Color("241d14");
                case Kind.PartEnd: return new Color("55555f");
                case Kind.Led: return new Color("2a8f3a");
                case Kind.LedGlint: return new Color("5fd36a");
            }
            // Raised copper and parts throw a shadow down and to the right.
            bool shaded = Casts(_kinds[Wrap(x - 1), y]) || Casts(_kinds[x, Wrap(y - 1)]);
            if (shaded)
                return new Color("0a120d");
            return Grain(x, y) < 0.05f ? new Color("16261c") : new Color("0f1a14");
        }

        private static bool Casts(Kind kind) => kind is not (Kind.Board or Kind.Hole);

        private static float Grain(int x, int y)
        {
            uint h = unchecked((uint)(x * 374761393 + y * 668265263 + 110 * 2246822519));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65536f;
        }

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
