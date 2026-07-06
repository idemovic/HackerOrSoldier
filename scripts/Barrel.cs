using Godot;

namespace HackerOrSoldier;

// Movable barrel (README: can be pushed in both directions to reach
// inaccessible places). The agent pushes it by walking into it and can
// stand on top of it; enemies treat it as a wall.
public partial class Barrel : CharacterBody2D
{
    private const float PushSpeed = 100f;
    private const float BarrelGravity = 1200f;

    private int _pushDir;

    public override void _Ready()
    {
        CollisionLayer = 1;   // solid for the agent and enemies, like terrain
        CollisionMask = 1;
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(30, 30) },
        });
        AddChild(new ColorRect
        {
            Position = new Vector2(-15, -15),
            Size = new Vector2(30, 30),
            Color = new Color(0.62f, 0.44f, 0.24f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddChild(new ColorRect   // barrel hoop
        {
            Position = new Vector2(-15, -3),
            Size = new Vector2(30, 5),
            Color = new Color(0.45f, 0.30f, 0.15f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    public void Push(int direction) => _pushDir = direction;

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;
        velocity.X = _pushDir * PushSpeed;
        _pushDir = 0;
        velocity.Y += BarrelGravity * (float)delta;
        Velocity = velocity;
        MoveAndSlide();
    }
}
