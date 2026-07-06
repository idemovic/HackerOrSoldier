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
        var def = System.Array.Find(ObjectDefs.All, d => d.Type == Source.Type);
        var collectible = CollectibleDefs.Get(Source.Type);
        var color = ObjectDefs.HasColorProperty(Source.Type) && Source.Properties.TryGetValue("color", out var name)
            ? ObjectDefs.KeyColor(name)
            : def?.Color ?? collectible?.Color ?? Colors.White;
        AddChild(new ColorRect
        {
            Position = new Vector2(-8, -8),
            Size = new Vector2(16, 16),
            Color = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        if (collectible?.Letter != null)
            AddChild(new Label
            {
                Text = collectible.Letter,
                Position = new Vector2(-4, -10),
                Theme = new Theme { DefaultFontSize = 10 },
                Modulate = new Color(0.2f, 0.2f, 0.2f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        BodyEntered += body =>
        {
            if (body is Agent)
                EmitSignal(SignalName.Collected, this);
        };
    }
}
