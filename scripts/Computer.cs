using Godot;

namespace HackerOrSoldier;

// Hacked automatically when the agent walks up carrying the USB key with the
// matching number (PlaySession polls proximity). Hacking turns off the
// computer's circuit.
public partial class Computer : Node2D
{
    public string Number { get; set; } = "1";
    public bool Hacked { get; private set; }

    private ColorRect _screen;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-12, -6),
            Size = new Vector2(24, 22),
            Color = new Color(0.35f, 0.37f, 0.42f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _screen = new ColorRect
        {
            Position = new Vector2(-9, -3),
            Size = new Vector2(18, 12),
            Color = new Color(0.25f, 0.85f, 0.85f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_screen);
        AddChild(new Label
        {
            Text = Number,
            Position = new Vector2(-4, -18),
            Theme = new Theme { DefaultFontSize = 10 },
        });
    }

    public void SetHacked()
    {
        Hacked = true;
        _screen.Color = new Color(0.15f, 0.35f, 0.20f);
    }
}
