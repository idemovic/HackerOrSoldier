using Godot;

namespace HackerOrSoldier;

// README: cyclically (10s) fires a flame from a hole, which takes a life.
// The flame covers the trap's cell and the cell above it during seconds 4-6
// of each cycle, a different phase than spikes so they can be combined.
// The four dots on the trap light up one by one (seconds 2, 2.5, 3, 3.5)
// as a countdown to the burst and stay lit while it burns.
public partial class FlameTrap : Node2D
{
    public PlaySession Session { get; set; }

    private Sprite2D _body;
    private Sprite2D _flame;
    private bool _wasFlaming;
    private int _shownDots;

    public bool Flaming => Session.Time % 10f is >= 4f and < 6f;

    public static int WarningDots(float time)
    {
        float t = time % 10f;
        return t is >= 2f and < 6f ? Mathf.Min(4, (int)((t - 2f) / 0.5f) + 1) : 0;
    }

    private static readonly string[] Flicker = { "flame", "flame2", "flame3" };

    // Frame for a moment of the 2s burst: it grows out of the nozzle,
    // flickers at 12 fps, then shrinks back down.
    public static string FlameFrame(float t)
    {
        if (t < 0.08f || t >= 1.92f)
            return "flame_start1";
        if (t < 0.16f || t >= 1.84f)
            return "flame_start2";
        return Flicker[(int)(t * 12f) % Flicker.Length];
    }

    public override void _Ready()
    {
        _body = new Sprite2D { Texture = Sprites.Get(ObjectDefs.FlameTrap) };
        AddChild(_body);
        _flame = new Sprite2D
        {
            Texture = Sprites.Get("flame"),
            Offset = new Vector2(0, -20),   // the flame column rises from the nozzle
            Visible = false,
        };
        AddChild(_flame);
    }

    public override void _PhysicsProcess(double delta)
    {
        int dots = WarningDots(Session.Time);
        if (dots != _shownDots)
        {
            _body.Texture = Sprites.Get(dots == 0 ? ObjectDefs.FlameTrap : $"flame_trap_lit{dots}");
            _shownDots = dots;
        }
        bool flaming = Flaming;
        _flame.Visible = flaming;
        if (flaming)
        {
            _flame.Texture = Sprites.Get(FlameFrame(Session.Time % 10f - 4f));
            _flame.FlipH = (int)(Session.Time * 4f) % 2 == 1;
        }
        if (flaming && !_wasFlaming)
            Sfx.PlayAt(Sound.FlameBurst, Position);
        _wasFlaming = flaming;
        if (!flaming || Session.Agent.IsHiding)
            return;
        var agent = Session.Agent;
        bool touching = Mathf.Abs(agent.Position.X - Position.X) < Agent.HalfSize.X + 9
            && agent.Position.Y + 14 > Position.Y - 48
            && agent.Position.Y - 14 < Position.Y + 14;
        if (touching)
            Session.AgentHit();
    }
}
