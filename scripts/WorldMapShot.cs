using System.Threading.Tasks;
using Godot;

namespace HackerOrSoldier;

// Renders the world map screens and a play-mode shot of the admin password
// halo and locked exit to user://, for looking at. Needs a real window:
//   godot --path . res://scenes/world_map_shot.tscn
public partial class WorldMapShot : Node
{
    public override async void _Ready()
    {
        var map = new WorldMap();
        AddChild(map);
        await Frames(2);
        map.SetProgress(10);
        map.Fly(9, 10);
        map.Seek(0.5f);
        await Frames(3);
        Save("world_map_flight.png");
        map.QueueFree();

        foreach (var (name, index) in new[]
                 {
                     ("world_map_intro", 0), ("world_map_dialog_start", 1), ("world_map_dialog", 5),
                     ("world_map_dialog_antarctica", 14), ("world_map_outro", 15),
                 })
        {
            Profile.Current = new Profile { LevelIndex = index };
            var flow = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<GameFlow>();
            AddChild(flow);
            await Frames(3);
            flow.FinishTyping();
            await Frames(3);
            Save($"{name}.png");
            flow.QueueFree();
            await Frames(2);
        }

        var editor = GD.Load<PackedScene>("res://scenes/editor.tscn").Instantiate<Editor>();
        AddChild(editor);
        await Frames(2);
        var level = LevelData.NewDefault(19, 11);
        level.Objects.Add(new LevelObject { Type = CollectibleDefs.AdminPassword, X = 8, Y = 9 });
        editor.SetLevel(level);
        editor.StartPlay();
        await Frames(30);
        Save("play_password_halo.png");
        GetTree().Quit();
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Save(string name)
    {
        string path = ProjectSettings.GlobalizePath($"user://{name}");
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"saved {path}");
    }
}
