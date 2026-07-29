using Godot;

namespace HackerOrSoldier;

// The whole-screen death effect, in the same DOS idiom as the sprites: a hard
// red palette flash on the hit, a few frames of camera shake, then a chunky
// block dissolve to black while the agent goes down. No shader and no
// tweening - coarse rectangles switching on a handful at a time, the way a
// VGA game wiped the screen between lives. Lives for exactly one death:
// PlaySession frees it when the agent respawns, which snaps the level back.
public partial class DeathEffect : CanvasLayer
{
    private const int Block = 32;             // one tile: the game's own pixel grid
    private const float FlashTime = 0.22f;    // the red burn, front-loaded
    private const float FlashAlpha = 0.75f;
    private const float DissolveStart = 0.12f;
    private const float ShakeTime = 0.3f;
    private const float ShakeStep = 1f / 30f; // the offset flips twice per DOS-era frame
    private const float ShakeAmplitude = 4f;

    private const uint ScatterSeed = 0x5EED1234;   // see _order

    private static readonly Color FlashColor = new("e03b2f");   // palette red
    private static readonly Color BlockColor = new("141419");   // palette black

    // How long the screen stays covered. The dissolve fills the last block
    // before this, so the respawn happens behind a fully black screen.
    public float Duration { get; init; } = 1.2f;

    // Shaken on impact and put back when the effect goes away.
    public Camera2D Camera { get; init; }

    private ColorRect _flash;
    private ColorRect[] _blocks;
    private int[] _order;
    private float _time;
    private int _lit;

    public override void _Ready()
    {
        Layer = 3;   // over the level and the HUD both

        var size = GetViewport().GetVisibleRect().Size;
        _flash = new ColorRect
        {
            Size = size,
            Color = new Color(FlashColor, FlashAlpha),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_flash);

        // One extra block on each axis so a viewport that is not a whole
        // number of blocks wide still ends up completely covered.
        int columns = Mathf.CeilToInt(size.X / Block) + 1;
        int rows = Mathf.CeilToInt(size.Y / Block) + 1;
        _blocks = new ColorRect[columns * rows];
        for (int i = 0; i < _blocks.Length; i++)
        {
            _blocks[i] = new ColorRect
            {
                Position = new Vector2(i % columns * Block, i / columns * Block),
                Size = new Vector2(Block, Block),
                Color = BlockColor,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddChild(_blocks[i]);
        }

        // The order the blocks come on in. Shuffled from a fixed seed rather
        // than sampled at run time: it scatters (stepping through the grid by
        // any fixed stride bands into diagonals instead), it covers every
        // block exactly once, and every death dissolves the same way.
        _order = new int[_blocks.Length];
        for (int i = 0; i < _order.Length; i++)
            _order[i] = i;
        uint seed = ScatterSeed;
        for (int i = _order.Length - 1; i > 0; i--)
        {
            seed = seed * 1664525 + 1013904223;
            int j = (int)(seed >> 16) % (i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
    }

    public override void _ExitTree()
    {
        if (Camera != null && GodotObject.IsInstanceValid(Camera))
            Camera.Offset = Vector2.Zero;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;

        _flash.Color = new Color(FlashColor, FlashAlpha * Mathf.Max(1f - _time / FlashTime, 0f));

        // The dissolve has to finish inside Duration, so the last block lands
        // before the respawn rather than at the same moment as it.
        float span = Mathf.Max(Duration - DissolveStart - 0.2f, 0.1f);
        float progress = (_time - DissolveStart) / span;
        int target = Mathf.Clamp(Mathf.RoundToInt(progress * _blocks.Length), 0, _blocks.Length);
        for (; _lit < target; _lit++)
            _blocks[_order[_lit]].Visible = true;

        Shake();
    }

    private void Shake()
    {
        if (Camera == null || !GodotObject.IsInstanceValid(Camera))
            return;
        if (_time >= ShakeTime)
        {
            Camera.Offset = Vector2.Zero;
            return;
        }
        // Whole-pixel offsets flipping on a coarse clock, decaying to nothing:
        // a jolt, not a wobble.
        float amplitude = ShakeAmplitude * (1f - _time / ShakeTime);
        int step = (int)(_time / ShakeStep);
        Camera.Offset = new Vector2(
            Mathf.Round(amplitude) * (step % 2 == 0 ? 1 : -1),
            Mathf.Round(amplitude * 0.5f) * (step % 4 < 2 ? -1 : 1));
    }
}
