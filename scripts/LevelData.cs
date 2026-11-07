using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace HackerOrSoldier;

public class LevelObject
{
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class LevelData
{
    public const int DefaultWidth = 80;
    public const int DefaultHeight = 80;

    // Bump CurrentFormatVersion when a change would misread older files,
    // and convert those files in LevelStore.Load. Files saved before the
    // field existed have none and read as version 1.
    // 2: the pipe and sign decorations were removed.
    // 3: background walls moved from Tiles into their own Background layer.
    public const int CurrentFormatVersion = 3;

    public int FormatVersion { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    // Cells with no tile are the outdoors; the sky ("night" or "day")
    // fills the level behind everything.
    public string Sky { get; set; } = "night";
    public int Width { get; set; } = DefaultWidth;
    public int Height { get; set; } = DefaultHeight;
    public int[] Tiles { get; set; } = new int[DefaultWidth * DefaultHeight];
    // Background walls, drawn behind Tiles; 0 where there is none.
    public int[] Background { get; set; } = new int[DefaultWidth * DefaultHeight];
    public List<LevelObject> Objects { get; set; } = new();

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public int GetTile(int x, int y) => InBounds(x, y) ? Tiles[y * Width + x] : TileDefs.Empty;

    public void SetTile(int x, int y, int tileId)
    {
        if (InBounds(x, y))
            Tiles[y * Width + x] = tileId;
    }

    public int GetBackground(int x, int y) => InBounds(x, y) ? Background[y * Width + x] : TileDefs.Empty;

    public void SetBackground(int x, int y, int tileId)
    {
        if (InBounds(x, y))
            Background[y * Width + x] = tileId;
    }

    // The tile on the layer that id belongs to.
    public int GetLayerTile(int x, int y, int id) =>
        TileDefs.IsBackground(id) ? GetBackground(x, y) : GetTile(x, y);

    // Writes id to its own layer and leaves the other layer alone.
    public void Paint(int x, int y, int id)
    {
        if (TileDefs.IsBackground(id))
            SetBackground(x, y, id);
        else
            SetTile(x, y, id);
    }

    // The cells a paint bucket fills from start: every cell reachable through
    // edge neighbours that holds the same tile and the same background.
    // Cells holding an object are left out, so a fill never buries one.
    public List<Vector2I> FillRegion(Vector2I start)
    {
        var region = new List<Vector2I>();
        if (!InBounds(start.X, start.Y))
            return region;
        int target = GetTile(start.X, start.Y);
        int targetBack = GetBackground(start.X, start.Y);
        var occupied = Objects.Select(o => new Vector2I(o.X, o.Y)).ToHashSet();
        var seen = new HashSet<Vector2I> { start };
        var open = new Stack<Vector2I>();
        open.Push(start);
        while (open.Count > 0)
        {
            var cell = open.Pop();
            if (GetTile(cell.X, cell.Y) != target || GetBackground(cell.X, cell.Y) != targetBack || occupied.Contains(cell))
                continue;
            region.Add(cell);
            foreach (var next in new[] { cell + Vector2I.Left, cell + Vector2I.Right, cell + Vector2I.Up, cell + Vector2I.Down })
                if (InBounds(next.X, next.Y) && seen.Add(next))
                    open.Push(next);
        }
        return region;
    }

    public LevelData Clone() => new()
    {
        FormatVersion = FormatVersion,
        Name = Name,
        Description = Description,
        Sky = Sky,
        Width = Width,
        Height = Height,
        Tiles = (int[])Tiles.Clone(),
        Background = (int[])Background.Clone(),
        Objects = Objects.Select(o => new LevelObject
        {
            Type = o.Type,
            X = o.X,
            Y = o.Y,
            Properties = new Dictionary<string, string>(o.Properties),
        }).ToList(),
    };

    // Null means valid, otherwise a user-facing error message.
    public string Validate()
    {
        int starts = Objects.Count(o => o.Type == ObjectDefs.AgentStart);
        if (starts != 1)
            return "Level must contain exactly one agent start.";
        if (!Objects.Any(o => o.Type == ObjectDefs.ExitDoor))
            return "Level must contain at least one exit door.";
        return null;
    }

    public static LevelData NewDefault(int width = DefaultWidth, int height = DefaultHeight)
    {
        var level = new LevelData
        {
            FormatVersion = CurrentFormatVersion, Width = width, Height = height,
            Tiles = new int[width * height], Background = new int[width * height],
        };
        int wall = TileDefs.All[0].Id;
        for (int x = 0; x < level.Width; x++)
            level.SetTile(x, level.Height - 1, wall);
        level.Objects.Add(new LevelObject { Type = ObjectDefs.AgentStart, X = 2, Y = level.Height - 2 });
        level.Objects.Add(new LevelObject { Type = ObjectDefs.ExitDoor, X = level.Width - 3, Y = level.Height - 2 });
        return level;
    }
}

public static class LevelStore
{
    // Run from the project, levels live in the repo's levels/ folder, so
    // they are committed and pushed like code and open on any checkout. An
    // exported build cannot write its own files and keeps them per player.
    public static readonly string DirPath = OS.HasFeature("template") ? "user://levels" : "res://levels";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(LevelData level)
    {
        level.FormatVersion = LevelData.CurrentFormatVersion;
        return JsonSerializer.Serialize(level, JsonOptions);
    }

    // Writes beside the target and renames over it, so a crash mid-write
    // leaves the old file intact instead of a truncated one.
    public static void WriteAtomic(string godotPath, string text)
    {
        string path = ProjectSettings.GlobalizePath(godotPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllText(path + ".tmp", text);
        System.IO.File.Move(path + ".tmp", path, overwrite: true);
    }

    // The previous version of each level is kept in user://backups, one per level.
    public static bool Save(LevelData level)
    {
        string fileName = FileNameFor(level.Name);
        string target = $"{DirPath}/{fileName}";
        try
        {
            string json = Serialize(level);
            string existing = ProjectSettings.GlobalizePath(target);
            if (System.IO.File.Exists(existing))
                WriteAtomic($"user://backups/{fileName}", System.IO.File.ReadAllText(existing));
            WriteAtomic(target, json);
            return true;
        }
        catch (System.Exception e) when (e is System.IO.IOException or System.UnauthorizedAccessException)
        {
            GD.PushError($"Saving {target} failed: {e.Message}");
            return false;
        }
    }

    public static LevelData Load(string fileName)
    {
        using var file = FileAccess.Open($"{DirPath}/{fileName}", FileAccess.ModeFlags.Read);
        return file == null ? null : Parse(file.GetAsText());
    }

    public static LevelData Parse(string json)
    {
        var level = JsonSerializer.Deserialize<LevelData>(json, JsonOptions);
        if (level?.Tiles == null || level.Tiles.Length != level.Width * level.Height)
            return null;
        // A file from a newer build may mean something this one would misread.
        if (level.FormatVersion > LevelData.CurrentFormatVersion)
            return null;
        // Before version 3 there is no background layer; a missing one would
        // otherwise keep the default size.
        if (level.FormatVersion < 3)
            level.Background = new int[level.Tiles.Length];
        if (level.Background?.Length != level.Tiles.Length)
            return null;
        level.Objects ??= new List<LevelObject>();
        // Unknown types would otherwise load as pickups.
        if (level.FormatVersion < 2)
            level.Objects.RemoveAll(o => o.Type is "deco_pipe" or "deco_sign");
        if (level.FormatVersion < 3)
            for (int i = 0; i < level.Tiles.Length; i++)
                if (TileDefs.IsBackground(level.Tiles[i]))
                    (level.Background[i], level.Tiles[i]) = (level.Tiles[i], TileDefs.Empty);
        return level;
    }

    public static string[] ListFiles()
    {
        var dir = DirAccess.Open(DirPath);
        return dir == null
            ? System.Array.Empty<string>()
            : System.Array.FindAll(dir.GetFiles(), f => f.EndsWith(".json"));
    }

    public static string FileNameFor(string name)
    {
        var slug = new System.Text.StringBuilder();
        foreach (char c in name.ToLowerInvariant())
            slug.Append(char.IsLetterOrDigit(c) ? c : '-');
        return $"{slug}.json";
    }
}
