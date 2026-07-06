using Godot;

namespace HackerOrSoldier;

// Raises the alarm while it sees the agent (README: 30s from the last
// trigger). A hidden agent or an active briefcase disguise is not detected.
public partial class SecurityCamera : Node2D
{
    private const float SightRange = 256f;   // 8 tiles, matching all-direction enemy sight

    public PlaySession Session { get; set; }

    public override void _Ready() =>
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.Camera) });

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
