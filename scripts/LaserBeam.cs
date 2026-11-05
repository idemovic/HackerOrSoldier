using Godot;

namespace HackerOrSoldier;

// A vertical beam from the emitter up to the first solid tile above it.
// Touching an active beam costs a life at once; the cooldown only matters
// when the vest absorbs the hit and the agent stays in the beam. Turning off
// the beam's circuit (computer hack or switch) disables it.
public partial class LaserBeam : Node2D
{
    private const float HitCooldown = 1f;

    public PlaySession Session { get; set; }
    public string Number { get; set; } = "1";

    private const float LensY = 5f;   // the emitter sprite's lens, below the node origin
    private const float SparkReseed = 0.05f;

    private static readonly Color OuterGlow = new(1f, 0.10f, 0.10f, 0.20f);
    private static readonly Color InnerGlow = new(1f, 0.15f, 0.15f, 0.30f);
    private static readonly Color Core = new(1f, 0.30f, 0.30f, 0.95f);
    private static readonly Color HotCore = new(1f, 0.90f, 0.90f);

    private float _topY;
    private float _cooldown;

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        int cellX = (int)Mathf.Floor(Position.X / tile);
        int top = (int)Mathf.Floor(Position.Y / tile);
        while (top > 0 && TileDefs.Get(Session.TileAt(cellX, top - 1))?.Solid != true)
            top--;
        _topY = top * tile;

        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.Laser), ShowBehindParent = true });
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    public override void _PhysicsProcess(double delta)
    {
        _cooldown -= (float)delta;
        if (Session.IsCircuitOff(Number))
            return;

        var agent = Session.Agent;
        bool touching = !agent.IsHiding
            && Mathf.Abs(agent.Position.X - Position.X) < Agent.HalfSize.X + 2
            && agent.Position.Y + Agent.HalfSize.Y > _topY
            && agent.Position.Y - Agent.HalfSize.Y < Position.Y + 10;
        if (!touching || _cooldown > 0)
            return;
        _cooldown = HitCooldown;
        Session.AgentHit();
    }

    public override void _Process(double delta) => QueueRedraw();

    // Drawn additively: stacked glow bands round a white-hot core, pulses
    // running up the beam, a flare at each end and sparks off the ceiling.
    public override void _Draw()
    {
        if (Session.IsCircuitOff(Number))
            return;
        float t = Session.Time;
        float top = _topY - Position.Y;
        float length = LensY - top;
        float pulse = 0.8f + 0.2f * Mathf.Sin(t * 25f);

        DrawRect(new Rect2(-6, top, 12, length), OuterGlow * new Color(1, 1, 1, pulse));
        DrawRect(new Rect2(-4, top, 8, length), InnerGlow * new Color(1, 1, 1, pulse));
        DrawRect(new Rect2(-2, top, 4, length), Core);
        DrawRect(new Rect2(-1, top, 2, length), HotCore);

        const float spacing = 18f;
        float scroll = t * 140f % spacing;
        for (float y = LensY - scroll; y > top; y -= spacing)
            DrawRect(new Rect2(-3, Mathf.Max(top, y - 5), 6, Mathf.Min(5, y - top)), Core);

        foreach (float y in new[] { top, LensY })
        {
            DrawCircle(new Vector2(0, y), 5f * pulse, InnerGlow);
            DrawCircle(new Vector2(0, y), 2.5f, Core);
        }

        var rng = new RandomNumberGenerator { Seed = (ulong)(t / SparkReseed) + GetInstanceId() };
        for (int i = 0; i < 3; i++)
        {
            float angle = rng.RandfRange(0.2f, Mathf.Pi - 0.2f);
            var from = new Vector2(0, top + 1);
            DrawLine(from, from + Vector2.FromAngle(angle) * rng.RandfRange(3f, 7f), HotCore, 1f);
        }
    }
}
