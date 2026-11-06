using Godot;

namespace HackerOrSoldier;

// A one-tile door that opens by sinking into the floor. The panel's bottom
// edge stays on the tile's bottom, so the visible part shrinks from the top
// down and nothing draws below the tile.
public abstract partial class SlidingDoor : StaticBody2D
{
    private const float SlideSeconds = 0.4f;

    private CollisionShape2D _shape;
    private Sprite2D _panel;

    public bool IsOpen { get; private set; }

    // Pixels the panel has sunk: 0 closed, TileSize fully down.
    public float Drop { get; private set; }

    protected abstract Texture2D PanelTexture { get; }

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(tile, tile) } };
        AddChild(_shape);
        _panel = new Sprite2D { Texture = PanelTexture, RegionEnabled = true, RegionRect = new Rect2(0, 0, tile, tile) };
        AddChild(_panel);
    }

    public void Open()
    {
        if (IsOpen)
            return;
        IsOpen = true;
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);   // the agent must not be blocked mid-slide
        Sfx.PlayAt(Sound.DoorOpen, Position);
        OnOpened();
        CreateTween()
            .TweenMethod(Callable.From<float>(SetDrop), 0f, (float)TileSetBuilder.TileSize, SlideSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);   // starts slow and accelerates like a sci-fi door
    }

    protected virtual void OnOpened() { }

    // Puts a node under the sliding panel.
    protected void AddBehindPanel(Node node)
    {
        AddChild(node);
        MoveChild(node, _panel.GetIndex());
    }

    private void SetDrop(float drop)
    {
        int tile = TileSetBuilder.TileSize;
        Drop = drop;
        _panel.RegionRect = new Rect2(0, 0, tile, tile - drop);
        _panel.Position = new Vector2(0, drop / 2);
        _panel.Visible = drop < tile;
    }
}
