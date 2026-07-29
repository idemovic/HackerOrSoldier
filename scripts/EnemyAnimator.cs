using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Enemy animation, same idiom as AgentAnimator: hand-authored poses swapped
// on a coarse clock, no tweening. One table covers all ten README enemies;
// the first frame of each cycle is the plain type key, so editor markers and
// palette icons keep working unchanged.
public sealed class EnemyAnimator
{
    // Walkers advance on distance travelled so their feet never slide.
    // Hovering rotors and blinking LEDs have nothing to travel, so those run
    // on the wall clock instead.
    private record Set(string[] Cycle, string Attack = null, string Off = null, bool ClockDriven = false);

    private const float WalkFrameStride = 13f;   // px of travel per cycle frame
    private const float ClockFrameTime = 0.14f;
    private const float AttackPoseTime = 0.22f;

    private static readonly Dictionary<string, Set> Sets = new()
    {
        [EnemyDefs.Fighter] = new(new[] { EnemyDefs.Fighter, "fighter_walk" }),
        [EnemyDefs.Soldier] = new(new[] { EnemyDefs.Soldier, "soldier_walk" },
            Attack: "soldier_fire"),
        [EnemyDefs.SuperSoldier] = new(new[] { EnemyDefs.SuperSoldier, "super_soldier_walk" },
            Attack: "super_soldier_fire"),
        [EnemyDefs.UltimateSoldier] = new(new[] { EnemyDefs.UltimateSoldier, "ultimate_soldier_walk" }),
        [EnemyDefs.Drone] = new(new[] { EnemyDefs.Drone, "drone_hover" }, ClockDriven: true),
        [EnemyDefs.Robodog] = new(new[] { EnemyDefs.Robodog, "robodog_walk" }),
        [EnemyDefs.Turret] = new(new[] { EnemyDefs.Turret }, Attack: "turret_fire"),
        [EnemyDefs.Robot] = new(new[] { EnemyDefs.Robot, "robot_walk" },
            Attack: "robot_fire", Off: "robot_off"),
        [EnemyDefs.Landmine] = new(new[] { EnemyDefs.Landmine, "landmine_blink" }, ClockDriven: true),
        [EnemyDefs.ElectricEel] = new(new[] { EnemyDefs.ElectricEel, "electric_eel_swim" }),
    };

    // Every frame any enemy can reach; the smoke test builds them all so a
    // malformed pixel map fails the run.
    public static readonly string[] AllFrames = Sets.Values
        .SelectMany(set => set.Cycle.Concat(new[] { set.Attack, set.Off }))
        .Where(frame => frame != null)
        .Distinct()
        .ToArray();

    private readonly Sprite2D _sprite;
    private Set _set;
    private float _phase;
    private float _attackTime;
    private string _frame;

    public string Frame => _frame;

    public EnemyAnimator(Sprite2D sprite, string type)
    {
        _sprite = sprite;
        SetType(type);
    }

    // The soldier chain swaps type on every hit; the pose carries over.
    public void SetType(string type) => _set = Sets[type];

    public void Attack() => _attackTime = AttackPoseTime;

    public void Update(float dt, Vector2 velocity, bool turnedOff, int facing)
    {
        _sprite.FlipH = facing < 0;
        _attackTime = Mathf.Max(_attackTime - dt, 0f);

        if (turnedOff && _set.Off != null)
        {
            Show(_set.Off);
            return;
        }
        if (_attackTime > 0f && _set.Attack != null)
        {
            Show(_set.Attack);
            return;
        }

        float step = _set.ClockDriven ? ClockFrameTime : WalkFrameStride;
        _phase += _set.ClockDriven ? dt : Mathf.Abs(velocity.X) * dt;
        _phase %= step * _set.Cycle.Length;
        Show(_set.Cycle[(int)(_phase / step)]);
    }

    private void Show(string key)
    {
        if (key == _frame)
            return;
        _frame = key;
        _sprite.Texture = Sprites.Get(key);
    }
}
