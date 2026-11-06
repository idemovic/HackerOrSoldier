using Godot;

namespace HackerOrSoldier;

public partial class Bullet : Node2D
{
    private static readonly float Speed = 2f * Agent.RunSpeed;   // README: bullets move at 2x agent speed

    public PlaySession Session { get; set; }
    public int Direction { get; set; } = 1;
    // Where the shot came from. The bullet spawns at the muzzle, ahead of the
    // shooter, so its first step sweeps back to here: a switch the agent
    // stands on is still hit. Defaults to the spawn position.
    public float FromX { get; set; } = float.NaN;

    public override void _Ready()
    {
        if (float.IsNaN(FromX))
            FromX = Position.X;
        AddChild(new Sprite2D { Texture = Sprites.Get("bullet") });
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += new Vector2(Direction * Speed * (float)delta, 0);
        float sweptFrom = FromX;
        FromX = Position.X;
        foreach (var enemy in Session.Enemies)
        {
            if (enemy.Hitbox.Grow(4).HasPoint(Position))
            {
                QueueFree();
                Session.RegisterHit();
                enemy.TakeHit();
                return;
            }
        }
        foreach (var wallSwitch in Session.Switches)
        {
            if (wallSwitch.Activated)
                continue;
            float dx = wallSwitch.Position.X - Mathf.Clamp(wallSwitch.Position.X, Mathf.Min(sweptFrom, Position.X), Mathf.Max(sweptFrom, Position.X));
            if (Mathf.Abs(dx) < 18 && Mathf.Abs(wallSwitch.Position.Y - Position.Y) < 18)
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
            if (TileDefs.IsDestructible(id))
                Session.DestroyTile(cell);
            else
                Sfx.PlayAt(Sound.BulletHit, Position);
            QueueFree();
        }
    }
}
