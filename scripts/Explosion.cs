using Godot;

namespace HackerOrSoldier;

// The blast that dynamite and landmines leave behind, drawn in the same DOS
// idiom as DeathEffect: chunky 4px blocks in hard palette colours, dithered
// edges instead of alpha, and a coarse flicker clock. A white-hot flash, a
// ragged fireball that swells and cools through yellow, orange and red, a
// dithered shock ring that marks the kill radius, burning debris thrown out
// under gravity, and smoke that rises and thins away. Purely visual: the
// damage is done by PlaySession.Explode before this is added.
public partial class Explosion : Node2D
{
    private const int Block = 4;
    private const float Duration = 1.3f;
    private const float FlickerStep = 1f / 30f;

    private const float FlashTime = 0.07f;
    private const float FireGrowTime = 0.12f;
    private const float FireHoldTime = 0.3f;
    private const float FireEndTime = 0.75f;
    private const float RingTime = 0.28f;

    private const float ShakeTime = 0.35f;
    private const float ShakeAmplitude = 6f;

    private static readonly Color White = new("f2f2f2");
    private static readonly Color Yellow = new("f2d43b");
    private static readonly Color Orange = new("f28022");
    private static readonly Color Red = new("e03b2f");
    private static readonly Color DarkRed = new("8f1f1a");
    private static readonly Color Grey = new("55555f");
    private static readonly Color DarkGrey = new("2a2a33");

    // The kill radius; the shock ring runs out to it so the player can see
    // what the blast reached.
    public float Radius { get; init; } = 160f;

    // Shaken on the blast and put back when the shake is over.
    public Camera2D Camera { get; init; }

    private record struct Debris(Vector2 Pos, Vector2 Vel, float Life, int Size);
    private record struct Puff(Vector2 Pos, float Drift, float Start, float Size);

    private Debris[] _debris;
    private Vector2[] _sparkPos;
    private Vector2[] _sparkVel;
    private Puff[] _smoke;
    private uint _seed;
    private float _time;
    private bool _shaking = true;

    public override void _Ready()
    {
        ZIndex = 1;   // over the agent, enemies and pickups
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        _seed = rng.Randi();

        _debris = new Debris[22];
        for (int i = 0; i < _debris.Length; i++)
        {
            // Biased upward: a blast on the floor throws most of its junk up.
            float angle = rng.RandfRange(-Mathf.Pi * 0.95f, -Mathf.Pi * 0.05f);
            if (i % 4 == 0)
                angle = rng.RandfRange(0, Mathf.Tau);
            float speed = rng.RandfRange(220f, 520f);
            _debris[i] = new Debris(
                Vector2.Zero,
                Vector2.FromAngle(angle) * speed,
                rng.RandfRange(0.55f, 1.05f),
                rng.RandiRange(2, 3) * Block);
        }

        _sparkPos = new Vector2[28];
        _sparkVel = new Vector2[_sparkPos.Length];
        for (int i = 0; i < _sparkVel.Length; i++)
            _sparkVel[i] = Vector2.FromAngle(rng.RandfRange(0, Mathf.Tau)) * rng.RandfRange(400f, 750f);

        _smoke = new Puff[9];
        for (int i = 0; i < _smoke.Length; i++)
            _smoke[i] = new Puff(
                new Vector2(rng.RandfRange(-0.4f, 0.4f), rng.RandfRange(-0.4f, 0.15f)) * Radius,
                rng.RandfRange(-15f, 15f),
                rng.RandfRange(0.18f, 0.4f),
                rng.RandfRange(18f, 34f));
    }

    public override void _ExitTree()
    {
        if (_shaking && Camera != null && IsInstanceValid(Camera))
            Camera.Offset = Vector2.Zero;
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

        for (int i = 0; i < _debris.Length; i++)
        {
            ref var d = ref _debris[i];
            d.Vel += new Vector2(0, 1100f) * dt;
            d.Vel *= 1f - 1.2f * dt;
            d.Pos += d.Vel * dt;
        }
        for (int i = 0; i < _sparkPos.Length; i++)
        {
            _sparkVel[i] *= 1f - 4f * dt;
            _sparkPos[i] += _sparkVel[i] * dt;
        }

        Shake();
        QueueRedraw();
    }

    public override void _Draw()
    {
        int flicker = (int)(_time / FlickerStep);

        DrawSmoke(flicker);
        DrawFireball(flicker);
        DrawRing();
        DrawDebris();
        DrawSparks(flicker);

        if (_time < FlashTime)
            FillDisc(Vector2.Zero, Radius * 0.55f, (x, y, d) => d < 0.8f || ((x + y) & 1) == 0 ? White : null);
    }

    // Heat runs from 1 at the core of a fresh fireball down to 0; each cell
    // gets a stable ragged offset plus a per-frame flicker so the edge boils.
    private void DrawFireball(int flicker)
    {
        if (_time >= FireEndTime)
            return;
        float grow = Mathf.Min(_time / FireGrowTime, 1f);
        grow = 1f - (1f - grow) * (1f - grow);
        float size = Radius * 0.72f * grow;
        float cool = Mathf.Max(_time - FireHoldTime, 0f) / (FireEndTime - FireHoldTime);
        // A floor blast billows up: the ball rides upward as it burns out.
        var center = new Vector2(0, -Radius * 0.12f * (grow + cool));

        var origin = Snap(center) / Block;
        float phase = (_seed & 0xFF) / 255f * Mathf.Tau;
        FillDisc(center, size * (1f + 0.15f * cool) * 1.2f, (x, y, d) =>
        {
            // Lobes around the rim so the ball billows instead of reading
            // as a clean disc; the disc is drawn oversize to leave them room.
            float angle = Mathf.Atan2(y - origin.Y, x - origin.X);
            float lobes = 1f + 0.09f * Mathf.Sin(angle * 2f + phase)
                + 0.08f * Mathf.Sin(angle * 3f - phase * 2f)
                + 0.06f * Mathf.Sin(angle * 7f + phase * 3f);
            d = d * 1.2f / lobes;
            if (d > 1f)
                return null;
            float rag = Hash(x, y, 0) * 0.35f + Hash(x, y, (uint)flicker) * 0.12f;
            float heat = 1f - d - rag + 0.25f - cool * 1.1f;
            bool odd = ((x + y) & 1) == 0;
            return heat switch
            {
                > 0.62f => White,
                > 0.55f => odd ? White : Yellow,
                > 0.4f => Yellow,
                > 0.33f => odd ? Yellow : Orange,
                > 0.2f => Orange,
                > 0.13f => odd ? Orange : Red,
                > 0.03f => Red,
                > -0.06f => odd ? DarkRed : null,
                _ => null,
            };
        });
    }

    // Every other block on a checker, so the ring reads as a shimmer of heat
    // rather than a solid hoop.
    private void DrawRing()
    {
        if (_time >= RingTime)
            return;
        float k = _time / RingTime;
        float r = Radius * (1f - (1f - k) * (1f - k));
        float thick = Mathf.Lerp(14f, 4f, k);
        var color = k < 0.5f ? Yellow : Orange;
        int reach = Mathf.CeilToInt((r + thick) / Block);
        for (int y = -reach; y <= reach; y++)
            for (int x = -reach; x <= reach; x++)
            {
                if (((x + y) & 1) != 0)
                    continue;
                float dist = new Vector2(x * Block, y * Block).Length();
                if (dist <= r && dist >= r - thick)
                    DrawRect(new Rect2(x * Block, y * Block, Block, Block), color);
            }
    }

    private void DrawDebris()
    {
        foreach (var d in _debris)
        {
            float age = _time / d.Life;
            if (age >= 1f)
                continue;
            var color = age switch
            {
                < 0.2f => Yellow,
                < 0.45f => Orange,
                < 0.7f => Red,
                _ => DarkGrey,
            };
            var at = Snap(d.Pos);
            DrawRect(new Rect2(at, new Vector2(d.Size, d.Size)), color);
            // A short smoking trail one step behind.
            if (age < 0.7f)
                DrawRect(new Rect2(Snap(d.Pos - d.Vel * 0.02f), new Vector2(Block, Block)), age < 0.35f ? Orange : Grey);
        }
    }

    private void DrawSparks(int flicker)
    {
        if (_time > 0.35f)
            return;
        for (int i = 0; i < _sparkPos.Length; i++)
            if (((i + flicker) & 1) == 0)
                DrawRect(new Rect2(Snap(_sparkPos[i]), new Vector2(2, 2)), i % 3 == 0 ? White : Yellow);
    }

    // Smoke thins by dropping blocks from a fixed dither order, the way a VGA
    // game faded without an alpha channel.
    private void DrawSmoke(int flicker)
    {
        foreach (var puff in _smoke)
        {
            float life = (_time - puff.Start) / (Duration - puff.Start);
            if (life < 0f || life >= 1f)
                continue;
            var center = puff.Pos + new Vector2(puff.Drift * life, -60f * (_time - puff.Start));
            float size = puff.Size * (0.6f + 0.8f * life);
            float density = 1f - life;
            FillDisc(center, size, (x, y, d) =>
            {
                if (Hash(x, y, 7) > density * (1.1f - d * 0.6f))
                    return null;
                return d < 0.55f && life < 0.6f ? Grey : DarkGrey;
            });
        }
    }

    // Visits every block inside a disc; pick returns the block's colour from
    // its grid coordinates and normalised distance, or null to leave it out.
    private void FillDisc(Vector2 center, float r, System.Func<int, int, float, Color?> pick)
    {
        if (r < Block)
            return;
        var c = Snap(center);
        int reach = Mathf.CeilToInt(r / Block);
        for (int y = -reach; y <= reach; y++)
            for (int x = -reach; x <= reach; x++)
            {
                float d = new Vector2(x * Block, y * Block).Length() / r;
                if (d > 1f)
                    continue;
                int gx = x + (int)(c.X / Block), gy = y + (int)(c.Y / Block);
                if (pick(gx, gy, d) is Color color)
                    DrawRect(new Rect2(c.X + x * Block, c.Y + y * Block, Block, Block), color);
            }
    }

    private static Vector2 Snap(Vector2 v) =>
        new(Mathf.Floor(v.X / Block) * Block, Mathf.Floor(v.Y / Block) * Block);

    private float Hash(int x, int y, uint salt)
    {
        uint h = (uint)x * 374761393u + (uint)y * 668265263u + salt * 2246822519u + _seed;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    private void Shake()
    {
        if (!_shaking || Camera == null || !IsInstanceValid(Camera))
            return;
        if (_time >= ShakeTime)
        {
            // Once, so a later shake (a death, another blast) is not cut off.
            Camera.Offset = Vector2.Zero;
            _shaking = false;
            return;
        }
        // A blast across the level barely nudges the view.
        float near = 1f - Mathf.Clamp(Camera.GetScreenCenterPosition().DistanceTo(GlobalPosition) / 900f, 0f, 1f);
        float amplitude = ShakeAmplitude * near * (1f - _time / ShakeTime);
        int step = (int)(_time / FlickerStep);
        Camera.Offset = new Vector2(
            Mathf.Round(amplitude) * (step % 2 == 0 ? 1 : -1),
            Mathf.Round(amplitude * 0.6f) * (step % 4 < 2 ? -1 : 1));
    }
}
