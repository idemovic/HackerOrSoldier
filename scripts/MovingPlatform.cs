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

    // The deck rides this far above its cell's center; the editor draws its
    // marker at the same height.
    public const float DeckOffsetY = -4f;

    public PlaySession Session { get; set; }
    public bool Vertical { get; set; }
    // The touching cells that make up this platform, so adjacent pieces move
    // as one body; each keeps its own paint. Defaults to a single steel cell.
    public List<PlatformCell> Cells { get; set; } = new() { new(Vector2.Zero, ObjectDefs.PlatformPaints[0]) };

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
                Position = cell.Offset + new Vector2(0, DeckOffsetY),
                Shape = new RectangleShape2D { Size = new Vector2(32, 12) },
            });
            AddChild(new Sprite2D
            {
                Texture = Sprites.Get("platform", cell.Paint),
                Position = cell.Offset,
                Offset = new Vector2(0, DeckOffsetY),
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
            var probe = next + cell.Offset + new Vector2(0, DeckOffsetY) + axis * (_direction * 16);
            blocked |= TileDefs.Get(Session.TileAtPosition(probe))?.Solid == true;
        }
        if (blocked)
            _direction = -_direction;
        else
            Position = next;
    }
}

// One cell of a platform body: its offset from the body origin and its paint.
public readonly record struct PlatformCell(Vector2 Offset, string Paint);
