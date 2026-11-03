using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Temporary helper: renders every character animation frame into contact
// sheets at user://<type>_frames.png (one per rigged character) and
// user://enemy_frames.png (the map-based enemies), then quits. Not part of
// the game. Map-based sheets show the set on dark, on mid-tone, and
// mirrored, which is what the player sees when the enemy turns around.
public partial class AgentFramesPreview : Node
{
    private const int Cell = 48;

    public override void _Ready()
    {
        BodySheet("user://agent_frames.png", BodyPoses.Agent);
        foreach (var who in new[] { EnemyDefs.Fighter, EnemyDefs.Soldier, EnemyDefs.SuperSoldier, EnemyDefs.UltimateSoldier })
            BodySheet($"user://{who}_frames.png", who);
        Sheet("user://enemy_frames.png", EnemyAnimator.AllFrames.Where(k => !BodyPoses.IsRigged(BodyPoses.Find(k)?.Who)).ToArray(), columns: 8);
        GetTree().Quit();
    }

    // One row per clip, right-facing on dark then left-facing on mid-grey,
    // so the lighting can be checked on both sides: rigged characters are
    // lit per facing, not mirrored. Armed characters add their gun overlay
    // on the clip that has the most frames.
    private static void BodySheet(string path, string who)
    {
        var plain = BodyPoses.All(who).Where(f => f.Shot == Shot.None).ToArray();
        var clips = plain.Select(f => f.Clip).Distinct().ToArray();
        var shots = BodyPoses.All(who).Where(f => f.Shot != Shot.None && f.Index % 3 == (int)f.Shot - 1)
            .GroupBy(f => f.Clip).OrderByDescending(g => g.Count()).FirstOrDefault()?.ToArray() ?? new BodyFrame[0];
        int columns = clips.Max(c => BodyPoses.FrameCounts[c]);
        int bandRows = clips.Length + (shots.Length > 0 ? 1 : 0);
        var sheet = Image.CreateEmpty(columns * Cell, 2 * bandRows * Cell, false, Image.Format.Rgba8);
        sheet.FillRect(new Rect2I(0, 0, sheet.GetWidth(), bandRows * Cell), new Color(0.10f, 0.10f, 0.16f));
        sheet.FillRect(new Rect2I(0, bandRows * Cell, sheet.GetWidth(), bandRows * Cell), new Color(0.42f, 0.42f, 0.46f));
        for (int band = 0; band < 2; band++)
        {
            bool left = band == 1;
            var rows = clips.Select(c => plain.Where(f => f.Clip == c).ToArray()).ToList();
            if (shots.Length > 0)
                rows.Add(shots);
            for (int r = 0; r < rows.Count; r++)
                for (int i = 0; i < rows[r].Length; i++)
                {
                    var image = BodyPoses.Texture(rows[r][i] with { Left = left }).GetImage();
                    image.Convert(Image.Format.Rgba8);
                    var at = new Vector2I(i * Cell + 4, (band * bandRows + r) * Cell + 8);
                    sheet.BlendRect(image, new Rect2I(Vector2I.Zero, image.GetSize()), at);
                }
        }
        sheet.Resize(sheet.GetWidth() * 4, sheet.GetHeight() * 4, Image.Interpolation.Nearest);
        sheet.SavePng(path);
        GD.Print($"{path}: {BodyPoses.All(who).Count()} poses");
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
