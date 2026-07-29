using Godot;

namespace HackerOrSoldier;

// Temporary helper: renders every agent animation frame into a contact sheet
// at user://agent_frames.png and quits. Not part of the game. Each band is
// the full set on one background; the flipped band is what the player sees
// walking the other way.
public partial class AgentFramesPreview : Node
{
    private const int Cell = 40;
    private const int Columns = 6;

    public override void _Ready()
    {
        var frames = AgentAnimator.AllFrames;
        int rows = (frames.Length + Columns - 1) / Columns;
        var bands = new[]
        {
            (Background: new Color(0.10f, 0.10f, 0.16f), Flip: false),
            (Background: new Color(0.42f, 0.42f, 0.46f), Flip: false),
            (Background: new Color(0.42f, 0.42f, 0.46f), Flip: true),
        };
        var sheet = Image.CreateEmpty(Columns * Cell, bands.Length * rows * Cell, false, Image.Format.Rgba8);

        for (int band = 0; band < bands.Length; band++)
        {
            int top = band * rows * Cell;
            sheet.FillRect(new Rect2I(0, top, sheet.GetWidth(), rows * Cell), bands[band].Background);
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = Sprites.Get(frames[i]).GetImage();
                frame.Convert(Image.Format.Rgba8);
                if (bands[band].Flip)
                    frame.FlipX();
                var at = new Vector2I(i % Columns * Cell + 4, top + i / Columns * Cell + 4);
                sheet.BlendRect(frame, new Rect2I(Vector2I.Zero, frame.GetSize()), at);
            }
        }

        sheet.Resize(sheet.GetWidth() * 4, sheet.GetHeight() * 4, Image.Interpolation.Nearest);
        sheet.SavePng("user://agent_frames.png");
        GD.Print($"wrote {frames.Length} frames");
        GetTree().Quit();
    }
}
