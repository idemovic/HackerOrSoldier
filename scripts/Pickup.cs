using Godot;

namespace HackerOrSoldier;

// Play-mode stand-in for any collectible level object (keys, health kits,
// ammo packs, vests, briefcases, goggles). PlaySession applies the effect.
public partial class Pickup : Area2D
{
    [Signal]
    public delegate void CollectedEventHandler(Pickup pickup);

    public LevelObject Source { get; set; }

    // True for pickups placed in the level (counts toward completeness),
    // false for drops and tombstones spawned during play.
    public bool FromLevel { get; set; }

    public override void _Ready()
    {
        CollisionMask = 2;   // the agent's collision layer
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(24, 24) },
        });
        Source.Properties.TryGetValue("color", out var colorName);
        AddChild(new Sprite2D { Texture = Sprites.ForObject(Source.Type, colorName) });
        BodyEntered += body =>
        {
            if (body is Agent)
                EmitSignal(SignalName.Collected, this);
        };
    }
}
