using Godot;

namespace HackerOrSoldier;

public partial class Bullet : Node2D
{
    private static readonly float Speed = 2f * Agent.RunSpeed;   // README: bullets move at 2x agent speed

    public PlaySession Session { get; set; }
    public int Direction { get; set; } = 1;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-4, -2),
            Size = new Vector2(8, 4),
            Color = new Color(1f, 0.9f, 0.4f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += new Vector2(Direction * Speed * (float)delta, 0);
        foreach (var enemy in Session.Enemies)
        {
            var d = (enemy.Position - Position).Abs();
            if (d.X < 18 && d.Y < 18)
            {
                QueueFree();
                enemy.TakeHit();
                return;
            }
        }
        foreach (var wallSwitch in Session.Switches)
        {
            if (wallSwitch.Activated)
                continue;
            var d = (wallSwitch.Position - Position).Abs();
            if (d.X < 18 && d.Y < 18)
            {
                QueueFree();
                wallSwitch.Activate();
                Session.TurnOffCircuit(wallSwitch.Number);   // README: switch turns off laser beam / floor on hit
                return;
            }
        }
        int tile = TileSetBuilder.TileSize;
        var cell = new Vector2I(
            (int)Mathf.Floor(Position.X / tile),
            (int)Mathf.Floor(Position.Y / tile));
        if (!Session.InLevel(cell))
        {
            QueueFree();
            return;
        }
        int id = Session.TileAt(cell.X, cell.Y);
        if (TileDefs.Get(id) is { Solid: true })
        {
            if (id == TileDefs.DestructibleWall)
                Session.DestroyTile(cell);
            QueueFree();
        }
    }
}
