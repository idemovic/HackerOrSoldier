using Godot;

namespace HackerOrSoldier;

// The turret's swivelling gun head. It turns toward the agent at a fixed
// rate and only reports itself on target once the barrel points at the
// agent, so the turret aims first and then fires along the barrel. The
// platform blocks a cone straight down: the barrel swings over the top to
// change sides and cannot reach an agent directly underneath.
public partial class TurretGun : Node2D
{
    public const string Idle = "turret_gun";
    public const string Firing = "turret_fire";

    // The hinge on top of the mast, relative to the turret's cell centre,
    // and the shift that puts the hinge (4px left of the head art's centre)
    // on that point.
    public static readonly Vector2 Pivot = new(-3, -6);
    public static readonly Vector2 HeadOffset = new(4, 0);

    private const float TurnSpeed = Mathf.Pi * 2f / 3f;           // 120 degrees a second
    private const float MaxTilt = Mathf.Pi * 145f / 180f;          // either side of straight up
    private const float OnTargetTolerance = Mathf.Pi * 3f / 180f;
    private const float MuzzleLength = 17f;                         // hinge to barrel tip
    private const float FlashTime = 0.22f;

    private Sprite2D _head;
    // Radians clockwise from straight up. Starts level and facing right, as
    // the editor marker draws it.
    private float _tilt = Mathf.Pi / 2f;
    private float _flash;

    public string Frame => _flash > 0f ? Firing : Idle;
    public Vector2 Aim => new(Mathf.Sin(_tilt), -Mathf.Cos(_tilt));

    // Where shots leave the barrel, relative to the turret's cell centre.
    public Vector2 Muzzle => Pivot + Aim * MuzzleLength;

    public override void _Ready()
    {
        Position = Pivot;
        _head = new Sprite2D { Texture = Sprites.Get(Idle), Offset = HeadOffset };
        AddChild(_head);
        Pose();
    }

    // Turns toward the target (relative to the turret's cell centre, null
    // when the turret cannot see it) and returns whether the barrel is on it.
    public bool Track(Vector2? target, float dt)
    {
        _flash = Mathf.Max(_flash - dt, 0f);
        bool onTarget = false;
        if (target is Vector2 at)
        {
            var to = at - Pivot;
            float bearing = Mathf.Atan2(to.X, -to.Y);
            _tilt = Mathf.MoveToward(_tilt, Mathf.Clamp(bearing, -MaxTilt, MaxTilt), TurnSpeed * dt);
            onTarget = Mathf.Abs(bearing - _tilt) <= OnTargetTolerance;
        }
        Pose();
        return onTarget;
    }

    public void Fire()
    {
        _flash = FlashTime;
        Pose();
    }

    private void Pose()
    {
        _head.Rotation = _tilt - Mathf.Pi / 2f;
        // Aiming left, mirror the head so its lit dome stays on top.
        _head.FlipV = _tilt < 0f;
        _head.Texture = Sprites.Get(Frame);
    }
}
