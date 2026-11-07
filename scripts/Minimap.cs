using Godot;

namespace HackerOrSoldier;

// Top-right overview of the whole level while editing, with the current
// camera view outlined. Redraws every frame from the live LevelData.
// Clicking or dragging on it moves the camera there.
public partial class Minimap : Control
{
    public float Top { get; set; } = 36f;

    public event System.Action<Vector2> Jumped;

    private bool _held;

    private const float MaxWidth = 160f;
    private const float MaxHeight = 60f;
    private const float Margin = 4f;

    private LevelData _level;
    private Camera2D _camera;
    private float _tileScale = 4f;

    public void Setup(LevelData level, Camera2D camera)
    {
        _level = level;
        _camera = camera;
        _tileScale = Mathf.Min(MaxWidth / level.Width, MaxHeight / level.Height);
        Size = new Vector2(level.Width, level.Height) * _tileScale;
        Position = new Vector2(GetViewportRect().Size.X - Size.X - Margin, Top);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
            _held = button.Pressed;
        else if (@event is not InputEventMouseMotion)
            return;
        if (!_held || _level == null)
            return;
        var local = ((InputEventMouse)@event).Position;
        Jumped?.Invoke(local / _tileScale * TileSetBuilder.TileSize);
        AcceptEvent();
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (_level == null)
            return;

        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.55f));

        for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                int id = _level.GetTile(x, y);
                if (id == TileDefs.Empty)
                    id = _level.GetBackground(x, y);
                if (id == TileDefs.Empty)
                    continue;
                foreach (var def in TileDefs.All)
                    if (def.Id == id)
                    {
                        DrawRect(new Rect2(x * _tileScale, y * _tileScale, _tileScale, _tileScale), def.Color);
                        break;
                    }
            }

        foreach (var obj in _level.Objects)
            foreach (var def in ObjectDefs.All)
                if (def.Type == obj.Type)
                {
                    DrawRect(new Rect2(obj.X * _tileScale, obj.Y * _tileScale, _tileScale, _tileScale), def.Color);
                    break;
                }

        if (_camera != null)
        {
            float worldToMap = _tileScale / TileSetBuilder.TileSize;
            var viewSize = GetViewportRect().Size / _camera.Zoom * worldToMap;
            var topLeft = _camera.GetScreenCenterPosition() * worldToMap - viewSize / 2f;
            DrawRect(new Rect2(topLeft, viewSize), new Color(1, 1, 1, 0.8f), false);
        }

        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1, 1, 1, 0.4f), false);
    }
}
