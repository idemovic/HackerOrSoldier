using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

public enum BodyClip { Idle, Walk, Run, Push, Jump, Fall, Land, Tread, Swim, Dive, Die }

// The upper-body overlay while the gun is out. It replaces only the arms,
// so the legs keep walking, running, falling or swimming under it.
public enum Shot { None, Fire, Recoil, Aim }

// One rendered frame of a rigged character. Key names the pose for tests and
// debugging and starts with the character's type, so the first idle frame
// is the bare type key the editor marker and palette icon use. The facing
// is not part of it because both facings show the same pose. Gun is the
// weapon in hand while a Shot overlay plays; without one it is ignored.
public readonly record struct BodyFrame(string Who, BodyClip Clip, int Index, Shot Shot = Shot.None, bool Left = false, Weapon Gun = Weapon.None)
{
    public string Key => Clip == BodyClip.Idle && Index == 0 && Shot == Shot.None
        ? Who
        : $"{Who}_{Clip.ToString().ToLowerInvariant()}{Index}"
          + (Shot == Shot.None ? "" : $"_{Gun.ToString().ToLowerInvariant()}_{Shot.ToString().ToLowerInvariant()}");
}

// Human motion, authored as key poses and sampled into many frames so every
// action moves in small steps, the way rotoscoped DOS games did it. The
// agent and the four soldier types share it; each has a look, a gun and the
// clips it can reach. The animators pick frames; CharacterRig draws them.
public static class BodyPoses
{
    public const string Agent = "agent";

    public static readonly Dictionary<BodyClip, int> FrameCounts = new()
    {
        [BodyClip.Idle] = 4,
        [BodyClip.Walk] = 12,
        [BodyClip.Run] = 12,
        [BodyClip.Push] = 12,
        [BodyClip.Jump] = 4,
        [BodyClip.Fall] = 4,
        [BodyClip.Land] = 3,
        [BodyClip.Tread] = 8,
        [BodyClip.Swim] = 12,
        [BodyClip.Dive] = 12,
        [BodyClip.Die] = 9,
    };

    // Guns lists every weapon the character can be seen holding; soldiers
    // carry one, the agent whatever it picked up.
    private sealed record Body(Look Look, Weapon[] Guns, BodyClip[] Clips);

    private static readonly Weapon[] Unarmed = { };
    private static readonly Weapon[] AgentGuns = WeaponDefs.All.Select(def => def.Kind).ToArray();

    private static readonly BodyClip[] EnemyClips = { BodyClip.Idle, BodyClip.Walk, BodyClip.Run };

    private static readonly Ramp Skin = new("f7cfa8", "e0a078", "a86a48");

    // Face tones every head map shares, plus the map's own hair or helmet.
    private static Dictionary<char, Color> Tones(params (char Key, string Hex)[] extra)
    {
        var tones = new Dictionary<char, Color>
        {
            ['S'] = new Color("f7cfa8"),
            ['s'] = new Color("e0a078"),
            ['z'] = new Color("a86a48"),
            ['^'] = new Color("a86a48"),
            ['m'] = new Color("9a4f3c"),
            ['K'] = new Color("0e0e16"),
        };
        foreach (var (key, hex) in extra)
            tones[key] = new Color(hex);
        return tones;
    }

    // Helmeted troopers: shell, brim, then a face or a visor band.
    private static readonly string[] HelmetFace =
    {
        "..gGGg..",
        ".gGGGGg.",
        "gGGGGGGg",
        "gggggggg",
        "jgzsKsS.",
        "jgzsSSSS",
        ".jzssmz.",
        "..zss...",
        "...z^...",
    };
    private static readonly string[] HelmetVisor =
    {
        "..gGGg..",
        ".gGGGGg.",
        "gGGGGGGg",
        "gGGGGGGg",
        "gGgvVVVV",
        "gGgggggg",
        ".jggggg.",
        "..jgg...",
        "...j^...",
    };

    private static readonly Dictionary<string, Body> Bodies = new()
    {
        // Black suit, white shirt, red tie, shades: the classic pixel spy.
        [Agent] = new(new Look(
            Skin: Skin,
            Jacket: new("5a6280", "262a38", "14151c"),
            Legs: new("4a5068", "222532", "121319"),
            Shoes: new("4a4a55", "202026", "0d0d11"),
            Shirt: new Ramp("ffffff", "dfe3ea", "9ea5b5"),
            Tie: new Ramp("ff6a55", "d8322a", "8a1c17"),
            HeadMap: new[]
            {
                "..jhhh..",
                ".jhHHHh.",
                "jhhhhhhh",
                "jhhsSSS.",
                "jhzKKKKc",
                "jhzsSSSS",
                ".jzssmz.",
                "..zss...",
                "...z^...",
            },
            HeadColors: Tones(('H', "6e5238"), ('h', "402d1d"), ('j', "1f150d"), ('c', "3e5a78"))),
            AgentGuns,
            new[] { BodyClip.Idle, BodyClip.Run, BodyClip.Push, BodyClip.Jump, BodyClip.Fall, BodyClip.Land, BodyClip.Tread, BodyClip.Swim, BodyClip.Dive, BodyClip.Die }),
        // Bare-knuckle brawler: orange tee, black trousers, no gun.
        [EnemyDefs.Fighter] = new(new Look(
            Skin: Skin,
            Jacket: new("ffb060", "f28022", "a8501a"),
            Legs: new("4a4a58", "23232c", "111116"),
            Shoes: new("5a4030", "2e2018", "140e0a"),
            Shirt: null, Tie: null,
            HeadMap: new[]
            {
                "..hhhh..",
                ".hhHHhh.",
                "hhhhhhhh",
                "hhhsSSS.",
                "hhzsSKs.",
                "hhzsSSSS",
                ".hzssmz.",
                "..zss...",
                "...z^...",
            },
            HeadColors: Tones(('H', "4a3626"), ('h', "2b1f14"))),
            Unarmed, EnemyClips),
        [EnemyDefs.Soldier] = new(new Look(
            Skin: Skin,
            Jacket: new("6fbf5a", "3d8a34", "1f4a1c"),
            Legs: new("4f9a44", "2e6a28", "173a15"),
            Shoes: new("3a3a30", "1c1c16", "0c0c0a"),
            Shirt: null, Tie: null,
            HeadMap: HelmetFace,
            HeadColors: Tones(('G', "6fbf5a"), ('g', "3d7d34"), ('j', "1f4a1c"))),
            new[] { Weapon.AssaultRifle }, EnemyClips),
        // Armoured troopers: same build, plated and visored.
        [EnemyDefs.SuperSoldier] = new(new Look(
            Skin: new("b6c0d6", "6f7a95", "3a4258"),
            Jacket: new("c2cce0", "7a87a5", "3a4258"),
            Legs: new("9aa6c0", "5c6884", "2c3346"),
            Shoes: new("4a5268", "262b38", "10131a"),
            Shirt: null, Tie: null,
            HeadMap: HelmetVisor,
            HeadColors: Tones(('G', "c2cce0"), ('g', "7a87a5"), ('j', "3a4258"), ('v', "9af2ff"), ('V', "35c8dc"))),
            new[] { Weapon.Minigun }, EnemyClips),
        [EnemyDefs.UltimateSoldier] = new(new Look(
            Skin: new("b48af5", "7a3be0", "3c1d78"),
            Jacket: new("a77af5", "7034d6", "35186e"),
            Legs: new("8a5ce8", "5426b0", "2a1260"),
            Shoes: new("3a2a5a", "1e1430", "0e0a18"),
            Shirt: null, Tie: null,
            HeadMap: HelmetVisor,
            HeadColors: Tones(('G', "a77af5"), ('g', "7034d6"), ('j', "35186e"), ('v', "ffb0ff"), ('V', "e03be0"))),
            Unarmed, EnemyClips),
    };

    public static readonly BodyFrame Dead = new(Agent, BodyClip.Die, 8);

    public static bool IsRigged(string who) => who != null && Bodies.ContainsKey(who);
    public static bool IsArmed(string who) => Bodies[who].Guns.Length > 0;

    // The gun a soldier carries; the agent's comes from the session.
    public static Weapon GunOf(string who) => Bodies[who].Guns.FirstOrDefault();

    private static readonly Dictionary<BodyFrame, ImageTexture> Cache = new();
    private static Dictionary<string, BodyFrame> _byKey;

    public static ImageTexture Texture(BodyFrame frame)
    {
        if (Cache.TryGetValue(frame, out var cached))
            return cached;
        var body = Bodies[frame.Who];
        var pose = Sample(frame.Clip, frame.Index);
        var gun = frame.Shot == Shot.None ? Weapon.None : frame.Gun;
        if (frame.Shot != Shot.None)
            pose = pose.WithArms(ShotArms(frame.Shot, gun, pose.Lean));
        var image = CharacterRig.Render(pose, body.Look, frame.Left, gun, flash: frame.Shot == Shot.Fire);
        var texture = ImageTexture.CreateFromImage(image);
        Cache[frame] = texture;
        return texture;
    }

    // Every pose any rigged character can reach, right-facing; the smoke
    // test and the contact sheets render them all.
    public static IEnumerable<BodyFrame> All(string who = null)
    {
        foreach (var (name, body) in Bodies)
        {
            if (who != null && name != who)
                continue;
            foreach (var clip in body.Clips)
                for (int i = 0; i < FrameCounts[clip]; i++)
                {
                    yield return new BodyFrame(name, clip, i);
                    if (clip == BodyClip.Die)
                        continue;
                    foreach (var gun in body.Guns)
                        foreach (var shot in new[] { Shot.Fire, Shot.Recoil, Shot.Aim })
                            yield return new BodyFrame(name, clip, i, shot, Gun: gun);
                }
        }
    }

    public static BodyFrame? Find(string key)
    {
        _byKey ??= All().ToDictionary(f => f.Key);
        return _byKey.TryGetValue(key, out var frame) ? frame : null;
    }

    private static Pose Sample(BodyClip clip, int index)
    {
        float t = (float)index / FrameCounts[clip];
        return clip switch
        {
            BodyClip.Idle => Idle(t),
            BodyClip.Walk => Walk(t),
            BodyClip.Run => Run(t),
            BodyClip.Push => Push(t),
            BodyClip.Jump => Jump[index],
            BodyClip.Fall => Fall[index],
            BodyClip.Land => CharacterRig.Grounded(Pose.Lerp(Crouch, Idle(0f), index / 3f)),
            BodyClip.Tread => Tread(t),
            BodyClip.Swim => Stroke(t, SwimLean, SwimRoot),
            BodyClip.Dive => Stroke(t, DiveLean, DiveRoot),
            _ => Die[index],
        };
    }

    // The walk: shorter reach, the heel barely lifts, one foot always down.
    private static readonly Limb[] WalkStride =
    {
        new(25f, 15f, 100f),
        new(15f, 0f, 90f),
        new(3f, -3f, 90f),
        new(-12f, -12f, 90f),
        new(-22f, -30f, 70f),
        new(-15f, -60f, 40f),
        new(8f, -45f, 70f),
        new(22f, 5f, 95f),
    };

    private static Pose Walk(float t)
    {
        Limb Arm(float phase)
        {
            float upper = -20f * Mathf.Cos(Mathf.Tau * phase);
            return new Limb(upper, upper + 25f);
        }
        return CharacterRig.Grounded(new Pose(Vector2.Zero, 4f,
            NearArm: Arm(t), FarArm: Arm(t + 0.5f),
            NearLeg: StrideAt(WalkStride, t), FarLeg: StrideAt(WalkStride, t + 0.5f)));
    }

    private static float Wave(float t, float offset = 0f) => Mathf.Sin(Mathf.Tau * (t + offset));

    // A slow breath: the chest sinks a pixel and the arms drift with it.
    private static Pose Idle(float t)
    {
        float breath = 0.5f - 0.5f * Mathf.Cos(Mathf.Tau * t);
        return CharacterRig.Grounded(new Pose(Vector2.Zero, 2f,
            NearArm: new(6f + 3f * breath, 14f + 4f * breath),
            FarArm: new(-6f - 2f * breath, 2f),
            NearLeg: new(6f, 2f),
            FarLeg: new(-7f, -5f),
            Chest: breath));
    }

    // One leg's run cycle as (thigh, shin, foot) keys, starting at heel
    // strike. The far leg runs the same cycle half a turn later, and the
    // arms swing against the legs. Grounding the lower foot every frame
    // gives the body its bob for free.
    private static readonly Limb[] Stride =
    {
        new(32f, 18f, 105f),    // heel strike
        new(18f, -8f, 90f),     // absorb
        new(2f, -6f, 90f),      // pass under the body
        new(-22f, -28f, 80f),   // push
        new(-40f, -58f, 40f),   // toe off
        new(-24f, -102f, 10f),  // heel kicks up
        new(20f, -78f, 45f),    // swing through
        new(50f, 8f, 90f),      // reach
    };

    private static Limb StrideAt(Limb[] keys, float t)
    {
        float k = (t - Mathf.Floor(t)) * keys.Length;
        int i = (int)k;
        return Limb.Lerp(keys[i], keys[(i + 1) % keys.Length], k - i);
    }

    private static Limb RunArm(float t)
    {
        float upper = -38f * Mathf.Cos(Mathf.Tau * t);
        return new Limb(upper, upper + 85f);
    }

    private static Pose Run(float t) => CharacterRig.Grounded(new Pose(Vector2.Zero, 11f,
        NearArm: RunArm(t), FarArm: RunArm(t + 0.5f),
        NearLeg: StrideAt(Stride, t), FarLeg: StrideAt(Stride, t + 0.5f)));

    // Shoving a barrel: hips back, the body leaning into it, both palms on
    // the barrel at chest height. The legs drive in short steps, the back
    // leg straightening as it pushes off, and the lean dips with each drive.
    private static readonly Limb[] PushStride =
    {
        new(18f, 2f, 95f),      // plant under the hips
        new(6f, -8f, 90f),
        new(-8f, -20f, 85f),
        new(-22f, -32f, 75f),   // drive
        new(-34f, -44f, 55f),   // back leg straight, toe pushing off
        new(-26f, -78f, 35f),   // heel lifts
        new(-4f, -66f, 55f),    // swing through low
        new(16f, -18f, 85f),    // reach
    };

    private static Pose Push(float t) => CharacterRig.Grounded(new Pose(new Vector2(-2f, 0f), 24f + 3f * Mathf.Abs(Wave(t)),
        NearArm: new(28f, 112f), FarArm: new(22f, 104f),
        NearLeg: StrideAt(PushStride, t), FarLeg: StrideAt(PushStride, t + 0.5f)));

    // Jump frames run from take-off to the apex; the animator picks one by
    // vertical speed, so the body tucks exactly as fast as the jump slows.
    private static readonly Pose[] Jump =
    {
        new(new(0f, -1f), 8f, new(150f, 172f), new(-35f, 15f), new(-18f, -22f, 30f), new(45f, -15f, 80f)),
        new(new(0f, -1f), 10f, new(125f, 150f), new(-12f, 50f), new(12f, -48f, 45f), new(62f, -8f, 90f)),
        new(new(0f, -1f), 12f, new(95f, 132f), new(40f, 105f), new(62f, -28f, 80f), new(55f, -38f, 70f)),
        new(new(0f, -1f), 10f, new(100f, 140f), new(45f, 100f), new(58f, -5f, 95f), new(40f, -25f, 80f)),
    };

    // Falling unfolds the tuck, then the arms windmill on the clock.
    private static readonly Pose[] Fall =
    {
        new(Vector2.Zero, 8f, new(110f, 150f), new(70f, 120f), new(38f, 10f, 95f), new(12f, -25f, 75f)),
        new(Vector2.Zero, 6f, new(130f, 165f), new(95f, 140f), new(25f, 6f, 100f), new(0f, -30f, 65f)),
        new(Vector2.Zero, 5f, new(145f, 175f), new(110f, 150f), new(20f, 5f, 100f), new(-4f, -32f, 60f)),
        new(Vector2.Zero, 5f, new(120f, 150f), new(150f, 178f), new(20f, 5f, 100f), new(-4f, -32f, 60f)),
    };

    private static readonly Pose Crouch =
        new(Vector2.Zero, 18f, new(40f, 80f), new(20f, 60f), new(58f, -38f), new(42f, -48f), Chest: 1f);

    // Treading water: a flutter kick under a slow breaststroke.
    private static Pose Tread(float t)
    {
        float stroke = 95f + 50f * Mathf.Cos(Mathf.Tau * t);
        return new Pose(Vector2.Zero, 14f,
            NearArm: new(stroke, stroke + 30f + 20f * Wave(t)),
            FarArm: new(stroke - 15f, stroke + 20f + 20f * Wave(t, 0.1f)),
            NearLeg: new(18f + 18f * Wave(t), -8f + 22f * Wave(t, -0.15f), 50f),
            FarLeg: new(18f - 18f * Wave(t), -8f - 22f * Wave(t, -0.15f), 50f));
    }

    // The swim lies nearly flat with the head a touch up; the dive points
    // the same stroke head-down. Each root centres its body in the frame.
    private const float SwimLean = 80f, DiveLean = 110f;
    private static readonly Vector2 SwimRoot = new(-3f, -4f), DiveRoot = new(-3f, -5f);

    // Front crawl along the spine: each arm windmills in turn (reach past
    // the head, pull under the body, push past the hip, recover over the
    // back) with the forearm leading the pull and trailing the recovery,
    // while the legs flutter twice per stroke with pointed toes.
    private static Pose Stroke(float t, float lean, Vector2 root)
    {
        float ahead = 180f - lean;
        Limb Arm(float phase)
        {
            float upper = ahead - 360f * phase;
            return new Limb(upper, upper - 30f * Wave(phase));
        }
        Limb Leg(float phase)
        {
            float thigh = -lean + 12f * Wave(2f * phase);
            float shin = thigh - 10f - 10f * Wave(2f * phase, -0.15f);
            return new Limb(thigh, shin, shin - 5f);
        }
        return new Pose(root, lean, Arm(t), Arm(t + 0.5f), Leg(t), Leg(t + 0.5f));
    }

    // Struck, staggering back, knees going, toppling backwards, a bounce on
    // the floor and still. The last pose holds.
    private static readonly Pose[] Die =
    {
        CharacterRig.Grounded(new(new(-1f, 0f), -14f, new(150f, 190f), new(125f, 160f), new(10f, 4f), new(-12f, -8f))),
        CharacterRig.Grounded(new(new(-2f, 0f), -24f, new(165f, 200f), new(140f, 175f), new(22f, -18f), new(6f, -26f))),
        CharacterRig.Grounded(new(new(-2f, 0f), -30f, new(175f, 205f), new(150f, 185f), new(48f, -22f), new(38f, -32f), Chest: 1f)),
        new(new(-1f, 5f), -48f, new(185f, 205f), new(160f, 190f), new(70f, -5f, 95f), new(60f, -18f, 95f)),
        new(new(1f, 7.5f), -66f, new(195f, 215f), new(170f, 200f), new(78f, 30f, 130f), new(70f, 15f, 120f)),
        new(new(3f, 9f), -82f, new(205f, 225f), new(185f, 210f), new(84f, 70f, 160f), new(78f, 60f, 150f)),
        new(new(3f, 9.8f), -90f, new(215f, 235f), new(200f, 225f), new(87f, 86f, 170f), new(83f, 82f, 165f)),
        new(new(3f, 9.2f), -87f, new(225f, 245f), new(205f, 230f), new(86f, 82f, 168f), new(82f, 78f, 160f)),
        new(new(3f, 9.8f), -90f, new(250f, 262f), new(215f, 240f), new(88f, 88f, 172f), new(84f, 84f, 168f)),
    };

    // Arms while the weapon is out, kicked up on the recoil beat. The pistol
    // is held out at shoulder height, the rifle and crossbow tucked into the
    // shoulder, the minigun and chainsaw carried low at the hip with less
    // kick for their weight, and dynamite reached down to set on the floor.
    private static Pose ShotArms(Shot shot, Weapon gun, float lean)
    {
        var (elbowDrop, aim, kick) = gun switch
        {
            Weapon.AssaultRifle or Weapon.Crossbow => (30f, 90f, 8f),
            Weapon.Minigun => (52f, 92f, 4f),
            Weapon.Chainsaw => (48f, 84f, 5f),
            Weapon.Dynamite => (20f, 40f, 0f),
            _ => (12f, 90f, 12f),
        };
        if (shot == Shot.Recoil)
            aim += kick;
        return new Pose(Vector2.Zero, lean,
            NearArm: new(aim - elbowDrop, aim),
            FarArm: new(aim - elbowDrop - 10f, aim - 4f),
            NearLeg: default, FarLeg: default);
    }
}
