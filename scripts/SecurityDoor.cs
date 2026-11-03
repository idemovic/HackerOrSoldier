using Godot;

namespace HackerOrSoldier;

// A steel door wired to a circuit: closed and solid while the circuit is on,
// open for good once it goes off. Colored keys do not open it. PlaySession
// calls Open when the circuit turns off.
public partial class SecurityDoor : StaticBody2D
{
    public string Number { get; set; } = "1";
    public bool IsOpen { get; private set; }

    private CollisionShape2D _shape;
    private Sprite2D _sprite;
    private Label _label;

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        _shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(tile, tile) } };
        AddChild(_shape);
        _sprite = new Sprite2D { Texture = Sprites.Get(ObjectDefs.SecurityDoor) };
        AddChild(_sprite);
        _label = new Label
        {
            Text = Number,
            Position = new Vector2(-4, -30),
            Theme = new Theme { DefaultFontSize = 10 },
        };
        AddChild(_label);
    }

    public void Open()
    {
        IsOpen = true;
        _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        _label.Visible = false;
        _sprite.Modulate = new Color(1, 1, 1, 0.25f);   // a faint frame stays so the route reads
    }
}
