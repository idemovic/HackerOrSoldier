using Godot;

namespace HackerOrSoldier;

// README: elevators move up/down, moving platforms left/right. The platform
// travels up to 4 tiles from its starting cell or until it would enter a
// solid tile, then reverses. Characters standing on it are carried.
public partial class MovingPlatform : AnimatableBody2D
{
    private const float Speed = 60f;
    private const float Range = 4 * 32f;

    public PlaySession Session { get; set; }
    public bool Vertical { get; set; }

    private Vector2 _origin;
    private int _direction;

    public override void _Ready()
    {
        SyncToPhysics = true;
        CollisionLayer = 1;
        CollisionMask = 0;
        _origin = Position;
        _direction = Vertical ? -1 : 1;   // elevators start upward, platforms rightward
        AddChild(new CollisionShape2D
        {
            Position = new Vector2(0, -4),
            Shape = new RectangleShape2D { Size = new Vector2(32, 12) },
        });
        AddChild(new ColorRect
        {
            Position = new Vector2(-16, -10),
            Size = new Vector2(32, 12),
            Color = Vertical ? new Color(0.55f, 0.60f, 0.70f) : new Color(0.60f, 0.65f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        var axis = Vertical ? Vector2.Down : Vector2.Right;
        var next = Position + axis * (_direction * Speed * (float)delta);
        float offset = (next - _origin).Dot(axis);
        var probe = next + new Vector2(0, -4) + axis * (_direction * 16);
        if (Mathf.Abs(offset) > Range || TileDefs.Get(Session.TileAtPosition(probe))?.Solid == true)
            _direction = -_direction;
        else
            Position = next;
    }
}
