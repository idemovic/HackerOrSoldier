using Godot;

namespace HackerOrSoldier;

// Dropped by drones. Falls until it rests on a solid tile, then the fuse
// runs out and PlaySession handles the explosion.
public partial class Dynamite : Node2D
{
    private const float Fuse = 5f;   // README: dynamite explodes in 5 seconds

    public PlaySession Session { get; set; }

    private float _timer = Fuse;
    private float _fallSpeed;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-4, -8),
            Size = new Vector2(8, 16),
            Color = new Color(0.9f, 0.15f, 0.1f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (TileDefs.Get(Session.TileAtPosition(Position + new Vector2(0, 10)))?.Solid == true)
        {
            _fallSpeed = 0;
        }
        else
        {
            _fallSpeed += 1200f * dt;
            Position += new Vector2(0, _fallSpeed * dt);
        }
        _timer -= dt;
        if (_timer <= 0)
        {
            QueueFree();
            Session.Explode(Position);
        }
    }
}
