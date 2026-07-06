using Godot;

namespace HackerOrSoldier;

// README: an electric door cyclically (10s) engages an electric field that
// takes a life. The field is on during the last 2 seconds of each cycle,
// same phase as the spikes.
public partial class ElectricDoorField : Node2D
{
    public PlaySession Session { get; set; }

    private ColorRect _field;

    public bool Active => Session.SpikesUp;

    public override void _Ready()
    {
        foreach (float x in new[] { -14f, 10f })
            AddChild(new ColorRect   // door posts
            {
                Position = new Vector2(x, -16),
                Size = new Vector2(4, 32),
                Color = new Color(0.55f, 0.55f, 0.35f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        _field = new ColorRect
        {
            Position = new Vector2(-10, -14),
            Size = new Vector2(20, 28),
            Color = new Color(0.95f, 0.95f, 0.40f, 0.75f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_field);
    }

    public override void _PhysicsProcess(double delta)
    {
        _field.Visible = Active;
        if (!Active || Session.Agent.IsHiding)
            return;
        var d = (Session.Agent.Position - Position).Abs();
        if (d.X < 26 && d.Y < 28)
            Session.AgentHit();
    }
}
