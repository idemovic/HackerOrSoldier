using Godot;

namespace HackerOrSoldier;

// Turns off its circuit when hit by a bullet (README: "switches off laser
// beam / floor on hit"). Bullet collision is handled in Bullet.cs.
public partial class WallSwitch : Node2D
{
    public string Number { get; set; } = "1";
    public bool Activated { get; private set; }

    private Sprite2D _sprite;

    public override void _Ready()
    {
        _sprite = new Sprite2D { Texture = Sprites.Get(ObjectDefs.Switch) };
        AddChild(_sprite);
        AddChild(new Label
        {
            Text = Number,
            Position = new Vector2(-4, -28),
            Theme = new Theme { DefaultFontSize = 10 },
        });
    }

    public void Activate()
    {
        Activated = true;
        _sprite.Modulate = new Color(0.5f, 0.5f, 0.5f);
    }
}
