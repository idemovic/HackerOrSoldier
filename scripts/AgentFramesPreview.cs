using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Temporary helper: renders every character animation frame into contact
// sheets at user://agent_frames.png and user://enemy_frames.png, then quits.
// Not part of the game. Each sheet has the set on a dark background, on a
// mid-tone background, and mirrored - the mirrored band is what the player
// sees when the character walks the other way.
public partial class AgentFramesPreview : Node
{
    private const int Cell = 40;

    public override void _Ready()
    {
        Sheet("user://agent_frames.png", AgentAnimator.AllFrames, columns: 6);
        Sheet("user://enemy_frames.png", EnemyAnimator.AllFrames, columns: 8);
        GetTree().Quit();
    }

    private static void Sheet(string path, string[] frames, int columns)
    {
        int rows = (frames.Length + columns - 1) / columns;
        var bands = new[]
        {
            (Background: new Color(0.10f, 0.10f, 0.16f), Flip: false),
            (Background: new Color(0.42f, 0.42f, 0.46f), Flip: false),
            (Background: new Color(0.42f, 0.42f, 0.46f), Flip: true),
        };
        var sheet = Image.CreateEmpty(columns * Cell, bands.Length * rows * Cell, false, Image.Format.Rgba8);

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
                var at = new Vector2I(i % columns * Cell + 4, top + i / columns * Cell + 4);
                sheet.BlendRect(frame, new Rect2I(Vector2I.Zero, frame.GetSize()), at);
            }
        }

        sheet.Resize(sheet.GetWidth() * 4, sheet.GetHeight() * 4, Image.Interpolation.Nearest);
        sheet.SavePng(path);
        GD.Print($"{path}: {frames.Length} frames -> {string.Join(", ", frames.Take(3))}...");
    }
}
