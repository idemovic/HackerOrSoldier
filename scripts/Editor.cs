using System;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

public partial class Editor : Node2D
{
    private const int TileSize = TileSetBuilder.TileSize;
    private const float PanSpeed = 400f;

    // Hidden platforms live on their own layer so play mode can hide them
    // (infra goggles reveal them); in edit mode they show semi-transparent.
    private static readonly Color HiddenEditTint = new(1, 1, 1, 0.45f);

    private LevelData _level = LevelData.NewDefault();
    private PackedScene _agentScene;

    private TileMapLayer _grid;
    private TileMapLayer _hiddenGrid;
    private Node2D _markers;
    private ColorRect _cursor;
    private Camera2D _camera;
    private PlaySession _session;

    private Label _status;
    private Button _playButton;
    private AcceptDialog _saveDialog;
    private LineEdit _nameEdit;
    private TextEdit _descEdit;
    private ConfirmationDialog _loadDialog;
    private ItemList _levelList;
    private Minimap _minimap;
    private HBoxContainer _colorRow;
    private OptionButton _colorPicker;

    private enum BrushKind { Tile, Object, Eraser }

    private BrushKind _brushKind = BrushKind.Tile;
    private int _brushTile = TileDefs.All[0].Id;
    private string _brushObject = "";
    private string _brushColor = ObjectDefs.KeyColors[0];

    public LevelData Level => _level;
    public bool IsPlaying => _session != null;
    public PlaySession Session => _session;
    public Agent PlayerAgent => _session?.Agent;

    public override void _Ready()
    {
        _agentScene = GD.Load<PackedScene>("res://scenes/agent.tscn");

        _grid = new TileMapLayer { TileSet = TileSetBuilder.Build() };
        AddChild(_grid);

        _hiddenGrid = new TileMapLayer { TileSet = _grid.TileSet, Modulate = HiddenEditTint };
        AddChild(_hiddenGrid);

        _markers = new Node2D();
        AddChild(_markers);

        _cursor = new ColorRect
        {
            Size = new Vector2(TileSize, TileSize),
            Color = new Color(1, 1, 1, 0.25f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_cursor);

        _camera = new Camera2D { Position = new Vector2(320, 180) };
        AddChild(_camera);
        _camera.MakeCurrent();

        BuildUi();
        RedrawLevel();
    }

    public override void _Process(double delta)
    {
        if (IsPlaying)
            return;
        var pan = Input.GetVector("move_left", "move_right", "ui_up", "ui_down");
        _camera.Position += pan * PanSpeed * (float)delta;

        var cell = MouseCell();
        _cursor.Visible = _level.InBounds(cell.X, cell.Y);
        _cursor.Position = new Vector2(cell.X * TileSize, cell.Y * TileSize);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsPlaying)
        {
            if (@event.IsActionPressed("pause") || @event.IsActionPressed("editor_play"))
            {
                StopPlay();
                _status.Text = "Stopped.";
            }
            return;
        }

        if (@event.IsActionPressed("editor_play"))
        {
            StartPlay();
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.Left)
                PaintCell(MouseCell());
            else if (button.ButtonIndex == MouseButton.Right)
                EraseCell(MouseCell());
        }
        else if (@event is InputEventMouseMotion motion)
        {
            if (motion.ButtonMask.HasFlag(MouseButtonMask.Left))
                PaintCell(MouseCell());
            else if (motion.ButtonMask.HasFlag(MouseButtonMask.Right))
                EraseCell(MouseCell());
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, _level.Width * TileSize, _level.Height * TileSize), new Color(1, 1, 1, 0.2f), false);
    }

    public void SetBrushTile(int tileId)
    {
        _brushKind = BrushKind.Tile;
        _brushTile = tileId;
        UpdateColorRow();
    }

    public void SetBrushObject(string type, string color = null)
    {
        _brushKind = BrushKind.Object;
        _brushObject = type;
        if (color != null)
        {
            _brushColor = color;
            int index = System.Array.IndexOf(ObjectDefs.KeyColors, color);
            if (_colorPicker != null && index >= 0)
                _colorPicker.Selected = index;
        }
        UpdateColorRow();
    }

    public void SetBrushEraser()
    {
        _brushKind = BrushKind.Eraser;
        UpdateColorRow();
    }

    private void UpdateColorRow()
    {
        if (_colorRow != null)
            _colorRow.Visible = _brushKind == BrushKind.Object && ObjectDefs.HasColorProperty(_brushObject);
    }

    public void PaintCell(Vector2I cell)
    {
        if (!_level.InBounds(cell.X, cell.Y))
            return;
        switch (_brushKind)
        {
            case BrushKind.Tile:
                _level.SetTile(cell.X, cell.Y, _brushTile);
                SetGridCell(cell, _brushTile);
                break;
            case BrushKind.Object:
                PlaceObject(cell, _brushObject);
                break;
            case BrushKind.Eraser:
                EraseCell(cell);
                break;
        }
    }

    public void EraseCell(Vector2I cell)
    {
        if (!_level.InBounds(cell.X, cell.Y))
            return;
        _level.SetTile(cell.X, cell.Y, TileDefs.Empty);
        _grid.EraseCell(cell);
        _hiddenGrid.EraseCell(cell);
        if (_level.Objects.RemoveAll(o => o.X == cell.X && o.Y == cell.Y) > 0)
            RebuildMarkers();
    }

    private void SetGridCell(Vector2I cell, int tileId)
    {
        var target = tileId == TileDefs.HiddenPlatform ? _hiddenGrid : _grid;
        var other = tileId == TileDefs.HiddenPlatform ? _grid : _hiddenGrid;
        other.EraseCell(cell);
        target.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.AtlasCoordsFor(tileId));
    }

    public bool StartPlay()
    {
        string error = _level.Validate();
        if (error != null)
        {
            _status.Text = error;
            return false;
        }

        _session = new PlaySession();
        _session.Setup(_level, _grid, _hiddenGrid, _agentScene);
        _session.Completed += () => Callable.From(() => EndPlay("Level complete!")).CallDeferred();
        _session.Failed += () => Callable.From(() => EndPlay("Out of lives.")).CallDeferred();
        AddChild(_session);

        _markers.Visible = false;
        _cursor.Visible = false;
        _minimap.Visible = false;
        _playButton.Text = "Stop (Esc)";
        _status.Text = "Playing - Esc to return to editor.";
        return true;
    }

    public void StopPlay()
    {
        if (_session == null)
            return;
        _session.QueueFree();
        _session = null;
        _hiddenGrid.Modulate = HiddenEditTint;
        _grid.Modulate = Colors.White;   // clears the alarm tint
        RedrawLevel();   // restores walls destroyed during play
        _markers.Visible = true;
        _minimap.Visible = true;
        _camera.MakeCurrent();
        _playButton.Text = "Play (P)";
    }

    private void EndPlay(string message)
    {
        StopPlay();
        _status.Text = message;
    }

    private Vector2I MouseCell() => _grid.LocalToMap(_grid.ToLocal(GetGlobalMousePosition()));

    private void PlaceObject(Vector2I cell, string type)
    {
        _level.Objects.RemoveAll(o => o.X == cell.X && o.Y == cell.Y);
        if (type == ObjectDefs.AgentStart)
            _level.Objects.RemoveAll(o => o.Type == ObjectDefs.AgentStart);
        var obj = new LevelObject { Type = type, X = cell.X, Y = cell.Y };
        if (ObjectDefs.HasColorProperty(type))
            obj.Properties["color"] = _brushColor;
        _level.Objects.Add(obj);
        RebuildMarkers();
    }

    private void RedrawLevel()
    {
        _grid.Clear();
        _hiddenGrid.Clear();
        for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                int id = _level.GetTile(x, y);
                if (id != TileDefs.Empty)
                    SetGridCell(new Vector2I(x, y), id);
            }
        RebuildMarkers();
        ApplyCameraLimits(_camera);
        _minimap.Setup(_level, _camera);
        QueueRedraw();
    }

    private void RebuildMarkers()
    {
        foreach (var child in _markers.GetChildren())
            child.QueueFree();
        foreach (var obj in _level.Objects)
        {
            var def = ObjectDefs.All.FirstOrDefault(d => d.Type == obj.Type);
            var enemyDef = def == null ? EnemyDefs.Get(obj.Type) : null;
            if (def == null && enemyDef == null)
                continue;
            var color = def?.Color ?? enemyDef.Color;
            if (ObjectDefs.HasColorProperty(obj.Type) && obj.Properties.TryGetValue("color", out var name))
                color = ObjectDefs.KeyColor(name);
            color.A = 0.85f;
            int inset = ObjectDefs.IsPickup(obj.Type) ? 8 : 0;   // pickups draw smaller than doors
            _markers.AddChild(new ColorRect
            {
                Position = new Vector2(obj.X * TileSize + inset, obj.Y * TileSize + inset),
                Size = new Vector2(TileSize - 2 * inset, TileSize - 2 * inset),
                Color = color,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
    }

    private void ApplyCameraLimits(Camera2D camera)
    {
        camera.LimitLeft = 0;
        camera.LimitTop = 0;
        camera.LimitRight = Mathf.Max(_level.Width * TileSize, 640);
        camera.LimitBottom = Mathf.Max(_level.Height * TileSize, 360);
    }

    private void BuildUi()
    {
        var ui = new CanvasLayer();
        AddChild(ui);

        var theme = new Theme { DefaultFontSize = 10 };

        var bar = new HBoxContainer { Theme = theme };
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        ui.AddChild(bar);

        var newButton = new Button { Text = "New" };
        newButton.Pressed += OnNewPressed;
        bar.AddChild(newButton);

        var saveButton = new Button { Text = "Save" };
        saveButton.Pressed += () =>
        {
            _nameEdit.Text = _level.Name;
            _descEdit.Text = _level.Description;
            _saveDialog.PopupCentered();
        };
        bar.AddChild(saveButton);

        var loadButton = new Button { Text = "Load" };
        loadButton.Pressed += OpenLoadDialog;
        bar.AddChild(loadButton);

        _playButton = new Button { Text = "Play (P)" };
        _playButton.Pressed += () =>
        {
            if (IsPlaying)
                StopPlay();
            else
                StartPlay();
        };
        bar.AddChild(_playButton);

        _status = new Label
        {
            Text = "Left click: paint, right click: erase, arrows: pan, P: play.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
        };
        bar.AddChild(_status);

        var palette = new PanelContainer { Theme = theme, Position = new Vector2(0, 32) };
        ui.AddChild(palette);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 312),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        palette.AddChild(scroll);
        var paletteBox = new VBoxContainer();
        scroll.AddChild(paletteBox);

        var group = new ButtonGroup();
        paletteBox.AddChild(new Label { Text = "Tiles" });
        foreach (var def in TileDefs.All)
        {
            int id = def.Id;
            paletteBox.AddChild(MakeBrushButton(def.Name, def.Color, group, () => SetBrushTile(id), pressed: id == _brushTile));
        }
        paletteBox.AddChild(new Label { Text = "Objects" });
        _colorRow = new HBoxContainer { Visible = false };
        _colorRow.AddChild(new Label { Text = "Color" });
        _colorPicker = new OptionButton();
        foreach (var name in ObjectDefs.KeyColors)
            _colorPicker.AddItem(name);
        _colorPicker.Selected = 0;
        _colorPicker.ItemSelected += index => _brushColor = ObjectDefs.KeyColors[(int)index];
        _colorRow.AddChild(_colorPicker);
        paletteBox.AddChild(_colorRow);
        foreach (var def in ObjectDefs.All)
        {
            string type = def.Type;
            paletteBox.AddChild(MakeBrushButton(def.Name, def.Color, group, () => SetBrushObject(type)));
        }
        paletteBox.AddChild(new Label { Text = "Enemies" });
        foreach (var def in EnemyDefs.All)
        {
            string type = def.Type;
            paletteBox.AddChild(MakeBrushButton(def.Name, def.Color, group, () => SetBrushObject(type)));
        }
        paletteBox.AddChild(MakeBrushButton("Eraser", new Color(0.1f, 0.1f, 0.1f), group, SetBrushEraser));

        _minimap = new Minimap { MouseFilter = Control.MouseFilterEnum.Ignore };
        ui.AddChild(_minimap);

        _saveDialog = new AcceptDialog { Title = "Save level", OkButtonText = "Save", Theme = theme };
        var form = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        form.AddChild(new Label { Text = "Name" });
        _nameEdit = new LineEdit();
        form.AddChild(_nameEdit);
        form.AddChild(new Label { Text = "Description (shown in the level briefing)" });
        _descEdit = new TextEdit { CustomMinimumSize = new Vector2(0, 80) };
        form.AddChild(_descEdit);
        _saveDialog.AddChild(form);
        _saveDialog.Confirmed += OnSaveConfirmed;
        ui.AddChild(_saveDialog);

        _loadDialog = new ConfirmationDialog { Title = "Load level", OkButtonText = "Load", Theme = theme };
        _levelList = new ItemList { CustomMinimumSize = new Vector2(300, 150) };
        _loadDialog.AddChild(_levelList);
        _loadDialog.Confirmed += OnLoadConfirmed;
        ui.AddChild(_loadDialog);
    }

    private static Button MakeBrushButton(string text, Color color, ButtonGroup group, Action onSelect, bool pressed = false)
    {
        var swatch = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
        swatch.Fill(color);
        var button = new Button
        {
            Text = text,
            Icon = ImageTexture.CreateFromImage(swatch),
            ToggleMode = true,
            ButtonGroup = group,
            ButtonPressed = pressed,
            Alignment = HorizontalAlignment.Left,
        };
        button.Pressed += onSelect;
        return button;
    }

    private void OnNewPressed()
    {
        if (IsPlaying)
            StopPlay();
        _level = LevelData.NewDefault();
        RedrawLevel();
        _status.Text = "New level.";
    }

    private void OnSaveConfirmed()
    {
        string name = _nameEdit.Text.Trim();
        if (name.Length == 0)
        {
            _status.Text = "Level needs a name.";
            return;
        }
        _level.Name = name;
        _level.Description = _descEdit.Text.Trim();
        LevelStore.Save(_level);
        _status.Text = $"Saved {LevelStore.DirPath}/{LevelStore.FileNameFor(name)}";
    }

    private void OpenLoadDialog()
    {
        _levelList.Clear();
        foreach (var file in LevelStore.ListFiles())
            _levelList.AddItem(file);
        _loadDialog.PopupCentered();
    }

    private void OnLoadConfirmed()
    {
        var selected = _levelList.GetSelectedItems();
        if (selected.Length == 0)
            return;
        var loaded = LevelStore.Load(_levelList.GetItemText(selected[0]));
        if (loaded == null)
        {
            _status.Text = "Failed to load level.";
            return;
        }
        if (IsPlaying)
            StopPlay();
        _level = loaded;
        RedrawLevel();
        _status.Text = $"Loaded '{_level.Name}'";
    }
}
