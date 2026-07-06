using Godot;

namespace HackerOrSoldier;

// Passive hiding spot. PlaySession toggles the agent's hidden state when the
// activate action is pressed next to a closet.
public partial class Closet : Node2D
{
    public override void _Ready() =>
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.Closet) });
}
