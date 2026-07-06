using Godot;

namespace HackerOrSoldier;

// Passive hiding spot. PlaySession toggles the agent's hidden state when the
// activate action is pressed next to a closet.
public partial class Closet : Node2D
{
    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-12, -16),
            Size = new Vector2(24, 32),
            Color = new Color(0.50f, 0.33f, 0.18f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddChild(new ColorRect   // door seam
        {
            Position = new Vector2(-1, -14),
            Size = new Vector2(2, 28),
            Color = new Color(0.32f, 0.20f, 0.10f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }
}
