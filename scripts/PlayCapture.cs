using Godot;

namespace HackerOrSoldier;

// Temporary helper: plays the bundled Operation Night Lift level, runs the
// agent right, jumps and fires, and saves a screenshot at each beat to
// user://play_<n>.png, then quits. Not part of the game.
public partial class PlayCapture : Node
{
    private const string LevelFile = "operation-night-lift.json";

    private Editor _editor;
    private int _frame;
    private int _shot;

    public override void _Ready()
    {
        using (var source = FileAccess.Open($"res://levels/{LevelFile}", FileAccess.ModeFlags.Read))
        {
            DirAccess.MakeDirRecursiveAbsolute(LevelStore.DirPath);
            using var copy = FileAccess.Open($"{LevelStore.DirPath}/{LevelFile}", FileAccess.ModeFlags.Write);
            copy.StoreString(source.GetAsText());
        }
        _editor = GD.Load<PackedScene>("res://scenes/editor.tscn").Instantiate<Editor>();
        AddChild(_editor);
        _editor.SetLevel(LevelStore.Load(LevelFile));
    }

    public override void _Process(double delta)
    {
        _frame++;
        switch (_frame)
        {
            case 5:
                _editor.StartPlay();
                break;
            case 30:
                Save();
                Input.ActionPress("move_right");
                break;
            case 44:
            case 51:
                Save();
                break;
            case 60:
                Input.ActionPress("jump");
                break;
            case 64:
                Input.ActionRelease("jump");
                Save();
                break;
            case 78:
                Save();
                break;
            case 110:
                Input.ActionRelease("move_right");
                break;
            case 140:
                Input.ActionPress("shoot");
                break;
            case 142:
                Save();
                Input.ActionRelease("shoot");
                break;
            case 147:
                Save();
                break;
            case 150:
                GetTree().Quit();
                break;
        }
    }

    private void Save() => GetViewport().GetTexture().GetImage().SavePng($"user://play_{_shot++}.png");
}
