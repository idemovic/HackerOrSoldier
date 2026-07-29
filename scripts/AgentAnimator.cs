using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Agent animation in the DOS platformer idiom: a handful of hand-authored
// poses swapped on a coarse clock, no tweening and no in-between frames.
// Sprites.cs holds the pixel maps, this class only decides which one shows.
public sealed class AgentAnimator
{
    // "agent" doubles as idle frame 0, so the editor marker and the palette
    // icon keep showing the neutral standing pose.
    private static readonly string[] IdleFrames = { "agent", "agent_breathe" };
    private static readonly string[] RunFrames =
        { "agent_run0", "agent_run1", "agent_run2", "agent_run3" };
    private static readonly string[] JumpFrames = { "agent_jump" };
    private static readonly string[] FallFrames = { "agent_fall" };
    // The flash burns for one beat, then the aim pose holds for two.
    private static readonly string[] ShootFrames = { "agent_fire", "agent_aim", "agent_aim" };

    // Every distinct frame the animator can reach; the smoke test builds them
    // all so a malformed pixel map fails the run.
    public static readonly string[] AllFrames = IdleFrames
        .Concat(RunFrames).Concat(JumpFrames).Concat(FallFrames).Concat(ShootFrames)
        .Distinct().ToArray();

    private const float IdleFrameTime = 0.7f;    // slow breath
    private const float SwimFrameTime = 0.18f;   // lazy paddle
    private const float ShootFrameTime = 0.08f;
    private const float RunFrameStride = 18f;    // px travelled per run frame

    // The shoot pose holds for exactly one pass over its frames.
    private const float ShootPoseTime = 3 * ShootFrameTime;

    private enum Pose { Idle, Run, Jump, Fall, Swim, Shoot }

    private readonly Sprite2D _sprite;
    private Pose _pose = Pose.Idle;
    private float _phase;
    private float _shootTime;
    private string _frame;

    // Sprite key currently on screen; the smoke test asserts against it.
    public string Frame => _frame;

    public AgentAnimator(Sprite2D sprite) => _sprite = sprite;

    // Restarts the pose from the flash frame, so holding fire keeps flashing.
    public void Shoot()
    {
        _shootTime = ShootPoseTime;
        _pose = Pose.Shoot;
        _phase = 0f;
    }

    public void Reset()
    {
        _pose = Pose.Idle;
        _phase = 0f;
        _shootTime = 0f;
    }

    public void Update(float dt, Vector2 velocity, bool onFloor, bool submerged, int facing)
    {
        _sprite.FlipH = facing < 0;
        _shootTime = Mathf.Max(_shootTime - dt, 0f);

        var pose = SelectPose(velocity, onFloor, submerged);
        if (pose != _pose)
        {
            _pose = pose;
            _phase = 0f;
        }

        var (frames, step) = pose switch
        {
            Pose.Run => (RunFrames, RunFrameStride),
            Pose.Swim => (RunFrames, SwimFrameTime),   // the run cycle reads as paddling
            Pose.Jump => (JumpFrames, 1f),
            Pose.Fall => (FallFrames, 1f),
            Pose.Shoot => (ShootFrames, ShootFrameTime),
            _ => (IdleFrames, IdleFrameTime),
        };

        // The run cycle advances on distance travelled so the feet never
        // slide, whatever the speed; every other pose runs on the clock.
        _phase += pose == Pose.Run ? Mathf.Abs(velocity.X) * dt : dt;
        _phase %= step * frames.Length;   // keeps the phase precise over a long session
        Show(frames[(int)(_phase / step)]);
    }

    private Pose SelectPose(Vector2 velocity, bool onFloor, bool submerged)
    {
        if (_shootTime > 0f)
            return Pose.Shoot;   // the shoot pose wins over locomotion, as in the DOS originals
        if (submerged)
            return Pose.Swim;
        if (!onFloor)
            return velocity.Y < 0f ? Pose.Jump : Pose.Fall;
        return Mathf.Abs(velocity.X) > 1f ? Pose.Run : Pose.Idle;
    }

    private void Show(string key)
    {
        if (key == _frame)
            return;
        _frame = key;
        _sprite.Texture = Sprites.Get(key);
    }
}
