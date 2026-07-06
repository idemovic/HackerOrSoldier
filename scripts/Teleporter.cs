using Godot;

namespace HackerOrSoldier;

// One capsule of a teleport pair (README: two joined capsules; entering one
// makes the agent appear in the other). Capsules pair by number property;
// PlaySession handles the actual teleporting.
public partial class Teleporter : Node2D
{
    public string Number { get; set; } = "1";

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-10, -16),
            Size = new Vector2(20, 32),
            Color = new Color(0.45f, 0.30f, 0.90f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddChild(new ColorRect
        {
            Position = new Vector2(-6, -12),
            Size = new Vector2(12, 24),
            Color = new Color(0.70f, 0.55f, 1.00f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddChild(new Label
        {
            Text = Number,
            Position = new Vector2(-4, -28),
            Theme = new Theme { DefaultFontSize = 10 },
        });
    }
}
