using Godot;

namespace HackerOrSoldier;

// Hostile projectile (pistol bullet, minigun bullet, plasma shot or rocket).
public partial class EnemyBullet : Node2D
{
    public PlaySession Session { get; set; }
    public Vector2 Direction { get; set; } = Vector2.Right;
    public string SpriteKey { get; set; } = "plasma";
    public float Speed { get; set; } = 2f * Agent.RunSpeed;   // README: bullets and plasma move at 2x agent speed

    public override void _Ready() =>
        AddChild(new Sprite2D { Texture = Sprites.Get(SpriteKey), FlipH = Direction.X < 0 });

    public override void _PhysicsProcess(double delta)
    {
        Position += Direction * Speed * (float)delta;

        if (!Session.Agent.IsHiding && Session.Agent.Hitbox.Grow(3).HasPoint(Position))
        {
            QueueFree();
            Session.AgentHit();
            return;
        }

        int tile = TileSetBuilder.TileSize;
        var cell = new Vector2I(
            (int)Mathf.Floor(Position.X / tile),
            (int)Mathf.Floor(Position.Y / tile));
        if (!Session.InLevel(cell) || TileDefs.Get(Session.TileAt(cell.X, cell.Y))?.Solid == true)
            QueueFree();
    }
}
