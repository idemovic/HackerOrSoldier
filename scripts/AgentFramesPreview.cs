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
    private const int Cell = 48;

    public override void _Ready()
    {
        AgentSheet("user://agent_frames.png");
        Sheet("user://enemy_frames.png", EnemyAnimator.AllFrames, columns: 8);
        GetTree().Quit();
    }

    // One row per clip, right-facing then left-facing, so the lighting can
    // be checked on both sides. The agent is lit per facing, not mirrored.
    private static void AgentSheet(string path)
    {
        var clips = AgentPoses.FrameCounts;
        int columns = clips.Values.Max();
        int rows = clips.Count * 2 + 2;
        var sheet = Image.CreateEmpty(columns * Cell, rows * Cell, false, Image.Format.Rgba8);
        sheet.FillRect(new Rect2I(0, 0, sheet.GetWidth(), clips.Count * Cell), new Color(0.10f, 0.10f, 0.16f));
        sheet.FillRect(new Rect2I(0, clips.Count * Cell, sheet.GetWidth(), (rows - clips.Count) * Cell), new Color(0.42f, 0.42f, 0.46f));
        void Put(AgentFrame frame, int column, int row)
        {
            var image = AgentPoses.Texture(frame).GetImage();
            image.Convert(Image.Format.Rgba8);
            sheet.BlendRect(image, new Rect2I(Vector2I.Zero, image.GetSize()), new Vector2I(column * Cell + 4, row * Cell + 8));
        }
        int r = 0;
        foreach (var left in new[] { false, true })
            foreach (var (clip, count) in clips)
            {
                for (int i = 0; i < count; i++)
                    Put(new AgentFrame(clip, i, Left: left), i, r);
                r++;
            }
        // The gun overlay on the run cycle, both facings.
        for (int i = 0; i < clips[AgentClip.Run]; i++)
        {
            Put(new AgentFrame(AgentClip.Run, i, (Shot)(1 + i % 3)), i, r);
            Put(new AgentFrame(AgentClip.Run, i, (Shot)(1 + i % 3), Left: true), i, r + 1);
        }
        sheet.Resize(sheet.GetWidth() * 4, sheet.GetHeight() * 4, Image.Interpolation.Nearest);
        sheet.SavePng(path);
        GD.Print($"{path}: {AgentAnimator.AllFrames.Length} poses");
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
