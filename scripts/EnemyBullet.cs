using Godot;

namespace HackerOrSoldier;

// Hostile projectile (pistol bullet, minigun bullet or plasma shot).
public partial class EnemyBullet : Node2D
{
    private static readonly float Speed = 2f * Agent.RunSpeed;   // README: bullets and plasma move at 2x agent speed

    public PlaySession Session { get; set; }
    public Vector2 Direction { get; set; } = Vector2.Right;
    public Color Tint { get; set; } = new(0.4f, 0.9f, 0.9f);

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-3, -3),
            Size = new Vector2(6, 6),
            Color = Tint,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += Direction * Speed * (float)delta;

        var d = (Session.Agent.Position - Position).Abs();
        if (!Session.Agent.IsHiding && d.X < 18 && d.Y < 18)
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
