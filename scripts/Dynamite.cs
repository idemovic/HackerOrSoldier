using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Dropped by drones or placed by the agent. Falls until it rests on top of a
// solid tile, sputtering sparks from its fuse, then the fuse runs out and
// PlaySession handles the explosion.
public partial class Dynamite : Node2D
{
    private const float Fuse = 2.5f;   // README: dynamite explodes in 2.5 seconds
    private const float Gravity = 1200f;
    private const float HalfHeight = 8f;   // the stick sprite is 16px tall, centred
    private static readonly Vector2 FuseTip = new(0.5f, -7.5f);   // the top fuse pixel
    private const float SparkStep = 1f / 30f;

    private static readonly Color[] SparkColors =
    {
        new("f2f2f2"), new("f2d43b"), new("f2d43b"), new("f28022"),
    };

    public PlaySession Session { get; set; }

    private struct Spark
    {
        public Vector2 Pos, Vel;
        public float Age, Life;
    }

    private readonly List<Spark> _sparks = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _timer = Fuse;
    private float _fallSpeed;
    private float _sparkClock;

    public override void _Ready()
    {
        _rng.Randomize();
        // Behind the parent's _Draw, so the sparks show over the stick.
        AddChild(new Sprite2D { Texture = Sprites.Get("dynamite"), ShowBehindParent = true });
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Fall(dt);
        UpdateSparks(dt);
        _timer -= dt;
        if (_timer <= 0)
        {
            QueueFree();
            Session.Explode(Position);
        }
    }

    // A fast fall covers more than a pixel per frame, so after each step the
    // stick is put back on top of whatever tile its bottom ended up inside.
    private void Fall(float dt)
    {
        if (SolidAt(Position.Y + HalfHeight))
        {
            _fallSpeed = 0;
            return;
        }
        _fallSpeed += Gravity * dt;
        Position += new Vector2(0, _fallSpeed * dt);
        float bottom = Position.Y + HalfHeight - 0.01f;
        if (SolidAt(bottom))
        {
            int tile = TileSetBuilder.TileSize;
            Position = new Vector2(Position.X, Mathf.Floor(bottom / tile) * tile - HalfHeight);
            _fallSpeed = 0;
        }
    }

    private bool SolidAt(float y) =>
        TileDefs.Get(Session.TileAtPosition(new Vector2(Position.X, y)))?.Solid == true;

    private void UpdateSparks(float dt)
    {
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += dt;
            if (s.Age >= s.Life)
            {
                _sparks.RemoveAt(i);
                continue;
            }
            s.Vel += new Vector2(0, 260f) * dt;
            s.Pos += s.Vel * dt;
            _sparks[i] = s;
        }
        for (int n = _rng.RandiRange(1, 2); n > 0; n--)
            _sparks.Add(new Spark
            {
                Pos = FuseTip,
                Vel = new Vector2(_rng.RandfRange(-45f, 45f), _rng.RandfRange(-80f, -20f)),
                Life = _rng.RandfRange(0.1f, 0.25f),
            });
        _sparkClock += dt;
        QueueRedraw();
    }

    public override void _Draw()
    {
        int flicker = (int)(_sparkClock / SparkStep);
        // The burning tip: a hot core that flares on the flicker clock.
        DrawRect(new Rect2(FuseTip - new Vector2(1.5f, 1.5f), new Vector2(3, 3)), new Color("f2d43b"));
        if (flicker % 2 == 0)
            DrawRect(new Rect2(FuseTip - new Vector2(2.5f, 0.5f), new Vector2(5, 1)), new Color("f28022"));
        else
            DrawRect(new Rect2(FuseTip - new Vector2(0.5f, 2.5f), new Vector2(1, 5)), new Color("f28022"));
        DrawRect(new Rect2(FuseTip - new Vector2(0.5f, 0.5f), Vector2.One), new Color("f2f2f2"));

        for (int i = 0; i < _sparks.Count; i++)
        {
            var s = _sparks[i];
            float age = s.Age / s.Life;
            var color = SparkColors[Mathf.Min((int)(age * SparkColors.Length), SparkColors.Length - 1)];
            var at = new Vector2(Mathf.Floor(s.Pos.X), Mathf.Floor(s.Pos.Y));
            DrawRect(new Rect2(at, Vector2.One), color);
        }
    }
}
