using Godot;

namespace HackerOrSoldier;

// A gain or loss rising off the agent's head: the stat's HUD icon and a
// signed number climb a short way, hold, then blink away in the same DOS
// idiom as the wall debris. It rides along with the agent so a running
// player can still read it. When another one starts while it is up, it
// steps a line higher so the two never overprint. Purely visual:
// PlaySession has already applied the change.
public partial class FloatingNumber : Node2D
{
    private const float HeadHeight = 26f;   // from the agent's centre to just above its head
    private const float Rise = 22f;
    private const float RiseTime = 0.5f;
    private const float BlinkFrom = 0.75f;
    private const float Duration = 1.05f;
    private const float FlickerStep = 1f / 30f;
    private const float LineHeight = 12f;
    private const float StepSpeed = 120f;   // px/s while making room for a newer one
    private const int FontSize = 8;          // PixelFont's 1x size
    private const int Gap = 2;

    public Node2D Follow { get; init; }
    public Texture2D Icon { get; init; }
    public int Amount { get; init; }
    public Color Color { get; init; } = Colors.White;

    private string _text;
    private Vector2 _anchor;
    private float _time;
    private float _lift;
    private float _shownLift;

    public override void _Ready()
    {
        ZIndex = 2;   // over the agent, enemies, pickups and wall debris
        _text = (Amount > 0 ? "+" : "") + Amount;
        _anchor = Position;
    }

    // A newer number is starting below this one.
    public void MakeRoom() => _lift += LineHeight;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (_time >= Duration)
        {
            QueueFree();
            return;
        }
        if (IsInstanceValid(Follow))
            _anchor = Follow.Position;
        _shownLift = Mathf.MoveToward(_shownLift, _lift, StepSpeed * dt);
        float eased = 1f - Mathf.Pow(1f - Mathf.Min(_time / RiseTime, 1f), 2f);
        Position = (_anchor + new Vector2(0, -HeadHeight - Rise * eased - _shownLift)).Round();
        Visible = _time < BlinkFrom || (int)((_time - BlinkFrom) / FlickerStep) % 2 == 0;
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        float textWidth = font.GetStringSize(_text, HorizontalAlignment.Left, -1, FontSize).X;
        int iconWidth = Icon?.GetWidth() ?? 0;
        float left = Mathf.Round(-(iconWidth + (Icon != null ? Gap : 0) + textWidth) / 2f);
        if (Icon != null)
            DrawTexture(Icon, new Vector2(left, -Icon.GetHeight() / 2));

        // A 1px black rim keeps the number legible over any tile.
        var baseline = new Vector2(left + iconWidth + (Icon != null ? Gap : 0), 4);
        foreach (var step in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
            DrawString(font, baseline + step, _text, HorizontalAlignment.Left, -1, FontSize, Colors.Black);
        DrawString(font, baseline, _text, HorizontalAlignment.Left, -1, FontSize, Color);
    }
}
