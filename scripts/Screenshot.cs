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

        // Showcase content in the bottom-left 20x11 cells, where the editor
        // camera starts.
        editor.SetBrushObject(ObjectDefs.DoorKey, "red");
        editor.PaintCell(new Vector2I(4, 38));
        editor.SetBrushObject(ObjectDefs.HealthKit);
        editor.PaintCell(new Vector2I(5, 38));
        editor.SetBrushObject(ObjectDefs.Door, "red");
        editor.PaintCell(new Vector2I(6, 38));
        editor.SetBrushTile(TileDefs.Spikes);
        editor.PaintCell(new Vector2I(8, 38));
        editor.SetBrushObject(ObjectDefs.Barrel);
        editor.PaintCell(new Vector2I(9, 38));
        editor.SetBrushTile(TileDefs.Water);
        editor.PaintCell(new Vector2I(10, 37));
        editor.PaintCell(new Vector2I(10, 38));
        editor.PaintCell(new Vector2I(11, 37));
        editor.PaintCell(new Vector2I(11, 38));
        editor.SetBrushObject(EnemyDefs.Drone);
        editor.PaintCell(new Vector2I(12, 34));
        editor.SetBrushTile(TileDefs.Lava);
        editor.PaintCell(new Vector2I(13, 38));
        editor.PaintCell(new Vector2I(14, 38));
        editor.SetBrushObject(ObjectDefs.Computer, number: "2");
        editor.PaintCell(new Vector2I(15, 38));
        editor.SetBrushObject(EnemyDefs.Soldier);
        editor.PaintCell(new Vector2I(16, 38));
        editor.SetBrushObject(ObjectDefs.Camera);
        editor.PaintCell(new Vector2I(17, 35));
        editor.SetBrushObject(EnemyDefs.Robot);
        editor.PaintCell(new Vector2I(18, 38));
        editor.SetBrushTile(TileDefs.Wall);
        editor.PaintCell(new Vector2I(19, 37));
        editor.PaintCell(new Vector2I(19, 38));
        editor.SetBrushTile(TileDefs.BackgroundMesh);
        for (int x = 1; x <= 3; x++)
            for (int y = 36; y <= 38; y++)
                editor.PaintCell(new Vector2I(x, y));
        editor.SetBrushTile(TileDefs.BackgroundRock);
        for (int x = 5; x <= 8; x++)
            editor.PaintCell(new Vector2I(x, 31));
        editor.SetBrushTile(TileDefs.BackgroundPanel);
        for (int x = 10; x <= 13; x++)
            editor.PaintCell(new Vector2I(x, 31));
        editor.SetBrushTile(TileDefs.StoneBlocks);
        editor.PaintCell(new Vector2I(6, 35));
        editor.PaintCell(new Vector2I(7, 35));
        editor.SetBrushTile(TileDefs.RedBrick);
        editor.PaintCell(new Vector2I(9, 35));
        editor.PaintCell(new Vector2I(10, 35));
        editor.SetBrushTile(TileDefs.SteelPlate);
        editor.PaintCell(new Vector2I(12, 35));
        editor.PaintCell(new Vector2I(13, 35));
        editor.SetBrushTile(TileDefs.Girder);
        editor.PaintCell(new Vector2I(15, 35));
        editor.PaintCell(new Vector2I(16, 35));
        editor.SetBrushTile(TileDefs.Concrete);
        editor.PaintCell(new Vector2I(1, 33));
        editor.PaintCell(new Vector2I(2, 33));
        editor.SetBrushTile(TileDefs.Sandbags);
        editor.PaintCell(new Vector2I(4, 33));
        editor.PaintCell(new Vector2I(5, 33));
        editor.SetBrushTile(TileDefs.Container);
        editor.PaintCell(new Vector2I(7, 33));
        editor.PaintCell(new Vector2I(8, 33));
        editor.SetBrushTile(TileDefs.PipeWall);
        editor.PaintCell(new Vector2I(10, 33));
        editor.PaintCell(new Vector2I(11, 33));
        editor.SetBrushTile(TileDefs.BackgroundBrick);
        editor.PaintCell(new Vector2I(15, 31));
        editor.PaintCell(new Vector2I(16, 31));
        editor.SetBrushTile(TileDefs.BackgroundHangar);
        editor.PaintCell(new Vector2I(18, 31));
        editor.PaintCell(new Vector2I(19, 31));
        editor.SetBrushTile(TileDefs.BackgroundCircuit);
        editor.PaintCell(new Vector2I(15, 33));
        editor.PaintCell(new Vector2I(16, 33));
        editor.SetBrushTile(TileDefs.BackgroundSky);
        editor.PaintCell(new Vector2I(18, 33));
        editor.PaintCell(new Vector2I(19, 33));
        editor.ToggleSky();   // show the day sky behind the showcase
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
