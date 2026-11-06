using Godot;

namespace HackerOrSoldier;

// A colored door that blocks the way until the agent touches it while
// carrying the key of the same color. PlaySession decides when it opens;
// this node is only the solid body and the visual.
public partial class LockedDoor : SlidingDoor
{
    public string ColorName { get; set; } = "red";

    protected override Texture2D PanelTexture => Sprites.Get(ObjectDefs.Door, ColorName);
}
