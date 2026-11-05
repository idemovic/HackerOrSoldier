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
    private Sprite2D _sprite;

    // The sprite faces right; it turns to aim each next shot.
    public override void _Ready()
    {
        _sprite = new Sprite2D { Texture = Sprites.Get(ObjectDefs.RocketLauncher), FlipH = _direction < 0 };
        AddChild(_sprite);
    }

    public override void _PhysicsProcess(double delta)
    {
        _timer -= (float)delta;
        if (_timer > 0)
            return;
        _timer = Interval;
        Session.SpawnRocket(Position + new Vector2(_direction * 20, -2), _direction);
        Sfx.PlayAt(Sound.RocketLaunch, Position);
        _direction = -_direction;
        _sprite.FlipH = _direction < 0;
    }
}
