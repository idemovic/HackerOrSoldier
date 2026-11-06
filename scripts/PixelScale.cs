using Godot;

namespace HackerOrSoldier;

// Scales the game by the largest whole number that still shows the full
// 640x360 base screen, and lets the view grow to cover the rest of the
// window. Godot's integer stretch mode floors the scale after fitting the
// aspect, so a maximized window kept black bars of up to one scale step on
// every side. Autoloaded, so every scene gets it before its own _Ready.
public partial class PixelScale : Node
{
    private static readonly Vector2I BaseSize = new(
        (int)ProjectSettings.GetSetting("display/window/size/viewport_width"),
        (int)ProjectSettings.GetSetting("display/window/size/viewport_height"));

    public override void _EnterTree()
    {
        var window = GetTree().Root;
        window.ContentScaleSize = BaseSize;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        window.SizeChanged += () => Apply(window);
        Apply(window);
    }

    private static void Apply(Window window)
    {
        int scale = Mathf.Min(window.Size.X / BaseSize.X, window.Size.Y / BaseSize.Y);
        // A window smaller than the base screen (or the headless one) shrinks
        // it to fit rather than cutting it off.
        var mode = scale >= 1 ? Window.ContentScaleModeEnum.Disabled : Window.ContentScaleModeEnum.CanvasItems;
        float factor = Mathf.Max(1, scale);
        // Either change resizes the viewport, which fires SizeChanged again.
        if (window.ContentScaleMode != mode)
            window.ContentScaleMode = mode;
        if (window.ContentScaleFactor != factor)
            window.ContentScaleFactor = factor;
    }
}
