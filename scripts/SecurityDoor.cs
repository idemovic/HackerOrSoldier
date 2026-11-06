using Godot;

namespace HackerOrSoldier;

// A steel door wired to a circuit: closed and solid while the circuit is on,
// open for good once it goes off. Colored keys do not open it. PlaySession
// calls Open when the circuit turns off.
public partial class SecurityDoor : SlidingDoor
{
    public string Number { get; set; } = "1";

    private Label _label;

    protected override Texture2D PanelTexture => Sprites.Get(ObjectDefs.SecurityDoor);

    public override void _Ready()
    {
        base._Ready();
        _label = new Label
        {
            Text = Number,
            Position = new Vector2(-4, -30),
            Theme = new Theme { DefaultFontSize = 10 },
        };
        AddChild(_label);
    }

    protected override void OnOpened()
    {
        _label.Visible = false;
        // A faint frame stays so the route reads once the panel is gone.
        AddBehindPanel(new Sprite2D { Texture = PanelTexture, Modulate = new Color(1, 1, 1, 0.25f) });
    }
}
