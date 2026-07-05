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
        AddChild(new ColorRect
        {
            Position = new Vector2(-16, -16),
            Size = new Vector2(32, 32),
            Color = new Color(0.2f, 0.9f, 0.4f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Agent)
            EmitSignal(SignalName.Reached);
    }
}
