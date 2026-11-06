using Godot;

namespace HackerOrSoldier;

// A steel door wired to a circuit: closed and solid while the circuit is on,
// open for good once it goes off. Colored keys do not open it. PlaySession
// calls Open when the circuit turns off.
public partial class SecurityDoor : SlidingDoor
{
    public string Number { get; set; } = "1";

    private CircuitMark _mark;

    protected override Texture2D PanelTexture => Sprites.Get(ObjectDefs.SecurityDoor);

    public override void _Ready()
    {
        base._Ready();
        _mark = new CircuitMark { Number = Number };
        AddChild(_mark);
    }

    protected override void OnOpened() => _mark.Visible = false;
}
