using Godot;

namespace HackerOrSoldier;

// Raises the alarm while it sees the agent (README: 30s from the last
// trigger). A hidden agent or an active briefcase disguise is not detected.
public partial class SecurityCamera : Node2D
{
    public const float SightRange = 256f;    // 8 tiles, matching all-direction enemy sight
    private const float BlinkPeriod = 0.5f;  // seconds per on/off cycle of the alarm LED
    private const int SightRays = 180;
    private const float SightStep = 4f;

    // The edge of the area the camera sees: a ray every 2 degrees out to
    // SightRange, each cut at the first solid point, like LineClear in play.
    public static Vector2[] SightOutline(Vector2 origin, System.Func<Vector2, bool> solid)
    {
        var outline = new Vector2[SightRays];
        for (int i = 0; i < SightRays; i++)
        {
            var dir = Vector2.Right.Rotated(Mathf.Tau * i / SightRays);
            float reach = 0f;
            while (reach < SightRange && !solid(origin + dir * Mathf.Min(reach + SightStep, SightRange)))
                reach = Mathf.Min(reach + SightStep, SightRange);
            outline[i] = origin + dir * reach;
        }
        return outline;
    }

    public PlaySession Session { get; set; }

    private Sprite2D _sprite;
    private float _blinkTime;

    public override void _Ready()
    {
        _sprite = new Sprite2D { Texture = Sprites.Get(ObjectDefs.Camera) };
        AddChild(_sprite);
    }

    public override void _PhysicsProcess(double delta)
    {
        _blinkTime = Session.AlarmActive ? (_blinkTime + (float)delta) % BlinkPeriod : 0f;
        _sprite.Texture = Sprites.Get(!Session.AlarmActive ? ObjectDefs.Camera
            : _blinkTime < BlinkPeriod / 2 ? "camera_alarm" : "camera_alarm_off");

        var agent = Session.Agent;
        if (agent.IsHiding || Session.DisguiseActive)
            return;
        if (Position.DistanceTo(agent.Position) > SightRange)
            return;
        if (Session.LineClear(Position, agent.Position))
            Session.TriggerAlarm();
    }
}
