using Godot;

namespace HackerOrSoldier;

// README: an electric door cyclically (10s) engages an electric field that
// takes a life. The field is on during the last 2 seconds of each cycle,
// same phase as the right spike of a spike tile. Before it the pillar lights
// come on one by one, bottom to top, each state lasting as long as the field;
// once all four are lit the field arcs as lightning between the pillars.
public partial class ElectricDoorField : Node2D
{
    public PlaySession Session { get; set; }

    private const float Cycle = 10f;
    private const float LightStep = 2f;   // same as the field's on time
    private const float BoltReseed = 0.06f;   // how often the arcs change shape

    // Light centres on the pillars, bottom to top, in the 32px sprite's space.
    private static readonly float[] LightY = { 7f, 1f, -5f, -11f };
    private const float PillarX = 10f;
    private const float PillarInner = 8f;

    private static readonly Color Lit = new("f2d43b");
    private static readonly Color LightGlow = new(0.95f, 0.85f, 0.25f, 0.35f);
    private static readonly Color BoltGlow = new(0.55f, 0.85f, 1f, 0.55f);

    public bool Active => Session.RaisedSpike == 1;

    public override void _Ready()
    {
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.ElectricDoor), ShowBehindParent = true });
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _PhysicsProcess(double delta)
    {
        if (!Active || Session.Agent.IsHiding)
            return;
        var d = (Session.Agent.Position - Position).Abs();
        if (d.X < Agent.HalfSize.X + 11 && d.Y < 28)
            Session.AgentHit();
    }

    private int LitCount()
    {
        if (Active)
            return LightY.Length;
        return Mathf.Min(LightY.Length, (int)(Session.Time % Cycle / LightStep) + 1);
    }

    public override void _Draw()
    {
        bool active = Active;
        var rng = new RandomNumberGenerator { Seed = (ulong)(Session.Time / BoltReseed) + GetInstanceId() };
        for (int i = 0; i < LitCount(); i++)
            foreach (float x in new[] { -PillarX, PillarX })
            {
                var core = new Rect2(x - 2, LightY[i] - 1, 4, 2);
                DrawRect(core.Grow(1), LightGlow);
                DrawRect(core, active && rng.Randf() < 0.5f ? Colors.White : Lit);
            }
        if (!active)
            return;
        for (int b = 0; b < 3; b++)
            DrawBolt(rng, LightY[rng.RandiRange(0, LightY.Length - 1)], LightY[rng.RandiRange(0, LightY.Length - 1)]);
    }

    // A jagged arc from a light on the left pillar to one on the right: a
    // wide pale-blue glow under a thin white core.
    private void DrawBolt(RandomNumberGenerator rng, float fromY, float toY)
    {
        const int segments = 5;
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float f = (float)i / segments;
            float jitter = i == 0 || i == segments ? 0f : rng.RandfRange(-4f, 4f);
            points[i] = new Vector2(Mathf.Lerp(-PillarInner, PillarInner, f), Mathf.Lerp(fromY, toY, f) + jitter);
        }
        DrawPolyline(points, BoltGlow, 3f);
        DrawPolyline(points, Colors.White, 1f);
    }
}
