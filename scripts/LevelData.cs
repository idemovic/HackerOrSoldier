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

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    // Cells with no tile are the outdoors; the sky ("night" or "day")
    // fills the level behind everything.
    public string Sky { get; set; } = "night";
    public int Width { get; set; } = DefaultWidth;
    public int Height { get; set; } = DefaultHeight;
    public int[] Tiles { get; set; } = new int[DefaultWidth * DefaultHeight];
    public List<LevelObject> Objects { get; set; } = new();

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public int GetTile(int x, int y) => InBounds(x, y) ? Tiles[y * Width + x] : TileDefs.Empty;

    public void SetTile(int x, int y, int tileId)
    {
        if (InBounds(x, y))
            Tiles[y * Width + x] = tileId;
    }

    public LevelData Clone() => new()
    {
        Name = Name,
        Description = Description,
        Sky = Sky,
        Width = Width,
        Height = Height,
        Tiles = (int[])Tiles.Clone(),
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
        var level = new LevelData { Width = width, Height = height, Tiles = new int[width * height] };
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
    public const string DirPath = "user://levels";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Save(LevelData level)
    {
        DirAccess.MakeDirRecursiveAbsolute(DirPath);
        using var file = FileAccess.Open($"{DirPath}/{FileNameFor(level.Name)}", FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(level, JsonOptions));
    }

    public static LevelData Load(string fileName)
    {
        using var file = FileAccess.Open($"{DirPath}/{fileName}", FileAccess.ModeFlags.Read);
        if (file == null)
            return null;
        var level = JsonSerializer.Deserialize<LevelData>(file.GetAsText(), JsonOptions);
        if (level?.Tiles == null || level.Tiles.Length != level.Width * level.Height)
            return null;
        level.Objects ??= new List<LevelObject>();
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
