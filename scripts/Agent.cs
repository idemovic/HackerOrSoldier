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
    public static readonly float RunSpeed = JumpDistance / (2f * TimeToApex);

    // README water spec: submerged vertical speed is capped by water
    // resistance; a jump from the surface line gets the full jump velocity.
    private const float WaterGravityFactor = 0.4f;
    private const float SwimSpeed = 120f;
    private const float SinkSpeed = 80f;

    private const float ElectricInterval = 3f;

    // Hazard overlap is sampled slightly inside the 30x30 collision box so
    // standing next to a hazard tile does not count as touching it.
    private const float HazardExtent = 14f;

    public PlaySession Session { get; set; }
    public int Facing { get; private set; } = 1;
    public bool IsHiding { get; private set; }
    public string AnimationFrame => _animator?.Frame;

    private float _electricTime;
    private Sprite2D _sprite;
    private AgentAnimator _animator;

    public override void _Ready()
    {
        _sprite = new Sprite2D { Texture = Sprites.Get("agent") };
        AddChild(_sprite);
        _animator = new AgentAnimator(_sprite);
    }

    public void ResetForRespawn(Vector2 position)
    {
        Position = position;
        Velocity = Vector2.Zero;
        _electricTime = 0f;
        _animator.Reset();
    }

    public void SetHiding(bool hiding)
    {
        IsHiding = hiding;
        Modulate = hiding ? new Color(1, 1, 1, 0.35f) : Colors.White;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (Session != null && Input.IsActionJustPressed("activate"))
            Session.ActivateAction();
        if (IsHiding)
        {
            Velocity = Vector2.Zero;
            _animator.Update(dt, Vector2.Zero, true, false, Facing);
            return;
        }

        Vector2 velocity = Velocity;

        bool submerged = TileAt(0, 0) == TileDefs.Water;
        bool atSurface = submerged && TileAt(0, -Tile) != TileDefs.Water;

        velocity.Y += Gravity * dt * (submerged ? WaterGravityFactor : 1f);

        if (Input.IsActionJustPressed("jump"))
        {
            if (submerged)
                velocity.Y = atSurface ? JumpVelocity : -SwimSpeed;
            else if (IsOnFloor())
                velocity.Y = JumpVelocity;
        }

        if (submerged)
            velocity.Y = atSurface
                ? Mathf.Min(velocity.Y, SinkSpeed)                // full jumps may leave the water
                : Mathf.Clamp(velocity.Y, -SwimSpeed, SinkSpeed); // deep water caps both directions

        float axis = Input.GetAxis("move_left", "move_right");
        if (axis != 0)
            Facing = axis > 0 ? 1 : -1;
        velocity.X = axis * RunSpeed * (submerged ? 0.5f : 1f);

        Velocity = velocity;
        MoveAndSlide();

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is Barrel barrel && Mathf.Abs(collision.GetNormal().X) > 0.9f)
                barrel.Push(collision.GetNormal().X < 0 ? 1 : -1);   // the normal points back at the agent
        }

        // The shoot pose only plays when a shot actually left the weapon, so
        // an empty magazine never flashes a muzzle.
        if (Session != null && Input.IsActionJustPressed("shoot") && Session.TryShoot(this))
            _animator.Shoot();

        _animator.Update(dt, Velocity, IsOnFloor(), submerged, Facing);

        if (Session != null)
            CheckHazards(dt);
    }

    private int TileAt(float dx, float dy) =>
        Session?.TileAtPosition(Position + new Vector2(dx, dy)) ?? TileDefs.Empty;

    private void CheckHazards(float dt)
    {
        // Kill plane below the level bounds.
        if (Position.Y > Session.LevelBottom + 2f * Tile)
        {
            Session.AgentHit();
            return;
        }

        int minX = (int)Mathf.Floor((Position.X - HazardExtent) / Tile);
        int maxX = (int)Mathf.Floor((Position.X + HazardExtent) / Tile);
        int minY = (int)Mathf.Floor((Position.Y - HazardExtent) / Tile);
        int maxY = (int)Mathf.Floor((Position.Y + HazardExtent) / Tile);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int id = Session.TileAt(x, y);
                if (id == TileDefs.Lava || id == TileDefs.Spear || (id == TileDefs.Spikes && Session.SpikesUp))
                {
                    Session.AgentHit();
                    return;
                }
            }

        bool onElectric = false;
        if (IsOnFloor())
        {
            int below = (int)Mathf.Floor((Position.Y + HazardExtent + 3f) / Tile);
            for (int x = minX; x <= maxX; x++)
            {
                int id = Session.TileAt(x, below);
                if (id == TileDefs.ElectricFloor && !Session.ElectricFloorsOff)
                    onElectric = true;
                else if (id == TileDefs.AlarmFloor)
                    Session.TriggerAlarm();   // README: alarm floor triggers on step
            }
        }
        if (!onElectric)
        {
            _electricTime = 0f;
            return;
        }
        _electricTime += dt;
        if (_electricTime >= ElectricInterval)
        {
            _electricTime = 0f;
            Session.AgentHit();
        }
    }
}
