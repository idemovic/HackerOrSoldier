using Godot;

namespace HackerOrSoldier;

// Turns off its circuit when hit by a bullet (README: "switches off laser
// beam / floor on hit"). Bullet collision is handled in Bullet.cs.
public partial class WallSwitch : Node2D
{
    public string Number { get; set; } = "1";
    public bool Activated { get; private set; }

    private ColorRect _lever;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            Position = new Vector2(-8, -10),
            Size = new Vector2(16, 20),
            Color = new Color(0.45f, 0.35f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _lever = new ColorRect
        {
            Position = new Vector2(-3, -8),
            Size = new Vector2(6, 10),
            Color = new Color(0.95f, 0.85f, 0.30f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_lever);
        AddChild(new Label
        {
            Text = Number,
            Position = new Vector2(-4, -22),
            Theme = new Theme { DefaultFontSize = 10 },
        });
    }

    public void Activate()
    {
        Activated = true;
        _lever.Position = new Vector2(-3, -2);
        _lever.Color = new Color(0.35f, 0.35f, 0.30f);
    }
}
