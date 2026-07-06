using Godot;

namespace HackerOrSoldier;

// README device: launches rockets (rockets take a life and move at 1x agent
// speed). Fires every 3 seconds, alternating left and right, starting left.
public partial class RocketLauncher : Node2D
{
    private const float Interval = 3f;

    public PlaySession Session { get; set; }

    private float _timer = Interval / 2f;
    private int _direction = -1;

    public override void _Ready() =>
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.RocketLauncher) });

    public override void _PhysicsProcess(double delta)
    {
        _timer -= (float)delta;
        if (_timer > 0)
            return;
        _timer = Interval;
        Session.SpawnRocket(Position + new Vector2(_direction * 20, -2), _direction);
        _direction = -_direction;
    }
}
