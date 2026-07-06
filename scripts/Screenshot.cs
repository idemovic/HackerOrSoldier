using Godot;

namespace HackerOrSoldier;

// Temporary helper: renders the editor for a few frames, saves a screenshot
// to user://editor_ui.png and quits. Not part of the game.
public partial class Screenshot : Node
{
    private int _frames;

    public override void _Ready()
    {
        var editor = GD.Load<PackedScene>("res://scenes/editor.tscn").Instantiate<Editor>();
        AddChild(editor);

        // Showcase content within the visible 20x11 cells.
        editor.SetBrushObject(ObjectDefs.DoorKey, "red");
        editor.PaintCell(new Vector2I(4, 10));
        editor.SetBrushObject(ObjectDefs.HealthKit);
        editor.PaintCell(new Vector2I(5, 10));
        editor.SetBrushObject(ObjectDefs.Door, "red");
        editor.PaintCell(new Vector2I(6, 10));
        editor.SetBrushTile(TileDefs.Spikes);
        editor.PaintCell(new Vector2I(8, 10));
        editor.SetBrushObject(ObjectDefs.Barrel);
        editor.PaintCell(new Vector2I(9, 10));
        editor.SetBrushTile(TileDefs.Water);
        editor.PaintCell(new Vector2I(10, 9));
        editor.PaintCell(new Vector2I(10, 10));
        editor.PaintCell(new Vector2I(11, 9));
        editor.PaintCell(new Vector2I(11, 10));
        editor.SetBrushObject(EnemyDefs.Drone);
        editor.PaintCell(new Vector2I(12, 6));
        editor.SetBrushTile(TileDefs.Lava);
        editor.PaintCell(new Vector2I(13, 10));
        editor.PaintCell(new Vector2I(14, 10));
        editor.SetBrushObject(ObjectDefs.Computer, number: "2");
        editor.PaintCell(new Vector2I(15, 10));
        editor.SetBrushObject(EnemyDefs.Soldier);
        editor.PaintCell(new Vector2I(16, 10));
        editor.SetBrushObject(ObjectDefs.Camera);
        editor.PaintCell(new Vector2I(17, 7));
        editor.SetBrushObject(EnemyDefs.Robot);
        editor.PaintCell(new Vector2I(18, 10));
        editor.SetBrushTile(TileDefs.Wall);
        editor.PaintCell(new Vector2I(19, 9));
        editor.PaintCell(new Vector2I(19, 10));
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (_frames == 10)
            GetViewport().GetTexture().GetImage().SavePng("user://editor_ui.png");
        if (_frames == 14)
            GetTree().Quit();
    }
}
