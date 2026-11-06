using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Enemy animation. The four soldier types are rigged bodies (BodyPoses):
// they idle, walk and run in many small steps and raise their gun over the
// moving legs to fire. Machines and creatures keep hand-authored cycles
// swapped on a coarse clock. Either way the first frame of a type is the
// plain type key, so editor markers and palette icons keep working.
public sealed class EnemyAnimator
{
    // Walkers advance on distance travelled so their feet never slide.
    // Hovering rotors and blinking LEDs have nothing to travel, so those run
    // on the wall clock instead.
    private record Set(string[] Cycle, string Attack = null, string Off = null, bool ClockDriven = false);

    private const float WalkFrameStride = 13f;   // px of travel per cycle frame
    private const float ClockFrameTime = 0.14f;
    private const float AttackPoseTime = 0.22f;

    // Rigged bodies: a 12-frame walk covers 36px, a run 84px, and anything
    // faster than speed 2 (80px/s) breaks into the run.
    private const float BodyWalkStride = 3f;
    private const float BodyRunStride = 7f;
    private const float BodyRunSpeed = 100f;
    private const float BodyIdleFrameTime = 0.35f;
    private const float FlashTime = 0.05f;
    private const float RecoilTime = 0.11f;

    private static readonly Dictionary<string, Set> Sets = new()
    {
        [EnemyDefs.Drone] = new(new[] { EnemyDefs.Drone, "drone_hover" }, ClockDriven: true),
        // Walkers stride one leg, pass through the standing frame, then
        // stride the other.
        [EnemyDefs.Robodog] = new(new[] { EnemyDefs.Robodog, "robodog_walk", EnemyDefs.Robodog, "robodog_walk2" }),
        [EnemyDefs.Robot] = new(new[] { EnemyDefs.Robot, "robot_walk", EnemyDefs.Robot, "robot_walk2" },
            Attack: "robot_fire", Off: "robot_off"),
        [EnemyDefs.Landmine] = new(new[] { EnemyDefs.Landmine, "landmine_blink" }, ClockDriven: true),
        // Four phases of a travelling wave, on the clock so the eel keeps
        // undulating at a steady rate instead of one governed by its speed.
        [EnemyDefs.ElectricEel] = new(new[]
        {
            EnemyDefs.ElectricEel, "electric_eel_swim1",
            "electric_eel_swim2", "electric_eel_swim3",
        }, ClockDriven: true),
    };

    private static readonly string[] Rigged =
        { EnemyDefs.Fighter, EnemyDefs.Soldier, EnemyDefs.SuperSoldier, EnemyDefs.UltimateSoldier };

    // Every frame any enemy can reach; the smoke test builds them all so a
    // malformed pixel map or a broken pose fails the run.
    public static readonly string[] AllFrames = Sets.Values
        .SelectMany(set => set.Cycle.Concat(new[] { set.Attack, set.Off }))
        .Where(frame => frame != null)
        .Concat(Rigged.SelectMany(type => BodyPoses.All(type)).Select(frame => frame.Key))
        .Distinct()
        .ToArray();

    private readonly Sprite2D _sprite;
    private string _type;
    private BodyClip _clip;
    private float _phase;
    private float _attackTime;
    private string _frame;
    private BodyFrame? _bodyFrame;

    public string Frame => _frame;

    public EnemyAnimator(Sprite2D sprite, string type)
    {
        _sprite = sprite;
        SetType(type);
    }

    // The soldier chain swaps type on every hit; the pose carries over.
    public void SetType(string type) => _type = type;

    public void Attack() => _attackTime = AttackPoseTime;

    public void Update(float dt, Vector2 velocity, bool turnedOff, int facing)
    {
        _attackTime = Mathf.Max(_attackTime - dt, 0f);
        if (BodyPoses.IsRigged(_type))
            UpdateBody(dt, velocity, facing);
        else
            UpdateMap(dt, velocity, turnedOff, facing);
    }

    private void UpdateBody(float dt, Vector2 velocity, int facing)
    {
        // Bodies are lit per facing, so the frame is never mirrored.
        _sprite.FlipH = false;
        float speed = Mathf.Abs(velocity.X);
        var clip = speed < 1f ? BodyClip.Idle : speed < BodyRunSpeed ? BodyClip.Walk : BodyClip.Run;
        if (clip != _clip)
        {
            _clip = clip;
            _phase = 0f;
        }
        float step = clip switch
        {
            BodyClip.Walk => BodyWalkStride,
            BodyClip.Run => BodyRunStride,
            _ => BodyIdleFrameTime,
        };
        int count = BodyPoses.FrameCounts[clip];
        _phase = (_phase + (clip == BodyClip.Idle ? dt : speed * dt)) % (step * count);

        var shot = Shot.None;
        if (_attackTime > 0f && BodyPoses.IsArmed(_type))
        {
            float elapsed = AttackPoseTime - _attackTime;
            shot = elapsed < FlashTime ? Shot.Fire : elapsed < RecoilTime ? Shot.Recoil : Shot.Aim;
        }
        var gun = shot == Shot.None ? Weapon.None : BodyPoses.GunOf(_type);
        var frame = new BodyFrame(_type, clip, (int)(_phase / step), shot, facing < 0, gun);
        if (frame == _bodyFrame)
            return;
        _bodyFrame = frame;
        _frame = frame.Key;
        _sprite.Texture = BodyPoses.Texture(frame);
    }

    private void UpdateMap(float dt, Vector2 velocity, bool turnedOff, int facing)
    {
        var set = Sets[_type];
        _sprite.FlipH = facing < 0;

        if (turnedOff && set.Off != null)
        {
            Show(set.Off);
            return;
        }
        if (_attackTime > 0f && set.Attack != null)
        {
            Show(set.Attack);
            return;
        }

        float step = set.ClockDriven ? ClockFrameTime : WalkFrameStride;
        _phase += set.ClockDriven ? dt : Mathf.Abs(velocity.X) * dt;
        _phase %= step * set.Cycle.Length;
        Show(set.Cycle[(int)(_phase / step)]);
    }

    private void Show(string key)
    {
        if (key == _frame)
            return;
        _frame = key;
        _sprite.Texture = Sprites.Get(key);
    }
}
