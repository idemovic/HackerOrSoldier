using Godot;

namespace HackerOrSoldier;

// A pulsing glow ring behind an admin password so the player can spot the
// sticky note. Ordered-dither rings in the password's green, three radii
// that step through a slow in-out cycle: no smooth gradients (README).
public partial class PasswordHalo : Sprite2D
{
    private const int Size = 56;
    private const float StepTime = 0.25f;
    private static readonly int[] Cycle = { 0, 1, 2, 3, 2, 1 };
    private static readonly float[] Radius = { 18f, 19.5f, 21f, 22.5f };
    private static readonly int[,] Bayer = { { 0, 2 }, { 3, 1 } };
    private static ImageTexture[] _frames;

    private float _time;

    public static Color Tint => CollectibleDefs.Get(CollectibleDefs.AdminPassword).Color;

    public static int FrameCount => Radius.Length;

    public static ImageTexture Frame(int index)
    {
        _frames ??= BuildFrames();
        return _frames[index];
    }

    public override void _Ready()
    {
        Texture = Frame(Cycle[0]);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        Texture = Frame(Cycle[(int)(_time / StepTime) % Cycle.Length]);
    }

    private static ImageTexture[] BuildFrames()
    {
        var frames = new ImageTexture[Radius.Length];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = ImageTexture.CreateFromImage(Ring(Radius[i]));
        return frames;
    }

    // Three dither densities fading out toward the rim: 3/4, 2/4 and 1/4
    // of the pixels lit. The sticky note covers the middle.
    private static Image Ring(float outer)
    {
        var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        var color = new Color(Tint.R, Tint.G, Tint.B, 0.75f);
        float center = Size / 2f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = new Vector2(x + 0.5f - center, y + 0.5f - center).Length();
                int density = d <= outer - 4f ? 3 : d <= outer - 2f ? 2 : d <= outer ? 1 : 0;
                if (density > Bayer[y % 2, x % 2])
                    img.SetPixel(x, y, color);
            }
        return img;
    }
}
