using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

// A copied block of a level. Objects sit relative to the block's top-left.
public sealed class LevelClip
{
    public Vector2I Size { get; }
    public int[] Tiles { get; }
    public int[] Background { get; }
    public IReadOnlyList<LevelObject> Objects { get; }

    private LevelClip(Vector2I size, int[] tiles, int[] background, List<LevelObject> objects)
    {
        Size = size;
        Tiles = tiles;
        Background = background;
        Objects = objects;
    }

    // Copies the part of the area inside the level, or null when none is.
    // The agent start stays behind: a level has exactly one.
    public static LevelClip Copy(LevelData level, Rect2I area)
    {
        area = area.Abs().Intersection(new Rect2I(0, 0, level.Width, level.Height));
        if (!area.HasArea())
            return null;
        var tiles = new int[area.Size.X * area.Size.Y];
        var background = new int[tiles.Length];
        for (int y = 0; y < area.Size.Y; y++)
            for (int x = 0; x < area.Size.X; x++)
            {
                tiles[y * area.Size.X + x] = level.GetTile(area.Position.X + x, area.Position.Y + y);
                background[y * area.Size.X + x] = level.GetBackground(area.Position.X + x, area.Position.Y + y);
            }
        var objects = level.Objects
            .Where(o => o.Type != ObjectDefs.AgentStart && area.HasPoint(new Vector2I(o.X, o.Y)))
            .Select(o => Moved(o, -area.Position))
            .ToList();
        return new LevelClip(area.Size, tiles, background, objects);
    }

    // The cells of the level a paste at origin would overwrite.
    public Rect2I FootprintAt(LevelData level, Vector2I origin) =>
        new Rect2I(origin, Size).Intersection(new Rect2I(0, 0, level.Width, level.Height));

    // Replaces the footprint with the clip and returns the rectangle written.
    // The level's agent start is never removed or covered.
    public Rect2I PasteInto(LevelData level, Vector2I origin)
    {
        var footprint = FootprintAt(level, origin);
        if (!footprint.HasArea())
            return footprint;
        for (int y = footprint.Position.Y; y < footprint.End.Y; y++)
            for (int x = footprint.Position.X; x < footprint.End.X; x++)
            {
                level.SetTile(x, y, Tiles[(y - origin.Y) * Size.X + (x - origin.X)]);
                level.SetBackground(x, y, Background[(y - origin.Y) * Size.X + (x - origin.X)]);
            }
        var start = level.Objects.Find(o => o.Type == ObjectDefs.AgentStart);
        var startCell = start == null ? (Vector2I?)null : new Vector2I(start.X, start.Y);
        level.Objects.RemoveAll(o => o.Type != ObjectDefs.AgentStart && footprint.HasPoint(new Vector2I(o.X, o.Y)));
        foreach (var obj in Objects)
        {
            var placed = Moved(obj, origin);
            var cell = new Vector2I(placed.X, placed.Y);
            if (footprint.HasPoint(cell) && cell != startCell)
                level.Objects.Add(placed);
        }
        return footprint;
    }

    private static LevelObject Moved(LevelObject o, Vector2I by) => new()
    {
        Type = o.Type,
        X = o.X + by.X,
        Y = o.Y + by.Y,
        Properties = new Dictionary<string, string>(o.Properties),
    };
}
