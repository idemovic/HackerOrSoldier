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
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.Teleport) });
        AddChild(new Label
        {
            Text = Number,
            Position = new Vector2(-4, -30),
            Theme = new Theme { DefaultFontSize = 10 },
        });
    }
}
