using Godot;

namespace HackerOrSoldier;

public partial class Agent : CharacterBody2D
{
    private const float Tile = 32f;

    // README movement spec: jumps 2.5 tiles high, up to 5 tiles sideways.
    // Gravity, jump velocity and run speed are derived from those two
    // constraints plus the chosen time to reach the jump apex.
    private const float JumpHeight = 2.5f * Tile;
    private const float JumpDistance = 5f * Tile;
    private const float TimeToApex = 0.35f;

    private static readonly float Gravity = 2f * JumpHeight / (TimeToApex * TimeToApex);
    private static readonly float JumpVelocity = -Gravity * TimeToApex;
    private static readonly float RunSpeed = JumpDistance / (2f * TimeToApex);

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;
        velocity.Y += Gravity * (float)delta;

        if (IsOnFloor() && Input.IsActionJustPressed("jump"))
            velocity.Y = JumpVelocity;

        velocity.X = Input.GetAxis("move_left", "move_right") * RunSpeed;

        Velocity = velocity;
        MoveAndSlide();
    }
}
