using Godot;

namespace HackerOrSoldier;

// README: cyclically (10s) fires a flame from a hole, which takes a life.
// The flame covers the trap's cell and the cell above it during seconds 4-6
// of each cycle, a different phase than spikes so they can be combined.
public partial class FlameTrap : Node2D
{
    public PlaySession Session { get; set; }

    private ColorRect _flame;

    public bool Flaming => Session.Time % 10f is >= 4f and < 6f;

    public override void _Ready()
    {
        AddChild(new ColorRect   // nozzle
        {
            Position = new Vector2(-10, 8),
            Size = new Vector2(20, 8),
            Color = new Color(0.45f, 0.35f, 0.30f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _flame = new ColorRect
        {
            Position = new Vector2(-9, -48),
            Size = new Vector2(18, 56),
            Color = new Color(0.98f, 0.55f, 0.10f, 0.9f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_flame);
    }

    public override void _PhysicsProcess(double delta)
    {
        _flame.Visible = Flaming;
        if (!Flaming || Session.Agent.IsHiding)
            return;
        var agent = Session.Agent;
        bool touching = Mathf.Abs(agent.Position.X - Position.X) < 24
            && agent.Position.Y + 14 > Position.Y - 48
            && agent.Position.Y - 14 < Position.Y + 14;
        if (touching)
            Session.AgentHit();
    }
}
