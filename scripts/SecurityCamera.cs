using Godot;

namespace HackerOrSoldier;

// Raises the alarm while it sees the agent (README: 30s from the last
// trigger). A hidden agent or an active briefcase disguise is not detected.
public partial class SecurityCamera : Node2D
{
    private const float SightRange = 256f;   // 8 tiles, matching all-direction enemy sight

    public PlaySession Session { get; set; }

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-10, -6),
            Size = new Vector2(20, 12),
            Color = new Color(0.65f, 0.65f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddChild(new ColorRect
        {
            Position = new Vector2(4, -3),
            Size = new Vector2(6, 6),
            Color = new Color(0.9f, 0.2f, 0.2f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        var agent = Session.Agent;
        if (agent.IsHiding || Session.DisguiseActive)
            return;
        if (Position.DistanceTo(agent.Position) > SightRange)
            return;
        if (Session.LineClear(Position, agent.Position))
            Session.TriggerAlarm();
    }
}
