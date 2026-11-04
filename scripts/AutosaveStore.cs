using System;
using System.IO;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

public record Autosave(string FileName, LevelData Level, DateTime Time);

// Rolling snapshots of the level being edited, so a crash loses at most a
// minute of work. Kept apart from the saved levels.
public class AutosaveStore
{
    private const string HandledFile = "handled.txt";
    private readonly string _dir;
    private readonly int _keep;

    public AutosaveStore(string dir, int keep)
    {
        _dir = dir;
        _keep = keep;
    }

    private string PathOf(string fileName) => ProjectSettings.GlobalizePath($"{_dir}/{fileName}");

    private string[] FileNames() =>
        Directory.Exists(PathOf(""))
            ? Directory.GetFiles(PathOf(""), "autosave-*.json").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    public bool Write(LevelData level)
    {
        try
        {
            // Names sort by time. Bump the stamp when two writes land in the same millisecond.
            var stamp = DateTime.Now;
            string fileName;
            while (File.Exists(PathOf(fileName = $"autosave-{stamp:yyyyMMdd-HHmmss-fff}.json")))
                stamp = stamp.AddMilliseconds(1);
            LevelStore.WriteAtomic($"{_dir}/{fileName}", LevelStore.Serialize(level));
            foreach (var old in FileNames().SkipLast(_keep))
                File.Delete(PathOf(old));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushError($"Autosave failed: {e.Message}");
            return false;
        }
    }

    public Autosave Latest()
    {
        string fileName = FileNames().LastOrDefault();
        if (fileName == null)
            return null;
        try
        {
            var level = LevelStore.Parse(File.ReadAllText(PathOf(fileName)));
            return level == null ? null : new Autosave(fileName, level, File.GetLastWriteTime(PathOf(fileName)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            GD.PushError($"Reading autosave {fileName} failed: {e.Message}");
            return null;
        }
    }

    // The newest autosave, unless it was already offered or the level was
    // saved after it.
    public Autosave PendingRestore()
    {
        var latest = Latest();
        if (latest == null)
            return null;
        string handled = PathOf(HandledFile);
        if (File.Exists(handled) && File.ReadAllText(handled) == latest.FileName)
            return null;
        string saved = ProjectSettings.GlobalizePath($"{LevelStore.DirPath}/{LevelStore.FileNameFor(latest.Level.Name)}");
        if (File.Exists(saved) && File.GetLastWriteTime(saved) >= latest.Time)
            return null;
        return latest;
    }

    public void MarkHandled(string fileName)
    {
        try
        {
            LevelStore.WriteAtomic($"{_dir}/{HandledFile}", fileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushError($"Marking autosave handled failed: {e.Message}");
        }
    }
}
