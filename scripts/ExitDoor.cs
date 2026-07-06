using Godot;

namespace HackerOrSoldier;

public partial class ExitDoor : Area2D
{
    [Signal]
    public delegate void ReachedEventHandler();

    public override void _Ready()
    {
        CollisionMask = 2;   // the agent's collision layer
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(32, 32) },
        });
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.ExitDoor) });
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Agent)
            EmitSignal(SignalName.Reached);
    }
}
