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

    // The collision box fits the drawn body (about 11 px wide, legs reach
    // +-11 mid-run), so the agent stands against walls and drops off ledges
    // where it looks like it should. Full height keeps 1-tile corridors.
    public static readonly Vector2 HalfSize = new(8, 15);
    public Rect2 Hitbox => new(Position - HalfSize, HalfSize * 2);

    // README water spec: submerged vertical speed is capped by water
    // resistance; a jump from the surface line gets the full jump velocity.
    private const float WaterGravityFactor = 0.4f;
    private const float SwimSpeed = 120f;
    private const float SinkSpeed = 80f;

    private const float ElectricInterval = 3f;

    // Landing faster than a normal jump's return counts as a fall worth a thud.
    private static readonly float HardLanding = -JumpVelocity * 1.25f;
    private const float PushRearm = 0.3f;   // pushing flickers; only a pause re-arms the scrape

    // Hazard overlap is sampled slightly inside the collision box so
    // standing next to a hazard tile does not count as touching it.
    private static readonly Vector2 HazardExtent = HalfSize - Vector2.One;

    public PlaySession Session { get; set; }
    public int Facing { get; private set; } = 1;
    public bool IsHiding { get; private set; }
    public bool IsDead { get; private set; }
    public string AnimationFrame => _animator?.Frame;

    private float _electricTime;
    private float _pushIdle = PushRearm;
    private Sprite2D _sprite;
    private AgentAnimator _animator;

    public override void _Ready()
    {
        GetNode<CollisionShape2D>("CollisionShape2D").Shape = new RectangleShape2D { Size = HalfSize * 2 };
        _sprite = new Sprite2D { Texture = Sprites.Get("agent") };
        AddChild(_sprite);
        _animator = new AgentAnimator(_sprite);
    }

    // Loses a life: the agent stops taking input and crumples where it
    // stands. PlaySession holds it there and decides when to respawn it.
    public void Die()
    {
        if (IsDead)
            return;
        IsDead = true;
        Velocity = Vector2.Zero;
        _animator.Die();
    }

    public void ResetForRespawn(Vector2 position)
    {
        Position = position;
        Velocity = Vector2.Zero;
        _electricTime = 0f;
        IsDead = false;
        SetHiding(false);   // dying inside a closet must not respawn a ghost
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

        // A downed agent takes no input and does not move: the body stays
        // where it fell until the respawn, hazards included.
        if (IsDead)
        {
            Velocity = Vector2.Zero;
            _animator.Update(dt, Vector2.Zero, true, false, Facing);
            return;
        }

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
            {
                velocity.Y = atSurface ? JumpVelocity : -SwimSpeed;
                Sfx.Play(atSurface ? Sound.Jump : Sound.Swim);
            }
            else if (IsOnFloor())
            {
                velocity.Y = JumpVelocity;
                Sfx.Play(Sound.Jump);
            }
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
        bool airborne = !IsOnFloor();
        MoveAndSlide();
        if (airborne && IsOnFloor() && velocity.Y > HardLanding)
            Sfx.Play(Sound.Land);

        bool pushing = false;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is Barrel barrel && Mathf.Abs(collision.GetNormal().X) > 0.9f)
            {
                barrel.Push(collision.GetNormal().X < 0 ? 1 : -1);   // the normal points back at the agent
                pushing = true;
            }
        }

        if (!pushing)
        {
            _pushIdle += dt;
        }
        else
        {
            if (_pushIdle >= PushRearm)
                Sfx.Play(Sound.BarrelPush);
            _pushIdle = 0f;
        }

        // The shoot pose only plays when a shot actually left the weapon, so
        // an empty magazine never flashes a muzzle.
        if (Session != null && Input.IsActionJustPressed("shoot") && Session.TryShoot(this))
            _animator.Shoot();

        _animator.Update(dt, Velocity, IsOnFloor(), submerged, Facing, pushing);

        if (Session != null)
            CheckHazards(dt);
    }

    private int TileAt(float dx, float dy) =>
        Session?.TileAtPosition(Position + new Vector2(dx, dy)) ?? TileDefs.Empty;

    // A spike tile only hurts over the half whose spike is raised.
    private bool OnRaisedSpike(int x)
    {
        int spike = Session.RaisedSpike;
        if (spike < 0)
            return false;
        float left = x * Tile + spike * Tile / 2f;
        return Position.X + HazardExtent.X > left && Position.X - HazardExtent.X < left + Tile / 2f;
    }

    private void CheckHazards(float dt)
    {
        // Kill plane below the level bounds.
        if (Position.Y > Session.LevelBottom + 2f * Tile)
        {
            Session.AgentFell();
            return;
        }

        int minX = (int)Mathf.Floor((Position.X - HazardExtent.X) / Tile);
        int maxX = (int)Mathf.Floor((Position.X + HazardExtent.X) / Tile);
        int minY = (int)Mathf.Floor((Position.Y - HazardExtent.Y) / Tile);
        int maxY = (int)Mathf.Floor((Position.Y + HazardExtent.Y) / Tile);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int id = Session.TileAt(x, y);
                if (id == TileDefs.Lava || (id == TileDefs.Spear && Session.SpearsOut)
                    || (id == TileDefs.Spikes && OnRaisedSpike(x)))
                {
                    Session.AgentHit();
                    return;
                }
            }

        bool onElectric = false;
        if (IsOnFloor())
        {
            int below = (int)Mathf.Floor((Position.Y + HazardExtent.Y + 3f) / Tile);
            for (int x = minX; x <= maxX; x++)
            {
                int id = Session.TileAt(x, below);
                if (id == TileDefs.ElectricFloor && !Session.IsFloorOff(new Vector2I(x, below)))
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
