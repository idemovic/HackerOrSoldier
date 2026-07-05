using Godot;

namespace HackerOrSoldier;

// Play-mode stand-in for any collectible level object (keys, health kits,
// ammo packs, vests, briefcases, goggles). PlaySession applies the effect.
public partial class Pickup : Area2D
{
    [Signal]
    public delegate void CollectedEventHandler(Pickup pickup);

    public LevelObject Source { get; set; }

    public override void _Ready()
    {
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(24, 24) },
        });
        var def = System.Array.Find(ObjectDefs.All, d => d.Type == Source.Type);
        var color = ObjectDefs.HasColorProperty(Source.Type) && Source.Properties.TryGetValue("color", out var name)
            ? ObjectDefs.KeyColor(name)
            : def?.Color ?? Colors.White;
        AddChild(new ColorRect
        {
            Position = new Vector2(-8, -8),
            Size = new Vector2(16, 16),
            Color = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        BodyEntered += body =>
        {
            if (body is Agent)
                EmitSignal(SignalName.Collected, this);
        };
    }
}
