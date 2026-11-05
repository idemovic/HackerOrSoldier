using Godot;

namespace HackerOrSoldier;

// Movable barrel (README: can be pushed in both directions to reach
// inaccessible places). The agent pushes it by walking into it and can
// stand on top of it; enemies treat it as a wall.
public partial class Barrel : CharacterBody2D
{
    private const float PushSpeed = 100f;
    private const float BarrelGravity = 1200f;

    public string Type = ObjectDefs.Barrel;   // wooden or steel art, same physics

    private int _pushDir;

    public override void _Ready()
    {
        CollisionLayer = 1;   // solid for the agent and enemies, like terrain
        CollisionMask = 1;
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(30, 30) },
        });
        AddChild(new Sprite2D { Texture = Sprites.Get(Type) });
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
