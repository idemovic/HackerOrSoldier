using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

public partial class Editor : Node2D
{
    private const int TileSize = TileSetBuilder.TileSize;
    private const float PanSpeed = 400f;
    private const int MaxUndo = 100;
    private static readonly float[] ZoomLevels = { 1f, 0.5f, 0.25f };

    // Hidden platforms live on their own layer so play mode can hide them
    // (infra goggles reveal them); in edit mode they show semi-transparent.
    private static readonly Color HiddenEditTint = new(1, 1, 1, 0.45f);

    private LevelData _level = LevelData.NewDefault();
    private readonly AutosaveStore _autosaves = new("user://autosave", keep: 10);
    private string _lastWrittenJson;
    private bool _autosaveEnabled;
    private Autosave _pendingRestore;
    private PackedScene _agentScene;

    // Undo and redo hold whole-level snapshots taken before each change. A
    // mouse stroke (press to release) records once, so Ctrl+Z reverts the
    // whole drag.
    private readonly List<LevelData> _undo = new();
    private readonly List<LevelData> _redo = new();
    private bool _strokeOpen;

    private SkyBackdrop _sky;
    private LevelGrids _grids;
    private Node2D _markers;
    private Node2D _overlay;
    private Sprite2D _ghost;
    private Camera2D _camera;
    private PlaySession _session;

    public enum Tool { Brush, Rect, Eraser, Picker }

    // What a held mouse button is doing, decided on press.
    private enum Drag { None, Paint, Erase, Rect, Pan }

    private Tool _tool = Tool.Brush;
    private PaletteEntry _brush;
    private int _category = -1;
    private string _brushColor = ObjectDefs.KeyColors[0];
    private string _brushNumber = ObjectDefs.Numbers[0];
    private Drag _drag;
    private Vector2I _dragStart;
    private bool _rectErase;
    private bool _showGrid = true;
    private bool _invincible;
    private bool _revealHidden;
    private int _zoomIndex;

    private EditorCommand[] _commands;
    private Theme _theme;
    private Control _editUi;
    private Control _bars;
    private Label _status;
    private Label _categoryLabel;
    private HBoxContainer _slotRow;
    private readonly List<Button> _slots = new();
    private readonly List<Button> _tabs = new();
    private readonly Dictionary<Tool, Button> _toolButtons = new();
    private Button _gridButton;
    private Button _skyButton;
    private Button _godButton;
    private Button _revealButton;
    private TextureRect _brushIcon;
    private Label _brushLabel;
    private HBoxContainer _colorChips;
    private HBoxContainer _numberChips;
    private Label _propertyHint;
    private Label _cellLabel;
    private Label _zoomLabel;
    private Control _menuPanel;
    private Control _playBar;
    private Button _playGodButton;
    private Button _playRevealButton;
    private AcceptDialog _helpDialog;
    private AcceptDialog _saveDialog;
    private LineEdit _nameEdit;
    private TextEdit _descEdit;
    private ConfirmationDialog _loadDialog;
    private ConfirmationDialog _restoreDialog;
    private ItemList _levelList;
    private Minimap _minimap;

    public LevelData Level => _level;
    public bool IsPlaying => _session != null;
    public PlaySession Session => _session;
    public Agent PlayerAgent => _session?.Agent;
    public LevelGrids Grids => _grids;   // for tests
    public Tool CurrentTool => _tool;   // for tests
    public PaletteEntry Brush => _brush;   // for tests
    public string BrushColor => _brushColor;   // for tests
    public bool Invincible => _invincible;
    public bool RevealHidden => _revealHidden;
    // For tests: the center of what is on screen, after the camera limits.
    public Vector2 ViewCenter
    {
        get
        {
            _camera.ForceUpdateScroll();
            return _camera.GetScreenCenterPosition();
        }
    }

    private bool DialogOpen => _saveDialog.Visible || _loadDialog.Visible || _restoreDialog.Visible || _helpDialog.Visible;

    private EditorCommand[] BuildCommands() => new EditorCommand[]
    {
        new("New level", "File", OnNewPressed, new Chord(Key.N, Ctrl: true)),
        new("Save", "File", OpenSaveDialog, new Chord(Key.S, Ctrl: true)),
        new("Load", "File", OpenLoadDialog, new Chord(Key.L, Ctrl: true)),
        new("Main menu", "File", () => GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn"), new Chord(Key.M, Ctrl: true)),
        new("Help", "File", () => _helpDialog.PopupCentered(), new Chord(Key.F1), new Chord(Key.H, Ctrl: true)),
        new("Undo", "Edit", Undo, new Chord(Key.Z, Ctrl: true)),
        new("Redo", "Edit", Redo, new Chord(Key.Y, Ctrl: true), new Chord(Key.Z, Ctrl: true, Shift: true)),
        new("Brush", "Tools", () => SetTool(Tool.Brush), new Chord(Key.B)),
        new("Rectangle fill", "Tools", () => SetTool(Tool.Rect), new Chord(Key.R)),
        new("Eraser", "Tools", () => SetTool(Tool.Eraser), new Chord(Key.E)),
        new("Picker", "Tools", () => SetTool(Tool.Picker), new Chord(Key.Q)),
        new("Next color/number", "Tools", () => CycleProperty(1), new Chord(Key.Bracketright)),
        new("Prev color/number", "Tools", () => CycleProperty(-1), new Chord(Key.Bracketleft)),
        new("Next category", "Palette", () => ShowCategory(Wrap(_category + 1, EditorPalette.Categories.Length)), new Chord(Key.Tab)),
        new("Prev category", "Palette", () => ShowCategory(Wrap(_category - 1, EditorPalette.Categories.Length)), new Chord(Key.Tab, Shift: true)),
        new("Play / stop", "View", TogglePlay, new Chord(Key.P), new Chord(Key.P, Ctrl: true)),
        new("Invincible playtest", "View", ToggleInvincible, new Chord(Key.I), new Chord(Key.I, Ctrl: true)),
        new("Solid hidden platforms", "View", ToggleRevealHidden, new Chord(Key.H)),
        new("Zoom in", "View", () => ZoomBy(-1, null), new Chord(Key.Equal)),
        new("Zoom out", "View", () => ZoomBy(1, null), new Chord(Key.Minus)),
        new("Grid", "View", ToggleGrid, new Chord(Key.G)),
        new("Day / night sky", "View", ToggleSky, new Chord(Key.K, Ctrl: true), new Chord(Key.K)),
    };

    // The playtest switches that also work while playing.
    private static readonly string[] PlaytestCommands = { "Invincible playtest", "Solid hidden platforms" };

    private EditorCommand Command(string name) => Array.Find(_commands, c => c.Name == name);

    public override void _Ready()
    {
        _commands = BuildCommands();
        _agentScene = GD.Load<PackedScene>("res://scenes/agent.tscn");

        _sky = new SkyBackdrop();
        AddChild(_sky);   // behind the grids: empty cells show the sky

        _grids = new LevelGrids(this);
        _grids.Hidden.Modulate = HiddenEditTint;

        _markers = new Node2D();
        AddChild(_markers);

        _ghost = new Sprite2D { Modulate = new Color(1, 1, 1, 0.55f), Visible = false };
        AddChild(_ghost);

        _overlay = new Node2D();
        _overlay.Draw += DrawOverlay;
        AddChild(_overlay);

        // Start looking at the bottom-left corner, where the default floor
        // and agent start are.
        _camera = new Camera2D { Position = new Vector2(320, _level.Height * TileSize - 180) };
        AddChild(_camera);
        _camera.MakeCurrent();

        BuildUi();
        SetBrushTile(TileDefs.Wall);
        RedrawLevel();

        _lastWrittenJson = LevelStore.Serialize(_level);
        Callable.From(StartAutosave).CallDeferred();   // CurrentScene is not set yet in _Ready
    }

    private void StartAutosave()
    {
        // Tests and capture tools run the editor as a child scene and must not touch the player's autosaves.
        _autosaveEnabled = GetTree().CurrentScene == this;
        if (!_autosaveEnabled)
            return;
        var timer = new Timer { WaitTime = 60, Autostart = true };
        timer.Timeout += AutosaveIfChanged;
        AddChild(timer);

        _pendingRestore = _autosaves.PendingRestore();
        if (_pendingRestore != null)
        {
            _restoreDialog.DialogText = $"Restore unsaved work on '{_pendingRestore.Level.Name}' from {_pendingRestore.Time:HH:mm}?";
            _restoreDialog.PopupCentered();
        }
    }

    public override void _ExitTree() => AutosaveIfChanged();

    private void AutosaveIfChanged()
    {
        if (!_autosaveEnabled || IsPlaying)
            return;
        string json = LevelStore.Serialize(_level);
        if (json == _lastWrittenJson || !_autosaves.Write(_level))
            return;
        _lastWrittenJson = json;
        SetStatus($"Autosaved {DateTime.Now:HH:mm}");
    }

    private void OnRestoreConfirmed()
    {
        _autosaves.MarkHandled(_pendingRestore.FileName);
        if (IsPlaying)
            StopPlay();
        RecordUndo();
        SetLevel(_pendingRestore.Level);
        _lastWrittenJson = LevelStore.Serialize(_level);
        SetStatus("Restored autosave");
    }

    public override void _Process(double delta)
    {
        if (IsPlaying)
            return;
        if (!DialogOpen && !Input.IsKeyPressed(Key.Ctrl) && !Input.IsKeyPressed(Key.Alt))
        {
            var pan = Input.GetVector("move_left", "move_right", "ui_up", "ui_down");
            if (Input.IsPhysicalKeyPressed(Key.W))
                pan.Y -= 1;
            if (Input.IsPhysicalKeyPressed(Key.S))
                pan.Y += 1;
            float speed = PanSpeed / _camera.Zoom.X * (Input.IsKeyPressed(Key.Shift) ? 2f : 1f);
            _camera.Position += pan.LimitLength() * speed * (float)delta;
            ClampCamera();
        }

        var cell = MouseCell();
        bool overUi = GetViewport().GuiGetHoveredControl() != null;
        bool inLevel = _level.InBounds(cell.X, cell.Y) && !overUi;
        _ghost.Visible = inLevel && _drag != Drag.Rect && _tool is Tool.Brush or Tool.Rect && !AltHeld();
        _ghost.Position = CellCenter(cell);
        _cellLabel.Text = inLevel ? $"{cell.X},{cell.Y}" : "";
        _overlay.QueueRedraw();
    }

    private static bool AltHeld() => Input.IsKeyPressed(Key.Alt);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsPlaying)
        {
            var playCommand = @event is InputEventKey { Pressed: true, Echo: false } playKey
                ? Array.Find(PlaytestCommands, name => Command(name).Chords.Any(c => c.Matches(playKey)))
                : null;
            if (playCommand != null)
            {
                Command(playCommand).Run();
                GetViewport()?.SetInputAsHandled();
            }
            else if (@event.IsActionPressed("pause") || @event.IsActionPressed("editor_play"))
            {
                StopPlay();
                SetStatus("Back to editing.");
            }
            return;
        }

        if (DialogOpen)
            return;

        // The open file menu closes on Esc or a click outside it.
        if (_menuPanel.Visible && (@event.IsActionPressed("pause") || @event is InputEventMouseButton { Pressed: true }))
        {
            _menuPanel.Hide();
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (HandleKey(key))
                GetViewport()?.SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton button)
        {
            if (button.Pressed)
                OnMousePressed(button);
            else if (button.ButtonIndex is MouseButton.Left or MouseButton.Right or MouseButton.Middle)
                OnMouseReleased();
        }
        else if (@event is InputEventMouseMotion motion)
            OnMouseMoved(motion);
    }

    private bool HandleKey(InputEventKey key)
    {
        int category = EditorPalette.CategoryFor(key);
        if (category >= 0)
        {
            if (category < EditorPalette.Categories.Length)
                ShowCategory(category);
            return true;
        }
        int slot = EditorPalette.SlotFor(key);
        if (slot >= 0)
        {
            SelectSlot(slot);
            return true;
        }
        var command = Array.Find(_commands, c => c.Chords.Any(chord => chord.Matches(key)));
        command?.Run();
        return command != null;
    }

    private void OnMousePressed(InputEventMouseButton button)
    {
        _strokeOpen = false;
        int wheel = button.ButtonIndex switch
        {
            MouseButton.WheelDown => 1,
            MouseButton.WheelUp => -1,
            _ => 0,
        };
        if (wheel != 0)
        {
            if (button.CtrlPressed)
                ZoomBy(wheel, button.Position);
            else if (button.ShiftPressed)
                ShowCategory(Wrap(_category + wheel, EditorPalette.Categories.Length));
            else
                CycleSlot(wheel);
            return;
        }

        var cell = MouseCell();
        _dragStart = cell;
        switch (button.ButtonIndex)
        {
            case MouseButton.Middle:
                _drag = Drag.Pan;
                break;
            case MouseButton.Left when Input.IsKeyPressed(Key.Space):
                _drag = Drag.Pan;
                break;
            case MouseButton.Left when button.AltPressed || _tool == Tool.Picker:
                Pick(cell);
                break;
            case MouseButton.Left when button.ShiftPressed || _tool == Tool.Rect:
                _drag = Drag.Rect;
                _rectErase = _tool == Tool.Eraser;
                break;
            case MouseButton.Left when _tool == Tool.Eraser:
                _drag = Drag.Erase;
                EraseCell(cell);
                break;
            case MouseButton.Left:
                _drag = Drag.Paint;
                PaintCell(cell);
                break;
            case MouseButton.Right when button.ShiftPressed || _tool == Tool.Rect:
                _drag = Drag.Rect;
                _rectErase = true;
                break;
            case MouseButton.Right:
                _drag = Drag.Erase;
                EraseCell(cell);
                break;
        }
    }

    private void OnMouseReleased()
    {
        if (_drag == Drag.Rect)
            ApplyRect(_dragStart, MouseCell(), _rectErase);
        _drag = Drag.None;
        _strokeOpen = false;   // the next paint starts a new stroke
    }

    private void OnMouseMoved(InputEventMouseMotion motion)
    {
        switch (_drag)
        {
            case Drag.Pan:
                _camera.Position -= motion.Relative / _camera.Zoom;
                ClampCamera();
                break;
            case Drag.Paint:
                PaintCell(MouseCell());
                break;
            case Drag.Erase:
                EraseCell(MouseCell());
                break;
        }
    }

    private static Rect2I CellRect(Vector2I a, Vector2I b)
    {
        var min = new Vector2I(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
        var max = new Vector2I(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y));
        return new Rect2I(min, max - min + Vector2I.One);
    }

    public void ApplyRect(Vector2I from, Vector2I to, bool erase)
    {
        var rect = CellRect(from, to).Intersection(new Rect2I(0, 0, _level.Width, _level.Height));
        _strokeOpen = false;
        for (int y = rect.Position.Y; y < rect.End.Y; y++)
            for (int x = rect.Position.X; x < rect.End.X; x++)
                if (erase)
                    EraseCell(new Vector2I(x, y));
                else
                    PlaceBrush(new Vector2I(x, y));
        _strokeOpen = false;
    }

    private void DrawOverlay()
    {
        if (IsPlaying)
            return;
        var levelRect = new Rect2(0, 0, _level.Width * TileSize, _level.Height * TileSize);
        if (_showGrid)
        {
            var half = GetViewportRect().Size / _camera.Zoom / 2;
            var center = _camera.GetScreenCenterPosition();
            var view = new Rect2(center - half, half * 2).Intersection(levelRect);
            var line = new Color(1, 1, 1, 0.07f);
            for (float x = Mathf.Ceil(view.Position.X / TileSize) * TileSize; x < view.End.X; x += TileSize)
                _overlay.DrawLine(new Vector2(x, view.Position.Y), new Vector2(x, view.End.Y), line, -1);
            for (float y = Mathf.Ceil(view.Position.Y / TileSize) * TileSize; y < view.End.Y; y += TileSize)
                _overlay.DrawLine(new Vector2(view.Position.X, y), new Vector2(view.End.X, y), line, -1);
        }
        _overlay.DrawRect(levelRect, new Color(1, 1, 1, 0.25f), false);

        var cell = MouseCell();
        if (GetViewport().GuiGetHoveredControl() != null && _drag != Drag.Rect)
            return;
        if (_drag == Drag.Rect)
        {
            var rect = CellRect(_dragStart, cell);
            var area = new Rect2(rect.Position * TileSize, rect.Size * TileSize);
            var color = _rectErase ? EditorTheme.Danger : EditorTheme.Accent;
            _overlay.DrawRect(area, color with { A = 0.2f });
            _overlay.DrawRect(area, color, false, 1);
            return;
        }
        if (!_level.InBounds(cell.X, cell.Y))
            return;
        var cursor = (AltHeld() ? Tool.Picker : _tool) switch
        {
            Tool.Eraser => EditorTheme.Danger,
            Tool.Picker => EditorTheme.Gold,
            _ => EditorTheme.Accent,
        };
        _overlay.DrawRect(new Rect2(cell * TileSize, new Vector2(TileSize, TileSize)), cursor, false, 1);
    }

    public void SetTool(Tool tool)
    {
        _tool = tool;
        RefreshBrushUi();
    }

    public void SetBrushTile(int tileId) => SelectBrush(EntryFor(tileId, null));

    public void SetBrushObject(string type, string color = null, string number = null)
    {
        if (color != null)
            _brushColor = color;
        if (number != null)
            _brushNumber = number;
        SelectBrush(EntryFor(-1, type));
    }

    public void SetBrushEraser() => SetTool(Tool.Eraser);

    private static PaletteEntry EntryFor(int tileId, string objectType)
    {
        var (category, slot) = EditorPalette.Find(tileId, objectType);
        return category >= 0
            ? EditorPalette.Categories[category].Entries[slot]
            : new PaletteEntry(objectType ?? TileDefs.Get(tileId)?.Name ?? $"Tile {tileId}", tileId, objectType);
    }

    private void SelectBrush(PaletteEntry entry)
    {
        _brush = entry;
        if (_tool is Tool.Eraser or Tool.Picker)
            _tool = Tool.Brush;
        var (category, _) = EditorPalette.Find(entry.TileId, entry.ObjectType);
        if (category >= 0 && category != _category)
            ShowCategory(category);
        else
            RefreshBrushUi();
    }

    public void SelectSlot(int slot)
    {
        var entries = EditorPalette.Categories[_category].Entries;
        if (slot < entries.Length)
            SelectBrush(entries[slot]);
    }

    private void CycleSlot(int step)
    {
        var entries = EditorPalette.Categories[_category].Entries;
        int current = Array.IndexOf(entries, _brush);
        SelectBrush(entries[current < 0 ? 0 : Wrap(current + step, entries.Length)]);
    }

    private static int Wrap(int value, int count) => ((value % count) + count) % count;

    private void CycleProperty(int step)
    {
        if (_brush.ObjectType != null && ObjectDefs.HasColorProperty(_brush.ObjectType))
            _brushColor = ObjectDefs.KeyColors[Wrap(Array.IndexOf(ObjectDefs.KeyColors, _brushColor) + step, ObjectDefs.KeyColors.Length)];
        else if (_brush.ObjectType != null && ObjectDefs.HasNumberProperty(_brush.ObjectType))
            _brushNumber = ObjectDefs.Numbers[Wrap(Array.IndexOf(ObjectDefs.Numbers, _brushNumber) + step, ObjectDefs.Numbers.Length)];
        else
            SetStatus($"{_brush.Name} has no color or number.");
        RefreshBrushUi();
    }

    // Copies the object or tile under the cursor into the brush, properties
    // included. A one-shot Picker tool hands back to the brush afterwards.
    public void Pick(Vector2I cell)
    {
        if (!_level.InBounds(cell.X, cell.Y))
            return;
        var obj = _level.Objects.Find(o => o.X == cell.X && o.Y == cell.Y);
        int tile = _level.GetTile(cell.X, cell.Y);
        if (obj != null)
            SetBrushObject(obj.Type, obj.Properties.GetValueOrDefault("color"), obj.Properties.GetValueOrDefault("number"));
        else if (tile != TileDefs.Empty)
            SetBrushTile(tile);
        else
        {
            SetStatus("Nothing to pick here.");
            return;
        }
        if (_tool == Tool.Picker)
            SetTool(Tool.Brush);
        SetStatus($"Picked {_brush.Name}.");
    }

    // Applies the brush (or erases, with the eraser tool) at a cell.
    public void PaintCell(Vector2I cell)
    {
        if (_tool == Tool.Eraser)
            EraseCell(cell);
        else
            PlaceBrush(cell);
    }

    private void PlaceBrush(Vector2I cell)
    {
        if (!_level.InBounds(cell.X, cell.Y))
            return;
        if (_brush.IsTile)
        {
            if (_level.GetTile(cell.X, cell.Y) == _brush.TileId)
                return;
            RecordStroke();
            _level.SetTile(cell.X, cell.Y, _brush.TileId);
            _grids.SetCell(cell, _brush.TileId);
        }
        else
            PlaceObject(cell, _brush.ObjectType);
    }

    public void EraseCell(Vector2I cell)
    {
        if (!_level.InBounds(cell.X, cell.Y))
            return;
        if (_level.GetTile(cell.X, cell.Y) == TileDefs.Empty && !_level.Objects.Any(o => o.X == cell.X && o.Y == cell.Y))
            return;
        RecordStroke();
        _level.SetTile(cell.X, cell.Y, TileDefs.Empty);
        _grids.EraseCell(cell);
        if (_level.Objects.RemoveAll(o => o.X == cell.X && o.Y == cell.Y) > 0)
            RebuildMarkers();
    }

    public bool StartPlay()
    {
        string error = _level.Validate();
        if (error != null)
        {
            SetStatus(error, warn: true);
            return false;
        }

        _drag = Drag.None;
        _session = new PlaySession { Invincible = _invincible, RevealHidden = _revealHidden };
        _session.Setup(_level, _grids, _agentScene);
        _session.Completed += () => Callable.From(() => EndPlay("Level complete!")).CallDeferred();
        _session.Failed += () => Callable.From(() => EndPlay("Out of lives.")).CallDeferred();
        AddChild(_session);

        _markers.Visible = false;
        _ghost.Visible = false;
        _menuPanel.Visible = false;
        _editUi.Visible = false;
        _minimap.Visible = false;
        _playBar.Visible = true;
        _overlay.QueueRedraw();
        return true;
    }

    public void StopPlay()
    {
        if (_session == null)
            return;
        // The freed session still gets this frame's physics tick, which would
        // hide the hidden grid again after the tint below is restored.
        _session.SetPhysicsProcess(false);
        _session.QueueFree();
        _session = null;
        _grids.Hidden.Modulate = HiddenEditTint;
        _grids.Hidden.CollisionEnabled = true;
        _grids.Main.Modulate = Colors.White;   // clears the alarm tint
        RedrawLevel();   // restores walls destroyed during play
        _markers.Visible = true;
        _editUi.Visible = true;
        _minimap.Visible = true;
        _playBar.Visible = false;
        _camera.MakeCurrent();
    }

    private void EndPlay(string message)
    {
        StopPlay();
        SetStatus(message);
    }

    private Vector2I MouseCell() => _grids.Main.LocalToMap(_grids.Main.ToLocal(GetGlobalMousePosition()));

    private static Vector2 CellCenter(Vector2I cell) =>
        new(cell.X * TileSize + TileSize / 2f, cell.Y * TileSize + TileSize / 2f);

    // Snapshots the level before a change. Cell edits go through
    // RecordStroke, which records once per mouse stroke.
    private void RecordUndo()
    {
        _undo.Add(_level.Clone());
        if (_undo.Count > MaxUndo)
            _undo.RemoveAt(0);
        _redo.Clear();
        _strokeOpen = false;
    }

    private void RecordStroke()
    {
        if (!_strokeOpen)
            RecordUndo();
        _strokeOpen = true;
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;
        _redo.Add(_level);
        _level = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _strokeOpen = false;
        RedrawLevel();
        SetStatus("Undo.");
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;
        _undo.Add(_level);
        _level = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _strokeOpen = false;
        RedrawLevel();
        SetStatus("Redo.");
    }

    private void PlaceObject(Vector2I cell, string type)
    {
        var existing = _level.Objects.Find(o => o.X == cell.X && o.Y == cell.Y);
        if (existing?.Type == type
            && (!ObjectDefs.HasColorProperty(type) || existing.Properties.GetValueOrDefault("color") == _brushColor)
            && (!ObjectDefs.HasNumberProperty(type) || existing.Properties.GetValueOrDefault("number") == _brushNumber))
            return;
        RecordStroke();
        _level.Objects.RemoveAll(o => o.X == cell.X && o.Y == cell.Y);
        if (type == ObjectDefs.AgentStart)
            _level.Objects.RemoveAll(o => o.Type == ObjectDefs.AgentStart);
        var obj = new LevelObject { Type = type, X = cell.X, Y = cell.Y };
        if (ObjectDefs.HasColorProperty(type))
            obj.Properties["color"] = _brushColor;
        if (ObjectDefs.HasNumberProperty(type))
            obj.Properties["number"] = _brushNumber;
        _level.Objects.Add(obj);
        RebuildMarkers();
    }

    private void RedrawLevel()
    {
        _sky.Apply(_level);
        if (_skyButton != null)
            _skyButton.Icon = EditorTheme.Icon(_level.Sky == "day" ? "sun" : "moon");
        _grids.Fill(_level);
        RebuildMarkers();
        ApplyCameraLimits(_camera);
        ClampCamera();
        _minimap.Setup(_level, _camera);
    }

    private void RebuildMarkers()
    {
        foreach (var child in _markers.GetChildren())
            child.QueueFree();
        foreach (var obj in _level.Objects)
        {
            obj.Properties.TryGetValue("color", out var colorName);
            var texture = Sprites.ForObject(obj.Type, colorName);
            var marker = new Sprite2D
            {
                Texture = texture,
                Position = CellCenter(new Vector2I(obj.X, obj.Y)),
                Offset = new Vector2(0, Mathf.Min(0, 16 - texture.GetHeight() / 2f)),   // tall art stands on the cell floor
                Modulate = new Color(1, 1, 1, 0.9f),
            };
            if (ObjectDefs.HasNumberProperty(obj.Type) && obj.Properties.TryGetValue("number", out var number))
                marker.AddChild(new Label
                {
                    Text = number,
                    Position = new Vector2(-14, -16),
                    Theme = _theme,
                    ThemeTypeVariation = EditorTheme.ShiftBadge,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                });
            _markers.AddChild(marker);
        }
    }

    private void ApplyCameraLimits(Camera2D camera)
    {
        camera.LimitLeft = 0;
        camera.LimitTop = 0;
        camera.LimitRight = Mathf.Max(_level.Width * TileSize, 640);
        camera.LimitBottom = Mathf.Max(_level.Height * TileSize, 360);
    }

    // Camera2D limits only clip the rendered view. Without clamping the
    // position, panning past an edge piles up invisible overshoot that the
    // user must pan back through before the view moves.
    private void ClampCamera()
    {
        var half = GetViewportRect().Size / _camera.Zoom / 2;
        _camera.Position = new Vector2(
            ClampAxis(_camera.Position.X, _camera.LimitLeft, _camera.LimitRight, half.X),
            ClampAxis(_camera.Position.Y, _camera.LimitTop, _camera.LimitBottom, half.Y));
    }

    private static float ClampAxis(float value, int min, int max, float half) =>
        max - min < half * 2 ? min + half : Mathf.Clamp(value, min + half, max - half);

    // Steps through ZoomLevels, keeping the world point under the mouse (or
    // the view center) fixed on screen.
    public void ZoomBy(int step, Vector2? screenAnchor)
    {
        int next = Mathf.Clamp(_zoomIndex + step, 0, ZoomLevels.Length - 1);
        if (next == _zoomIndex)
            return;
        var size = GetViewportRect().Size;
        var offset = (screenAnchor ?? size / 2) - size / 2;
        var world = _camera.Position + offset / _camera.Zoom;
        _zoomIndex = next;
        _camera.Zoom = Vector2.One * ZoomLevels[next];
        _camera.Position = world - offset / _camera.Zoom;
        ClampCamera();
        _zoomLabel.Text = $"{ZoomLevels[next] * 100:0}%";
    }

    private void TogglePlay()
    {
        if (IsPlaying)
            StopPlay();
        else
            StartPlay();
    }

    public void ToggleInvincible()
    {
        _invincible = !_invincible;
        if (_session != null)
            _session.Invincible = _invincible;
        _godButton.SetPressedNoSignal(_invincible);
        _playGodButton.SetPressedNoSignal(_invincible);
        _playGodButton.Text = _invincible ? "INVINCIBLE" : "MORTAL";
        SetStatus(_invincible ? "Invincible playtest on: hazards and enemies can't hurt the agent." : "Invincible playtest off.");
    }

    public void ToggleRevealHidden()
    {
        _revealHidden = !_revealHidden;
        if (_session != null)
            _session.RevealHidden = _revealHidden;
        _revealButton.SetPressedNoSignal(_revealHidden);
        _playRevealButton.SetPressedNoSignal(_revealHidden);
        _playRevealButton.Text = _revealHidden ? "HIDDEN SOLID" : "HIDDEN OFF";
        SetStatus(_revealHidden ? "Hidden platforms are visible and solid in playtest." : "Hidden platforms need goggles again.");
    }

    private void ToggleGrid()
    {
        _showGrid = !_showGrid;
        _gridButton.SetPressedNoSignal(_showGrid);
    }

    public void ToggleSky()
    {
        RecordUndo();
        _level.Sky = _level.Sky == "day" ? "night" : "day";
        RedrawLevel();
    }

    private void SetStatus(string text, bool warn = false)
    {
        _status.Text = text;
        _status.ThemeTypeVariation = warn ? EditorTheme.KeyLabel : "";
    }

    private void OpenSaveDialog()
    {
        _nameEdit.Text = _level.Name;
        _descEdit.Text = _level.Description;
        _saveDialog.PopupCentered();
        _nameEdit.GrabFocus();
    }

    private void OnNewPressed()
    {
        if (IsPlaying)
            StopPlay();
        RecordUndo();
        _level = LevelData.NewDefault();
        RedrawLevel();
        SetStatus("New level. Ctrl+Z brings the old one back.");
    }

    private void OnSaveConfirmed()
    {
        string name = _nameEdit.Text.Trim();
        if (name.Length == 0)
        {
            SetStatus("Level needs a name.", warn: true);
            return;
        }
        _level.Name = name;
        _level.Description = _descEdit.Text.Trim();
        if (!LevelStore.Save(_level))
        {
            SetStatus("Save failed", warn: true);
            return;
        }
        _lastWrittenJson = LevelStore.Serialize(_level);
        SetStatus($"Saved {LevelStore.FileNameFor(name)}");
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
            SetStatus("Failed to load level.", warn: true);
            return;
        }
        if (IsPlaying)
            StopPlay();
        RecordUndo();
        SetLevel(loaded);
        SetStatus($"Loaded '{_level.Name}'");
    }

    // Replaces the edited level (load dialog, tools, tests).
    public void SetLevel(LevelData level)
    {
        _level = level;
        RedrawLevel();
    }
}
