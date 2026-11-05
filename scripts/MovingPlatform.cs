using System.Collections.Generic;
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
    // Offsets of the touching cells that make up this platform, so adjacent
    // pieces move as one body. Defaults to a single cell.
    public List<Vector2> Cells { get; set; } = new() { Vector2.Zero };

    private Vector2 _origin;
    private int _direction;
    private float _speed;
    private float _delay;

    public override void _Ready()
    {
        SyncToPhysics = true;
        CollisionLayer = 1;
        CollisionMask = 0;
        _origin = Position;
        _direction = Vertical ? -1 : 1;   // elevators start upward, platforms rightward
        // Speed and start delay vary per platform so neighbours drift out of
        // sync. Seeded from the start position, so a level always plays the same.
        var rng = new RandomNumberGenerator { Seed = (ulong)GD.Hash(_origin) };
        _speed = Speed * rng.RandfRange(0.85f, 1.15f);
        _delay = rng.RandfRange(0f, 1.5f);
        foreach (var cell in Cells)
        {
            AddChild(new CollisionShape2D
            {
                Position = cell + new Vector2(0, -4),
                Shape = new RectangleShape2D { Size = new Vector2(32, 12) },
            });
            AddChild(new Sprite2D
            {
                Texture = Sprites.Get("platform"),
                Position = cell,
                Offset = new Vector2(0, -4),
                Modulate = Vertical ? new Color(0.85f, 0.9f, 1f) : Colors.White,
            });
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_delay > 0)
        {
            _delay -= (float)delta;
            return;
        }
        var axis = Vertical ? Vector2.Down : Vector2.Right;
        var next = Position + axis * (_direction * _speed * (float)delta);
        float offset = (next - _origin).Dot(axis);
        bool blocked = Mathf.Abs(offset) > Range;
        foreach (var cell in Cells)
        {
            var probe = next + cell + new Vector2(0, -4) + axis * (_direction * 16);
            blocked |= TileDefs.Get(Session.TileAtPosition(probe))?.Solid == true;
        }
        if (blocked)
            _direction = -_direction;
        else
            Position = next;
    }
}
