using Godot;

namespace HackerOrSoldier;

// One teleport of a pair (README: entering one makes the agent appear in the
// other): a floor pad with an emitter over the agent's head. Teleports pair
// by number, shown as a colored line under the pad; PlaySession handles the
// actual teleporting and calls Open on both ends, which swirls a portal
// between the two platforms.
public partial class Teleporter : Node2D
{
    private const float PortalTime = 0.7f;

    // The open space between the platforms, relative to the cell centre.
    private const float PortalTop = -18f;
    private const float PortalBottom = 8f;

    public string Number { get; set; } = "1";

    private float _portal;

    public override void _Ready()
    {
        var texture = Sprites.Get(ObjectDefs.Teleport);
        AddChild(new Sprite2D
        {
            Texture = texture,
            Offset = new Vector2(0, 16 - texture.GetHeight() / 2f),
        });
        AddChild(new CircuitMark { Number = Number });
    }

    public void Open()
    {
        _portal = PortalTime;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_portal <= 0f)
            return;
        _portal = Mathf.Max(0f, _portal - (float)delta);
        QueueRedraw();
    }

    // An oval that opens and closes over PortalTime, filled with rings that
    // shrink toward the centre so it reads as a swirl.
    public override void _Draw()
    {
        if (_portal <= 0f)
            return;
        float t = 1f - _portal / PortalTime;
        float open = Mathf.Sin(t * Mathf.Pi);
        var centre = new Vector2(0, (PortalTop + PortalBottom) / 2f);
        float halfHeight = (PortalBottom - PortalTop) / 2f * open;
        float halfWidth = 11f * open;
        if (halfHeight < 1f)
            return;

        var outer = new Color("7a3be0");
        var inner = new Color("45d6d6");
        DrawSetTransform(centre, 0f, new Vector2(halfWidth / halfHeight, 1f));
        DrawCircle(Vector2.Zero, halfHeight, outer with { A = 0.55f });
        for (int i = 0; i < 3; i++)
        {
            float ring = Mathf.PosMod(1f - t * 3f + i / 3f, 1f);
            DrawArc(Vector2.Zero, halfHeight * ring, 0f, Mathf.Tau, 24,
                outer.Lerp(inner, 1f - ring) with { A = 0.9f }, 2f);
        }
        DrawCircle(Vector2.Zero, halfHeight * 0.3f, Colors.White with { A = 0.8f * open });
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
