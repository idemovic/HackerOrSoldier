using System;
using Godot;

namespace HackerOrSoldier;

// A three-tone material: lit, base and shaded. Far-side limbs use Far, a
// darker copy, so the two legs and two arms separate the way they do in
// rotoscoped DOS animation.
public readonly record struct Ramp(Color Hi, Color Mid, Color Lo)
{
    public Ramp(string hi, string mid, string lo) : this(new Color(hi), new Color(mid), new Color(lo)) { }
    public Ramp Far => new(Mid.Darkened(0.2f), Lo, Lo.Darkened(0.35f));
}

// One limb as two absolute segment angles plus the hand/foot angle, in
// degrees: 0 points straight down, +90 points the way the character faces,
// -90 behind it, 180 straight up. Absolute angles interpolate without the
// knee or elbow drifting when the hip or shoulder swings.
public readonly record struct Limb(float Upper, float Lower, float End = 90f)
{
    public static Limb Lerp(Limb a, Limb b, float t) => new(
        Mathf.Lerp(a.Upper, b.Upper, t), Mathf.Lerp(a.Lower, b.Lower, t), Mathf.Lerp(a.End, b.End, t));
}

// A full-body pose. Root moves the hip from its standing spot (px, +y down),
// Lean tilts the spine forward (negative falls backwards), Chest sinks the
// shoulders towards the hip for breathing and crouches.
public readonly record struct Pose(
    Vector2 Root, float Lean, Limb NearArm, Limb FarArm, Limb NearLeg, Limb FarLeg, float Chest = 0f)
{
    public static Pose Lerp(Pose a, Pose b, float t) => new(
        a.Root.Lerp(b.Root, t), Mathf.Lerp(a.Lean, b.Lean, t),
        Limb.Lerp(a.NearArm, b.NearArm, t), Limb.Lerp(a.FarArm, b.FarArm, t),
        Limb.Lerp(a.NearLeg, b.NearLeg, t), Limb.Lerp(a.FarLeg, b.FarLeg, t),
        Mathf.Lerp(a.Chest, b.Chest, t));

    public Pose WithArms(Pose arms) => this with { NearArm = arms.NearArm, FarArm = arms.FarArm };
}

// What a character is made of. The head is hand-drawn because a face does
// not survive procedural drawing at this size; everything below the neck is
// generated from the pose. HeadMap chars index HeadColors; '.' is empty and
// the bottom row's '^' marks the neck pixel the head hangs from. Without a
// Shirt the jacket front stays plain.
public sealed record Look(
    Ramp Skin, Ramp Jacket, Ramp Legs, Ramp Shoes, Ramp? Shirt, Ramp? Tie,
    string[] HeadMap, System.Collections.Generic.Dictionary<char, Color> HeadColors);

// Gun held in the near hand while shooting.
public enum Weapon { None, Pistol, Rifle }

// Rasterises a Pose with a Look into pixel art at native resolution: limbs
// as shaded capsules, the torso as a capsule with the shirt and tie cut into
// its front, the hand-drawn head, then edge lighting from the top-left and a
// one-pixel dark outline. Rendering the left-facing frame with mirrored
// lighting and flipping the result keeps the light on the same side as the
// tiles whichever way the character faces.
public static class CharacterRig
{
    public const int Width = 40;
    public const int Height = 32;

    // Standing hip position and segment lengths, sized so a standing figure
    // spans rows 2-31 of the frame: one tile tall, feet on the bottom row.
    private static readonly Vector2 Hip = new(19.5f, 19.5f);
    private const float Spine = 8.5f;
    private const float Thigh = 5.8f, Shin = 5.2f, Foot = 3f;
    private const float AnkleFloor = 30.5f;   // ankle height that puts the sole on the bottom row
    private const float UpperArm = 5f, Forearm = 4.6f;

    private enum Part { None, FarArm, FarHand, FarLeg, FarShoe, NearLeg, NearShoe, Torso, Shirt, Tie, Head, NearArm, NearHand, Gun, Flash }

    private static readonly Color Outline = new("0a0a10");
    private static readonly Ramp GunSteel = new("8a93a8", "4a5266", "23262f");
    private static readonly Ramp FlashFire = new("ffffff", "fff27a", "f28022");

    public static Image Render(Pose pose, Look look, bool facingLeft, Weapon weapon = Weapon.None, bool flash = false)
    {
        var parts = new Part[Width * Height];
        var headPixels = new Color[Width * Height];

        Vector2 hip = Hip + pose.Root;
        Vector2 up = Dir(180f - pose.Lean);
        Vector2 shoulder = hip + up * (Spine - pose.Chest);
        Vector2 armRoot = shoulder - up * 1.5f;

        DrawArm(parts, armRoot, pose.FarArm, Part.FarArm, Part.FarHand, Weapon.None, false);
        DrawLeg(parts, hip, pose.FarLeg, Part.FarLeg, Part.FarShoe);
        DrawLeg(parts, hip, pose.NearLeg, Part.NearLeg, Part.NearShoe);
        DrawTorso(parts, hip, shoulder, up, Dir(90f - pose.Lean), look.Shirt != null);
        DrawHead(parts, headPixels, shoulder + up * 0.5f, pose.Lean, look);
        DrawArm(parts, armRoot, pose.NearArm, Part.NearArm, Part.NearHand, weapon, flash);

        var img = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var part = parts[y * Width + x];
                if (part == Part.None)
                {
                    if (Touches(parts, x, y))
                        img.SetPixel(x, y, Outline);
                    continue;
                }
                img.SetPixel(x, y, part == Part.Head
                    ? headPixels[y * Width + x]
                    : Lit(RampFor(part, look), parts, x, y, part, facingLeft));
            }
        if (facingLeft)
            img.FlipX();
        return img;
    }

    // Moves the hip vertically so the lower foot rests on the floor row:
    // knees bend and the body dips instead of the feet sinking or floating.
    public static Pose Grounded(Pose pose)
    {
        float Ankle(Limb leg) => Dir(leg.Upper).Y * Thigh + Dir(leg.Lower).Y * Shin;
        float lowest = Mathf.Max(Ankle(pose.NearLeg), Ankle(pose.FarLeg));
        return pose with { Root = new Vector2(pose.Root.X, AnkleFloor - Hip.Y - lowest) };
    }

    private static Ramp RampFor(Part part, Look look) => part switch
    {
        Part.FarArm => look.Jacket.Far,
        Part.FarHand => look.Skin.Far,
        Part.FarLeg => look.Legs.Far,
        Part.FarShoe => look.Shoes.Far,
        Part.NearLeg => look.Legs,
        Part.NearShoe => look.Shoes,
        Part.Torso or Part.NearArm => look.Jacket,
        Part.Shirt => look.Shirt.Value,
        Part.Tie => look.Tie ?? look.Shirt.Value,
        Part.NearHand => look.Skin,
        Part.Gun => GunSteel,
        Part.Flash => FlashFire,
        _ => look.Jacket,
    };

    // Edge lighting per part: the side facing the light picks up the lit
    // tone, the far side the shaded one, so every limb reads as a rounded
    // form and the near arm separates from the jacket behind it. Facing
    // left, the light sits behind the character, so the rule mirrors before
    // the frame is flipped.
    private static Color Lit(Ramp ramp, Part[] parts, int x, int y, Part part, bool facingLeft)
    {
        int towardLight = facingLeft ? 1 : -1;
        bool lit = !Is(parts, x, y - 1, part) || !Is(parts, x + towardLight, y, part);
        bool shaded = !Is(parts, x, y + 1, part) || !Is(parts, x - towardLight, y, part);
        if (lit && !shaded)
            return ramp.Hi;
        if (shaded && !lit)
            return ramp.Lo;
        // A pixel lit on top and shaded underneath is a one-pixel-thick run;
        // the top edge wins on a horizontal run, the back on a vertical one.
        if (lit && shaded)
            return Is(parts, x, y - 1, part) ? ramp.Lo : ramp.Mid;
        // Interior pixels dither towards the shade on the lower half of
        // wide parts (the torso), as the VGA originals did.
        return !Is(parts, x, y + 2, part) && (x + y) % 2 == 0 ? ramp.Lo : ramp.Mid;
    }

    private static bool Is(Part[] parts, int x, int y, Part part) =>
        x >= 0 && y >= 0 && x < Width && y < Height && parts[y * Width + x] == part;

    private static bool Touches(Part[] parts, int x, int y)
    {
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int nx = x + dx, ny = y + dy;
            if (nx >= 0 && ny >= 0 && nx < Width && ny < Height
                && parts[ny * Width + nx] is not (Part.None or Part.Flash))
                return true;
        }
        return false;
    }

    // Unit vector for an absolute angle (0 = down, +90 = facing direction).
    private static Vector2 Dir(float degrees)
    {
        float r = Mathf.DegToRad(degrees);
        return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
    }

    private static void DrawLeg(Part[] parts, Vector2 hip, Limb leg, Part limb, Part shoe)
    {
        Vector2 knee = hip + Dir(leg.Upper) * Thigh;
        Vector2 ankle = knee + Dir(leg.Lower) * Shin;
        Capsule(parts, hip, knee, 1.9f, limb);
        Capsule(parts, knee, ankle, 1.45f, limb);
        Capsule(parts, ankle + Dir(leg.End) * -0.5f, ankle + Dir(leg.End) * Foot, 1.05f, shoe);
    }

    private static void DrawArm(Part[] parts, Vector2 shoulder, Limb arm, Part limb, Part hand, Weapon weapon, bool flash)
    {
        Vector2 elbow = shoulder + Dir(arm.Upper) * UpperArm;
        Vector2 wrist = elbow + Dir(arm.Lower) * Forearm;
        Capsule(parts, shoulder, elbow, 1.3f, limb);
        Capsule(parts, elbow, wrist, 1.15f, limb);
        Capsule(parts, wrist, wrist + Dir(arm.Lower) * 0.8f, 1.0f, hand);
        if (weapon == Weapon.None)
            return;
        // The gun extends the forearm along the aim line with a grip (and on
        // the rifle a magazine) dropping below it; the rifle's stock reaches
        // back past the hand towards the shoulder.
        bool rifle = weapon == Weapon.Rifle;
        Vector2 aim = Dir(arm.Lower), below = Dir(arm.Lower - 90f);
        Vector2 muzzle = wrist + aim * (rifle ? 8f : 4.5f);
        Capsule(parts, wrist + aim * (rifle ? -4f : 0.5f), muzzle, 0.9f, Part.Gun);
        Capsule(parts, wrist, wrist + below * 1.8f, 0.7f, Part.Gun);
        if (rifle)
            Capsule(parts, wrist + aim * 2.5f, wrist + aim * 2.5f + below * 2.2f, 0.7f, Part.Gun);
        if (flash)
        {
            Capsule(parts, muzzle + aim * 1.5f, muzzle + aim * 4.5f, 1.4f, Part.Flash);
            Capsule(parts, muzzle + aim * 2.5f + Dir(arm.Lower + 90f) * 2f,
                muzzle + aim * 2.5f + Dir(arm.Lower - 90f) * 2f, 0.6f, Part.Flash);
        }
    }

    // The torso is a capsule along the spine; the shirt and tie are the
    // pixels near its front edge in the upper chest, measured in spine
    // space so they lean with the body.
    private static void DrawTorso(Part[] parts, Vector2 hip, Vector2 shoulder, Vector2 up, Vector2 front, bool shirt)
    {
        Capsule(parts, hip + up * 0.8f, shoulder - up * 1.2f, 3.5f, Part.Torso);
        if (!shirt)
            return;
        Vector2 down = -up;
        Vector2 neck = shoulder + down * 0.5f;
        ForEachPixel(neck - new Vector2(6, 6), neck + new Vector2(6, 10), (x, y, center) =>
        {
            if (parts[y * Width + x] != Part.Torso)
                return;
            Vector2 d = center - neck;
            float along = d.Dot(down), side = d.Dot(front);
            if (along < 0f || along > 5.5f)
                return;
            if (side > 0.6f && side < 1.9f && along > 0.8f)
                parts[y * Width + x] = Part.Tie;
            else if (side >= 1.9f || side > 0.6f && along <= 0.8f)
                parts[y * Width + x] = Part.Shirt;
        });
    }

    private static void DrawHead(Part[] parts, Color[] pixels, Vector2 neck, float lean, Look look)
    {
        var map = look.HeadMap;
        int anchorX = map[^1].IndexOf('^');
        // A forward lean moves the head ahead of the neck one pixel at a time
        // rather than rotating the face, which would smear it.
        int ox = Mathf.RoundToInt(neck.X) - anchorX;
        int oy = Mathf.RoundToInt(neck.Y) - (map.Length - 1);
        if (Mathf.Abs(lean) > 60f)
        {
            // Lying flat: the head lies on its side next to the shoulders.
            DrawHeadRotated(parts, pixels, neck, lean, look);
            return;
        }
        for (int my = 0; my < map.Length; my++)
            for (int mx = 0; mx < map[my].Length; mx++)
            {
                char ch = map[my][mx];
                if (ch == '.')
                    continue;
                Plot(parts, pixels, ox + mx, oy + my, look.HeadColors[ch]);
            }
    }

    // Quarter turns only, so the face stays crisp: backwards falls lay the
    // head with its crown pointing behind the body.
    private static void DrawHeadRotated(Part[] parts, Color[] pixels, Vector2 neck, float lean, Look look)
    {
        var map = look.HeadMap;
        int h = map.Length, anchorX = map[^1].IndexOf('^');
        bool backwards = lean < 0f;
        int nx = Mathf.RoundToInt(neck.X), ny = Mathf.RoundToInt(neck.Y);
        for (int my = 0; my < h; my++)
            for (int mx = 0; mx < map[my].Length; mx++)
            {
                char ch = map[my][mx];
                if (ch == '.')
                    continue;
                int up = h - 1 - my, side = mx - anchorX;
                int px = backwards ? nx - up : nx + up;
                int py = backwards ? ny - side : ny + side;
                Plot(parts, pixels, px, py, look.HeadColors[ch]);
            }
    }

    private static void Plot(Part[] parts, Color[] pixels, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;
        parts[y * Width + x] = Part.Head;
        pixels[y * Width + x] = color;
    }

    private static void Capsule(Part[] parts, Vector2 a, Vector2 b, float radius, Part part)
    {
        var min = new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)) - Vector2.One * (radius + 1);
        var max = new Vector2(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y)) + Vector2.One * (radius + 1);
        ForEachPixel(min, max, (x, y, center) =>
        {
            if (DistanceToSegment(center, a, b) <= radius)
                parts[y * Width + x] = part;
        });
    }

    private static void ForEachPixel(Vector2 min, Vector2 max, Action<int, int, Vector2> visit)
    {
        int x0 = Math.Max(0, (int)Mathf.Floor(min.X)), x1 = Math.Min(Width - 1, (int)Mathf.Ceil(max.X));
        int y0 = Math.Max(0, (int)Mathf.Floor(min.Y)), y1 = Math.Min(Height - 1, (int)Mathf.Ceil(max.Y));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                visit(x, y, new Vector2(x + 0.5f, y + 0.5f));
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.LengthSquared() < 1e-6f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }
}
