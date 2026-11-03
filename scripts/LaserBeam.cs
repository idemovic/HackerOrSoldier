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

    private float _topY;
    private float _cooldown;
    private ColorRect _beam;

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        int cellX = (int)Mathf.Floor(Position.X / tile);
        int top = (int)Mathf.Floor(Position.Y / tile);
        while (top > 0 && TileDefs.Get(Session.TileAt(cellX, top - 1))?.Solid != true)
            top--;
        _topY = top * tile;

        AddChild(new ColorRect   // emitter base
        {
            Position = new Vector2(-10, 6),
            Size = new Vector2(20, 10),
            Color = new Color(0.55f, 0.20f, 0.20f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _beam = new ColorRect
        {
            Position = new Vector2(-2, _topY - Position.Y),
            Size = new Vector2(4, Position.Y - _topY + 6),
            Color = new Color(1f, 0.25f, 0.25f, 0.85f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_beam);
    }

    public override void _PhysicsProcess(double delta)
    {
        _cooldown -= (float)delta;
        bool active = !Session.IsCircuitOff(Number);
        _beam.Visible = active;
        if (!active)
            return;

        var agent = Session.Agent;
        bool touching = !agent.IsHiding
            && Mathf.Abs(agent.Position.X - Position.X) < 17
            && agent.Position.Y + 15 > _topY
            && agent.Position.Y - 15 < Position.Y + 10;
        if (!touching || _cooldown > 0)
            return;
        _cooldown = HitCooldown;
        Session.AgentHit();
    }
}
