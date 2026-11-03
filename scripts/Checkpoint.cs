using Godot;

namespace HackerOrSoldier;

// A flag post. Touching it makes it the agent's respawn point; the session
// lights the latest one and dims the one before.
public partial class Checkpoint : Area2D
{
    [Signal]
    public delegate void ReachedEventHandler(Checkpoint checkpoint);

    private Sprite2D _sprite;
    private bool _lit;

    public bool Lit
    {
        get => _lit;
        set
        {
            _lit = value;
            _sprite.Texture = Sprites.Get(value ? "checkpoint_lit" : ObjectDefs.Checkpoint);
        }
    }

    public override void _Ready()
    {
        CollisionMask = 2;   // the agent's collision layer
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(32, 32) },
        });
        _sprite = new Sprite2D { Texture = Sprites.Get(ObjectDefs.Checkpoint) };
        AddChild(_sprite);
        BodyEntered += body =>
        {
            if (body is Agent)
                EmitSignal(SignalName.Reached, this);
        };
    }
}
