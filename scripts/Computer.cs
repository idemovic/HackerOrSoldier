using Godot;

namespace HackerOrSoldier;

// Hacked automatically when the agent walks up carrying the USB key with the
// matching number (PlaySession polls proximity). Hacking turns off the
// computer's circuit.
public partial class Computer : Node2D
{
    public string Number { get; set; } = "1";
    public bool Hacked { get; private set; }

    private Sprite2D _sprite;

    public override void _Ready()
    {
        _sprite = new Sprite2D { Texture = Sprites.Get(ObjectDefs.Computer) };
        AddChild(_sprite);
        AddChild(new CircuitMark { Number = Number });
    }

    public void SetHacked()
    {
        Hacked = true;
        _sprite.Modulate = new Color(0.5f, 0.65f, 0.5f);
    }
}
