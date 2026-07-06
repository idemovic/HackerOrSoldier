using Godot;

namespace HackerOrSoldier;

// A colored door that blocks the way until the agent touches it while
// carrying the key of the same color. PlaySession decides when it opens;
// this node is only the solid body and the visual.
public partial class LockedDoor : StaticBody2D
{
    public string ColorName { get; set; } = "red";

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(tile, tile) },
        });
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.Door, ColorName) });
    }
}
