using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Picks which agent frame shows from the movement state. BodyPoses holds
// the motion and CharacterRig draws it; this class only keeps the clocks.
// Locomotion and shooting are separate layers: the gun pose replaces the
// arms while the legs keep running, jumping or swimming under it.
public sealed class AgentAnimator
{
    // Every distinct pose the animator can reach; the smoke test builds them
    // all so a broken pose fails the run.
    public static readonly string[] AllFrames = BodyPoses.All(BodyPoses.Agent).Select(f => f.Key).Distinct().ToArray();

    private const float IdleFrameTime = 0.35f;   // one breath per 1.4s
    private const float TreadFrameTime = 0.1f;
    private const float SwimFrameTime = 0.07f;
    private const float DiveFrameTime = 0.09f;
    private const float FallFrameTime = 0.09f;   // arm windmill
    private const float LandFrameTime = 0.05f;
    private const float DieFrameTime = 0.055f;
    private const float RunFrameStride = 7f;     // px travelled per run frame
    private const float PushFrameTime = 0.06f;   // a heavy, deliberate step
    private const float FlashTime = 0.05f;
    private const float RecoilTime = 0.11f;
    private const float ShootPoseTime = 0.24f;

    // Speeds that bracket the jump and fall frames: take-off speed down to
    // the apex, and how fast a fall has to be before the arms windmill.
    private const float TakeOffSpeed = 450f;
    private const float WindmillSpeed = 160f;
    private const float MinAirTimeForLanding = 0.2f;

    // Sinking turns into a dive only after this long without a stroke up,
    // so tapping jump to hold a depth keeps treading water instead of
    // flipping head-down between strokes. A fall into water is already
    // sinking, so it dives straight in.
    private const float DiveDelay = 0.4f;

    private static float LandTime => BodyPoses.FrameCounts[BodyClip.Land] * LandFrameTime;

    private readonly Sprite2D _sprite;
    private BodyClip _clip = BodyClip.Idle;
    private float _phase;
    private float _shootTime;
    private Weapon _gun;
    private float _airTime;
    private float _landTime;
    private float _sinkTime;
    private bool _dying;
    private BodyFrame? _frame;

    // Pose currently on screen; the smoke test asserts against it.
    public string Frame => _frame?.Key;

    public AgentAnimator(Sprite2D sprite) => _sprite = sprite;

    // Restarts the gun pose from the flash, so holding fire keeps flashing.
    public void Shoot(Weapon gun)
    {
        _gun = gun;
        _shootTime = ShootPoseTime;
    }

    // Takes over from every other clip until Reset; the session decides when
    // the body gets up again.
    public void Die()
    {
        _dying = true;
        _phase = 0f;
        _shootTime = 0f;
    }

    public void Reset()
    {
        _clip = BodyClip.Idle;
        _phase = 0f;
        _shootTime = 0f;
        _airTime = 0f;
        _landTime = 0f;
        _sinkTime = 0f;
        _dying = false;
    }

    public void Update(float dt, Vector2 velocity, bool onFloor, bool submerged, int facing, bool pushing = false)
    {
        bool left = facing < 0;

        // The death clip runs once and holds its last pose rather than
        // wrapping: the agent stays down.
        if (_dying)
        {
            _phase += dt;
            int last = BodyPoses.FrameCounts[BodyClip.Die] - 1;
            Show(new BodyFrame(BodyPoses.Agent, BodyClip.Die, Mathf.Min((int)(_phase / DieFrameTime), last), Left: left));
            return;
        }

        _shootTime = Mathf.Max(_shootTime - dt, 0f);
        TrackLanding(dt, onFloor, submerged);
        _sinkTime = !onFloor && velocity.Y > 0f ? _sinkTime + dt : 0f;

        var clip = SelectClip(velocity, onFloor, submerged, pushing);
        if (clip != _clip)
        {
            _clip = clip;
            _phase = 0f;
        }
        // The run advances on distance travelled so the feet never slide,
        // whatever the speed; the rest run on the clock.
        _phase += clip == BodyClip.Run ? Mathf.Abs(velocity.X) * dt : dt;

        int count = BodyPoses.FrameCounts[clip];
        int index = clip switch
        {
            BodyClip.Run => Cycle(RunFrameStride, count),
            BodyClip.Push => Cycle(PushFrameTime, count),
            BodyClip.Idle => Cycle(IdleFrameTime, count),
            BodyClip.Tread => Cycle(TreadFrameTime, count),
            BodyClip.Swim => Cycle(SwimFrameTime, count),
            BodyClip.Dive => Cycle(DiveFrameTime, count),
            // Jump frames track vertical speed, so the tuck is timed by the
            // jump itself: take-off at full speed, the last frame at the apex.
            BodyClip.Jump => Mathf.Clamp((int)((1f + velocity.Y / TakeOffSpeed) * count), 0, count - 1),
            BodyClip.Fall => velocity.Y < WindmillSpeed ? 0 : 1 + (int)(_phase / FallFrameTime) % (count - 1),
            BodyClip.Land => Mathf.Min((int)((LandTime - _landTime) / LandFrameTime), count - 1),
            _ => 0,
        };
        var shot = CurrentShot();
        Show(new BodyFrame(BodyPoses.Agent, clip, index, shot, left, shot == Shot.None ? Weapon.None : _gun));
    }

    private int Cycle(float step, int count)
    {
        _phase %= step * count;   // keeps the phase precise over a long session
        return (int)(_phase / step);
    }

    // A landing from a real jump or fall plays the crouch-and-rise before
    // idle takes over; running straight on skips it.
    private void TrackLanding(float dt, bool onFloor, bool submerged)
    {
        _landTime = Mathf.Max(_landTime - dt, 0f);
        if (onFloor && _airTime >= MinAirTimeForLanding)
            _landTime = LandTime;
        _airTime = onFloor || submerged ? 0f : _airTime + dt;
    }

    // Pushing beats the run: against a barrel the slide leaves the agent
    // almost no horizontal speed, which would otherwise read as standing.
    private BodyClip SelectClip(Vector2 velocity, bool onFloor, bool submerged, bool pushing)
    {
        if (submerged)
        {
            if (Mathf.Abs(velocity.X) > 1f)
                return BodyClip.Swim;
            return _sinkTime >= DiveDelay ? BodyClip.Dive : BodyClip.Tread;
        }
        if (!onFloor)
            return velocity.Y < 0f ? BodyClip.Jump : BodyClip.Fall;
        if (pushing)
            return BodyClip.Push;
        if (Mathf.Abs(velocity.X) > 1f)
            return BodyClip.Run;
        return _landTime > 0f ? BodyClip.Land : BodyClip.Idle;
    }

    private Shot CurrentShot()
    {
        if (_shootTime <= 0f)
            return Shot.None;
        float elapsed = ShootPoseTime - _shootTime;
        return elapsed < FlashTime ? Shot.Fire : elapsed < RecoilTime ? Shot.Recoil : Shot.Aim;
    }

    private void Show(BodyFrame frame)
    {
        if (frame == _frame)
            return;
        _frame = frame;
        _sprite.Texture = BodyPoses.Texture(frame);
    }
}
