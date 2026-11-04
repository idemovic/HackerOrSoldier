using Godot;

namespace HackerOrSoldier;

// README: cyclically (10s) fires a flame from a hole, which takes a life.
// The flame covers the trap's cell and the cell above it during seconds 4-6
// of each cycle, a different phase than spikes so they can be combined.
public partial class FlameTrap : Node2D
{
    public PlaySession Session { get; set; }

    private Sprite2D _flame;

    public bool Flaming => Session.Time % 10f is >= 4f and < 6f;

    public override void _Ready()
    {
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.FlameTrap) });
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
        _flame.Visible = Flaming;
        if (!Flaming || Session.Agent.IsHiding)
            return;
        var agent = Session.Agent;
        bool touching = Mathf.Abs(agent.Position.X - Position.X) < Agent.HalfSize.X + 9
            && agent.Position.Y + 14 > Position.Y - 48
            && agent.Position.Y - 14 < Position.Y + 14;
        if (touching)
            Session.AgentHit();
    }
}
