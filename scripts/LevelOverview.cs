using Godot;

namespace HackerOrSoldier;

// Renders a whole level as the editor draws it to user://<level>_overview.png
// by stepping the editor camera over the level one screen at a time and
// stitching the frames, then quits. Not part of the game. The level file
// defaults to the bundled test level; pass another res://levels file name
// after "--": Godot --path . res://scenes/level_overview.tscn -- my-level.json
public partial class LevelOverview : Node
{
    private const int SettleFrames = 3;

    private Editor _editor;
    private Camera2D _camera;
    private Image _sheet;
    private string _levelFile = "operation-night-lift.json";
    private Vector2I _screen;
    private int _columns;
    private int _rows;
    private int _shot;
    private int _wait;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Length > 0)
            _levelFile = args[0];
        var level = LevelStore.Load(_levelFile);

        _editor = GD.Load<PackedScene>("res://scenes/editor.tscn").Instantiate<Editor>();
        AddChild(_editor);
        _editor.SetLevel(level);
        foreach (var child in _editor.GetChildren())
            if (child is CanvasLayer ui)
                ui.Visible = false;

        _camera = new Camera2D();
        AddChild(_camera);
        _camera.MakeCurrent();

        int tile = TileSetBuilder.TileSize;
        _screen = (Vector2I)GetViewport().GetVisibleRect().Size;
        var size = new Vector2I(level.Width * tile, level.Height * tile);
        _columns = Mathf.CeilToInt((float)size.X / _screen.X);
        _rows = Mathf.CeilToInt((float)size.Y / _screen.Y);
        _sheet = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
        Aim();
    }

    public override void _Process(double delta)
    {
        if (++_wait < SettleFrames)
            return;
        var frame = GetViewport().GetTexture().GetImage();
        frame.Convert(Image.Format.Rgba8);
        frame.Resize(_screen.X, _screen.Y, Image.Interpolation.Nearest);   // the window may be scaled up
        var origin = Origin(_shot);
        _sheet.BlitRect(frame, new Rect2I(Vector2I.Zero, _screen), origin);
        if (++_shot < _columns * _rows)
        {
            Aim();
            return;
        }
        string path = $"user://{_levelFile.Replace(".json", "")}_overview.png";
        _sheet.SavePng(path);
        GD.Print($"saved {ProjectSettings.GlobalizePath(path)}");
        GetTree().Quit();
    }

    // The last row and column are pulled back inside the level so every
    // frame is a full screen of level, overlapping its neighbour instead.
    private Vector2I Origin(int shot)
    {
        var limit = _sheet.GetSize() - _screen;
        return new Vector2I(
            Mathf.Min(shot % _columns * _screen.X, limit.X),
            Mathf.Min(shot / _columns * _screen.Y, limit.Y));
    }

    private void Aim()
    {
        _camera.Position = Origin(_shot) + _screen / 2;
        _camera.MakeCurrent();
        _wait = 0;
    }
}
