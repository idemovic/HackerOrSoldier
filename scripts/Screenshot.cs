using Godot;

namespace HackerOrSoldier;

// Temporary helper: renders the editor for a few frames, saves a screenshot
// to user://editor_ui.png and quits. Not part of the game.
public partial class Screenshot : Node
{
    private int _frames;

    public override void _Ready() =>
        AddChild(GD.Load<PackedScene>("res://scenes/editor.tscn").Instantiate());

    public override void _Process(double delta)
    {
        _frames++;
        if (_frames < 10)
            return;
        GetViewport().GetTexture().GetImage().SavePng("user://editor_ui.png");
        GetTree().Quit();
    }
}
