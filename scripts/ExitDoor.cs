using Godot;

namespace HackerOrSoldier;

// The level exit. Locked until every admin password in the level is taken:
// a red lamp shows while locked, a green one once open.
public partial class ExitDoor : Area2D
{
    [Signal]
    public delegate void ReachedEventHandler();

    private const float HintTime = 1.6f;
    public const string HintText = "Find the admin password";

    private static ImageTexture _lampRed;
    private static ImageTexture _lampGreen;

    public bool Locked { get; private set; }
    public bool HintShown => _hintTime > 0f;

    private Sprite2D _sprite;
    private Sprite2D _lamp;
    private Label _hint;
    private float _hintTime;

    public override void _Ready()
    {
        CollisionMask = 2;   // the agent's collision layer
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(32, 32) },
        });
        _sprite = new Sprite2D { Texture = Sprites.Get(ObjectDefs.ExitDoor) };
        AddChild(_sprite);
        _lamp = new Sprite2D { Centered = false, Position = new Vector2(-7, -2) };
        AddChild(_lamp);
        ShowLamp();
        _hint = new Label
        {
            Text = HintText,
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-70, -34),
            Size = new Vector2(140, 10),
            ZIndex = 2,
        };
        _hint.AddThemeFontSizeOverride("font_size", 8);
        _hint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hint.AddThemeConstantOverride("outline_size", 4);
        AddChild(_hint);
        BodyEntered += OnBodyEntered;
    }

    public void SetLocked(bool locked)
    {
        if (Locked == locked)
            return;
        Locked = locked;
        if (_sprite != null)
            _sprite.Modulate = locked ? new Color(0.62f, 0.62f, 0.62f) : Colors.White;
        ShowLamp();
        if (locked)
            return;
        // The agent may already be standing in the doorway; BodyEntered will not fire again.
        foreach (var body in GetOverlappingBodies())
            if (body is Agent)
                EmitSignal(SignalName.Reached);
    }

    private void ShowLamp()
    {
        _lampRed ??= Lamp(new Color("e03b2f"));
        _lampGreen ??= Lamp(new Color("6cf27a"));
        _lamp.Texture = Locked ? _lampRed : _lampGreen;
    }

    // A 7x7 lamp: dark rim, lit body, a highlight.
    private static ImageTexture Lamp(Color lit)
    {
        var img = Image.CreateEmpty(7, 7, false, Image.Format.Rgba8);
        img.Fill(new Color("141419"));
        img.FillRect(new Rect2I(1, 1, 5, 5), lit);
        img.FillRect(new Rect2I(2, 2, 2, 1), Colors.White.Lerp(lit, 0.5f));
        return ImageTexture.CreateFromImage(img);
    }

    public override void _Process(double delta)
    {
        if (_hintTime <= 0f)
            return;
        _hintTime -= (float)delta;
        _hint.Visible = _hintTime > 0f;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Agent)
            return;
        if (!Locked)
        {
            EmitSignal(SignalName.Reached);
            return;
        }
        if (_hintTime > 0f)
            return;   // already showing; do not spam the sound
        _hintTime = HintTime;
        _hint.Visible = true;
        Sfx.Play(Sound.CantAfford);
    }
}
