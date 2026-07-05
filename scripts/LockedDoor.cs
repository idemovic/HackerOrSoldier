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
        var color = ObjectDefs.KeyColor(ColorName);
        AddChild(new ColorRect
        {
            Position = new Vector2(-tile / 2f, -tile / 2f),
            Size = new Vector2(tile, tile),
            Color = color.Darkened(0.25f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddChild(new ColorRect   // keyhole accent so doors read differently from keys
        {
            Position = new Vector2(-3, -6),
            Size = new Vector2(6, 12),
            Color = color.Lightened(0.4f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }
}
