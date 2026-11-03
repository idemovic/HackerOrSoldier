using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

public enum AgentClip { Idle, Run, Jump, Fall, Land, Swim, Die }

// The upper-body overlay while the gun is out. It replaces only the arms,
// so the legs keep running, falling or swimming under it.
public enum Shot { None, Fire, Recoil, Aim }

// One rendered agent frame. Key names the pose for tests and debugging;
// the facing is not part of it because both facings show the same pose.
public readonly record struct AgentFrame(AgentClip Clip, int Index, Shot Shot = Shot.None, bool Left = false)
{
    public string Key => Clip == AgentClip.Idle && Index == 0 && Shot == Shot.None
        ? "agent"
        : $"agent_{Clip.ToString().ToLowerInvariant()}{Index}" + (Shot == Shot.None ? "" : $"_{Shot.ToString().ToLowerInvariant()}");
}

// The agent's motion, authored as key poses and sampled into many frames so
// every action moves in small steps, the way rotoscoped DOS games did it.
// AgentAnimator decides which frame shows; CharacterRig draws it.
public static class AgentPoses
{
    public static readonly Dictionary<AgentClip, int> FrameCounts = new()
    {
        [AgentClip.Idle] = 4,
        [AgentClip.Run] = 12,
        [AgentClip.Jump] = 4,
        [AgentClip.Fall] = 4,
        [AgentClip.Land] = 3,
        [AgentClip.Swim] = 8,
        [AgentClip.Die] = 9,
    };

    // Black suit, white shirt, red tie, shades: the classic pixel spy.
    private static readonly Look Agent = new(
        Skin: new("f7cfa8", "e0a078", "a86a48"),
        Jacket: new("5a6280", "262a38", "14151c"),
        Legs: new("4a5068", "222532", "121319"),
        Shoes: new("4a4a55", "202026", "0d0d11"),
        Shirt: new("ffffff", "dfe3ea", "9ea5b5"),
        Tie: new("ff6a55", "d8322a", "8a1c17"),
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
        HeadColors: new()
        {
            ['H'] = new Color("6e5238"),
            ['h'] = new Color("402d1d"),
            ['j'] = new Color("1f150d"),
            ['S'] = new Color("f7cfa8"),
            ['s'] = new Color("e0a078"),
            ['z'] = new Color("a86a48"),
            ['^'] = new Color("a86a48"),
            ['m'] = new Color("9a4f3c"),
            ['K'] = new Color("0e0e16"),
            ['c'] = new Color("3e5a78"),
        });

    public static readonly AgentFrame Dead = new(AgentClip.Die, 8);

    private static readonly Dictionary<AgentFrame, ImageTexture> Cache = new();

    public static ImageTexture Texture(AgentFrame frame)
    {
        if (Cache.TryGetValue(frame, out var cached))
            return cached;
        var weapon = frame.Shot switch
        {
            Shot.None => Weapon.None,
            Shot.Fire => Weapon.PistolFlash,
            _ => Weapon.Pistol,
        };
        var pose = Sample(frame.Clip, frame.Index);
        if (frame.Shot != Shot.None)
            pose = pose.WithArms(ShotArms(frame.Shot, pose.Lean));
        var texture = ImageTexture.CreateFromImage(CharacterRig.Render(pose, Agent, frame.Left, weapon));
        Cache[frame] = texture;
        return texture;
    }

    // Every pose the animator can reach, right-facing; the smoke test and
    // the contact sheet render them all.
    public static IEnumerable<AgentFrame> All()
    {
        foreach (var (clip, count) in FrameCounts)
            for (int i = 0; i < count; i++)
            {
                yield return new AgentFrame(clip, i);
                if (clip == AgentClip.Die)
                    continue;
                foreach (var shot in new[] { Shot.Fire, Shot.Recoil, Shot.Aim })
                    yield return new AgentFrame(clip, i, shot);
            }
    }

    public static AgentFrame? Find(string key) =>
        All().Select(f => (AgentFrame?)f).FirstOrDefault(f => f.Value.Key == key);

    private static Pose Sample(AgentClip clip, int index)
    {
        float t = (float)index / FrameCounts[clip];
        return clip switch
        {
            AgentClip.Idle => Idle(t),
            AgentClip.Run => Run(t),
            AgentClip.Jump => Jump[index],
            AgentClip.Fall => Fall[index],
            AgentClip.Land => CharacterRig.Grounded(Pose.Lerp(Crouch, Idle(0f), index / 3f)),
            AgentClip.Swim => Swim(t),
            _ => Die[index],
        };
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

    private static Limb StrideAt(float t)
    {
        float k = (t - Mathf.Floor(t)) * Stride.Length;
        int i = (int)k;
        return Limb.Lerp(Stride[i], Stride[(i + 1) % Stride.Length], k - i);
    }

    private static Limb RunArm(float t)
    {
        float upper = -38f * Mathf.Cos(Mathf.Tau * t);
        return new Limb(upper, upper + 85f);
    }

    private static Pose Run(float t) => CharacterRig.Grounded(new Pose(Vector2.Zero, 11f,
        NearArm: RunArm(t), FarArm: RunArm(t + 0.5f),
        NearLeg: StrideAt(t), FarLeg: StrideAt(t + 0.5f)));

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
    private static Pose Swim(float t)
    {
        float stroke = 95f + 50f * Mathf.Cos(Mathf.Tau * t);
        return new Pose(Vector2.Zero, 14f,
            NearArm: new(stroke, stroke + 30f + 20f * Wave(t)),
            FarArm: new(stroke - 15f, stroke + 20f + 20f * Wave(t, 0.1f)),
            NearLeg: new(18f + 18f * Wave(t), -8f + 22f * Wave(t, -0.15f), 50f),
            FarLeg: new(18f - 18f * Wave(t), -8f - 22f * Wave(t, -0.15f), 50f));
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

    // Arms while the gun is out: both hands on the pistol at shoulder
    // height, kicked up a few degrees on the recoil beat. Measured against
    // the torso's lean so the aim stays level whatever the legs do.
    private static Pose ShotArms(Shot shot, float lean)
    {
        float kick = shot == Shot.Recoil ? 12f : 0f;
        float aim = 90f + kick;
        return new Pose(Vector2.Zero, lean,
            NearArm: new(aim - 12f, aim),
            FarArm: new(aim - 22f, aim - 4f),
            NearLeg: default, FarLeg: default);
    }
}
