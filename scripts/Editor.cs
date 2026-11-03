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

    // Hidden platforms live on their own layer so play mode can hide them
    // (infra goggles reveal them); in edit mode they show semi-transparent.
    private static readonly Color HiddenEditTint = new(1, 1, 1, 0.45f);

    private LevelData _level = LevelData.NewDefault();
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
    private ColorRect _cursor;
    private Camera2D _camera;
    private PlaySession _session;

    private Label _status;
    private HBoxContainer _bar;
    private HBoxContainer _paletteRoot;
    private Label _playHint;
    private Button _skyButton;
    private AcceptDialog _helpDialog;
    private AcceptDialog _saveDialog;
    private LineEdit _nameEdit;
    private TextEdit _descEdit;
    private ConfirmationDialog _loadDialog;
    private ItemList _levelList;
    private Minimap _minimap;
    private HBoxContainer _colorRow;
    private OptionButton _colorPicker;
    private HBoxContainer _numberRow;
    private OptionButton _numberPicker;
    private HBoxContainer _objectRow;
    private Label _brushLabel;

    private enum BrushKind { Tile, Object, Eraser }

    private BrushKind _brushKind = BrushKind.Tile;
    private int _brushTile = TileDefs.All[0].Id;
    private string _brushObject = "";
    private string _brushColor = ObjectDefs.KeyColors[0];
    private string _brushNumber = ObjectDefs.Numbers[0];

    public LevelData Level => _level;
    public bool IsPlaying => _session != null;
    public PlaySession Session => _session;
    public Agent PlayerAgent => _session?.Agent;
    public LevelGrids Grids => _grids;   // for tests
    // For tests: the center of what is on screen, after the camera limits.
    public Vector2 ViewCenter
    {
        get
        {
            _camera.ForceUpdateScroll();
            return _camera.GetScreenCenterPosition();
        }
    }

    // One row per top-bar button: the label letter, the Ctrl shortcut and the
    // action all come from here.
    private record MenuItem(string Name, Key Key, Action Run);

    private const string SkyItem = "Sky";

    private MenuItem[] _menu;

    private MenuItem[] BuildMenu() => new MenuItem[]
    {
        new("New", Key.N, OnNewPressed),
        new("Save", Key.S, OpenSaveDialog),
        new("Load", Key.L, OpenLoadDialog),
        new("Play", Key.P, TogglePlay),
        new(SkyItem, Key.K, ToggleSky),
        new("Menu", Key.M, () => GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn")),
        new("Help", Key.H, () => _helpDialog.PopupCentered()),
    };

    // "Sky: Night" with K becomes "S(k)y: Night".
    public static string MenuLabel(string text, Key key)
    {
        int index = text.IndexOf(key.ToString(), StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            throw new ArgumentException($"'{text}' has no letter for {key}");
        return $"{text[..index]}({text[index]}){text[(index + 1)..]}";
    }

    public override void _Ready()
    {
        _menu = BuildMenu();
        _agentScene = GD.Load<PackedScene>("res://scenes/agent.tscn");

        _sky = new SkyBackdrop();
        AddChild(_sky);   // behind the grids: empty cells show the sky

        _grids = new LevelGrids(this);
        _grids.Hidden.Modulate = HiddenEditTint;

        _markers = new Node2D();
        AddChild(_markers);

        _cursor = new ColorRect
        {
            Size = new Vector2(TileSize, TileSize),
            Color = new Color(1, 1, 1, 0.25f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_cursor);

        // Start looking at the bottom-left corner, where the default floor
        // and agent start are.
        _camera = new Camera2D { Position = new Vector2(320, _level.Height * TileSize - 180) };
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
        ClampCamera();

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

        if (_saveDialog.Visible || _loadDialog.Visible || _helpDialog.Visible)
            return;

        if (@event is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true, AltPressed: false, ShiftPressed: false, MetaPressed: false } shortcut)
        {
            var item = Array.Find(_menu, m => m.Key == shortcut.Keycode);
            if (item != null)
            {
                item.Run();
                GetViewport()?.SetInputAsHandled();
                return;
            }
        }

        // Exact match: Shift+Ctrl+P must not fall through to plain P.
        if (@event.IsActionPressed("editor_play", false, true))
        {
            StartPlay();
            return;
        }
        if (@event.IsActionPressed("ui_undo"))
        {
            Undo();
            return;
        }
        if (@event.IsActionPressed("ui_redo"))
        {
            Redo();
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } button)
        {
            _strokeOpen = false;
            if (button.ButtonIndex == MouseButton.Left)
                PaintCell(MouseCell());
            else if (button.ButtonIndex == MouseButton.Right)
                EraseCell(MouseCell());
        }
        else if (@event is InputEventMouseButton)
        {
            _strokeOpen = false;   // released: the next paint starts a new stroke
        }
        else if (@event is InputEventMouseMotion motion)
        {
            if (motion.ButtonMask.HasFlag(MouseButtonMask.Middle))
            {
                _camera.Position -= motion.Relative / _camera.Zoom;
                ClampCamera();
            }
            else if (motion.ButtonMask.HasFlag(MouseButtonMask.Left))
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

    public void SetBrushObject(string type, string color = null, string number = null)
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
        if (number != null)
        {
            _brushNumber = number;
            int index = System.Array.IndexOf(ObjectDefs.Numbers, number);
            if (_numberPicker != null && index >= 0)
                _numberPicker.Selected = index;
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
        if (_numberRow != null)
            _numberRow.Visible = _brushKind == BrushKind.Object && ObjectDefs.HasNumberProperty(_brushObject);
    }

    public void PaintCell(Vector2I cell)
    {
        if (!_level.InBounds(cell.X, cell.Y))
            return;
        switch (_brushKind)
        {
            case BrushKind.Tile:
                if (_level.GetTile(cell.X, cell.Y) == _brushTile)
                    return;
                RecordStroke();
                _level.SetTile(cell.X, cell.Y, _brushTile);
                _grids.SetCell(cell, _brushTile);
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
            _status.Text = error;
            return false;
        }

        _session = new PlaySession();
        _session.Setup(_level, _grids, _agentScene);
        _session.Completed += () => Callable.From(() => EndPlay("Level complete!")).CallDeferred();
        _session.Failed += () => Callable.From(() => EndPlay("Out of lives.")).CallDeferred();
        AddChild(_session);

        _markers.Visible = false;
        _cursor.Visible = false;
        _minimap.Visible = false;
        _bar.Visible = false;
        _paletteRoot.Visible = false;
        _playHint.Visible = true;
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
        _minimap.Visible = true;
        _bar.Visible = true;
        _paletteRoot.Visible = true;
        _playHint.Visible = false;
        _camera.MakeCurrent();
    }

    private void EndPlay(string message)
    {
        StopPlay();
        _status.Text = message;
    }

    private Vector2I MouseCell() => _grids.Main.LocalToMap(_grids.Main.ToLocal(GetGlobalMousePosition()));

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
        _status.Text = "Undo.";
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
        _status.Text = "Redo.";
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
            _skyButton.Text = MenuLabel(_level.Sky == "day" ? "Sky: Day" : "Sky: Night",
                Array.Find(_menu, m => m.Name == SkyItem).Key);
        _grids.Fill(_level);
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
            obj.Properties.TryGetValue("color", out var colorName);
            var marker = new Sprite2D
            {
                Texture = Sprites.ForObject(obj.Type, colorName),
                Position = new Vector2(obj.X * TileSize + TileSize / 2f, obj.Y * TileSize + TileSize / 2f),
                Modulate = new Color(1, 1, 1, 0.9f),
            };
            if (ObjectDefs.HasNumberProperty(obj.Type) && obj.Properties.TryGetValue("number", out var number))
                marker.AddChild(new Label
                {
                    Text = number,
                    Position = new Vector2(-14, -16),
                    Theme = new Theme { DefaultFontSize = 10 },
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

    private void TogglePlay()
    {
        if (IsPlaying)
            StopPlay();
        else
            StartPlay();
    }

    private void OpenSaveDialog()
    {
        _nameEdit.Text = _level.Name;
        _descEdit.Text = _level.Description;
        _saveDialog.PopupCentered();
    }

    private string HelpText()
    {
        var shortcuts = string.Join("\n", _menu.Select(m => $"  Ctrl+{m.Key}  {m.Name}"));
        return "Mouse\n"
            + "  Left click: paint\n"
            + "  Right click: erase\n"
            + "  Middle drag: pan the camera\n\n"
            + "Keyboard\n"
            + "  Arrows: pan the camera\n"
            + "  P: play or stop\n"
            + "  Esc: stop playing\n"
            + "  Ctrl+Z / Ctrl+Y: undo / redo\n\n"
            + "Menu shortcuts\n" + shortcuts + "\n\n"
            + "Palette\n"
            + "  Pick a category on the left, then a swatch.\n"
            + "  The property row sets the brush color or number (1-8).\n"
            + "  A Floor circuit marker on an electric floor tile wires that\n"
            + "  whole connected floor region to its circuit number.\n\n"
            + "Level rules\n"
            + "  Exactly one agent start and at least one exit door.\n"
            + "  The level is validated before play.\n"
            + "  Leaving play restores the level.\n"
            + "  Save takes a name and a description shown in the level briefing.\n"
            + "  The minimap is top right.";
    }

    private void BuildUi()
    {
        var ui = new CanvasLayer();
        AddChild(ui);

        var theme = new Theme { DefaultFontSize = 10 };

        var bar = _bar = new HBoxContainer { Theme = theme };
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        ui.AddChild(bar);

        foreach (var item in _menu)
        {
            var button = new Button { Text = MenuLabel(item.Name, item.Key) };
            button.Pressed += item.Run;
            bar.AddChild(button);
            if (item.Name == SkyItem)
                _skyButton = button;
        }

        _status = new Label
        {
            Text = "Ctrl+H: help",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
        };
        bar.AddChild(_status);

        // Palette: vertical category list on the left, a horizontal row of
        // swatch buttons for the selected category, and the property row
        // (brush name, color, number) beneath it.
        var paletteRoot = _paletteRoot = new HBoxContainer { Theme = theme, Position = new Vector2(0, 32) };
        ui.AddChild(paletteRoot);

        // Shown instead of the editor UI while playtesting.
        _playHint = new Label { Text = "ESC to return to editor", Theme = theme, Visible = false };
        _playHint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 4);
        _playHint.GrowHorizontal = Control.GrowDirection.Begin;
        ui.AddChild(_playHint);

        var categoryPanel = new PanelContainer();
        paletteRoot.AddChild(categoryPanel);
        var categoryBox = new VBoxContainer();
        categoryPanel.AddChild(categoryBox);

        var rightBox = new VBoxContainer();
        paletteRoot.AddChild(rightBox);
        var objectPanel = new PanelContainer();
        rightBox.AddChild(objectPanel);
        var objectScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(380, 36),
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        objectPanel.AddChild(objectScroll);
        _objectRow = new HBoxContainer();
        objectScroll.AddChild(_objectRow);

        var propsPanel = new PanelContainer();
        rightBox.AddChild(propsPanel);
        var propsBox = new HBoxContainer();
        propsPanel.AddChild(propsBox);
        _brushLabel = new Label { Text = TileDefs.All[0].Name };
        propsBox.AddChild(_brushLabel);
        _colorRow = new HBoxContainer { Visible = false };
        _colorRow.AddChild(new Label { Text = "Color" });
        _colorPicker = new OptionButton();
        foreach (var name in ObjectDefs.KeyColors)
            _colorPicker.AddItem(name);
        _colorPicker.Selected = 0;
        _colorPicker.ItemSelected += index => _brushColor = ObjectDefs.KeyColors[(int)index];
        _colorRow.AddChild(_colorPicker);
        propsBox.AddChild(_colorRow);
        _numberRow = new HBoxContainer { Visible = false };
        _numberRow.AddChild(new Label { Text = "Number" });
        _numberPicker = new OptionButton();
        foreach (var number in ObjectDefs.Numbers)
            _numberPicker.AddItem(number);
        _numberPicker.Selected = 0;
        _numberPicker.ItemSelected += index => _brushNumber = ObjectDefs.Numbers[(int)index];
        _numberRow.AddChild(_numberPicker);
        propsBox.AddChild(_numberRow);

        var categories = PaletteCategories();
        var categoryGroup = new ButtonGroup();
        foreach (var (name, entries) in categories)
        {
            var captured = entries;
            var button = new Button
            {
                Text = name,
                ToggleMode = true,
                ButtonGroup = categoryGroup,
                Alignment = HorizontalAlignment.Left,
                ButtonPressed = name == "Terrain",
            };
            button.Pressed += () => ShowCategory(captured);
            categoryBox.AddChild(button);
        }
        var eraserButton = new Button
        {
            Text = "Eraser",
            ToggleMode = true,
            ButtonGroup = categoryGroup,
            Alignment = HorizontalAlignment.Left,
        };
        eraserButton.Pressed += () =>
        {
            SetBrushEraser();
            ShowCategory(System.Array.Empty<PaletteEntry>());
            _brushLabel.Text = "Eraser";
        };
        categoryBox.AddChild(eraserButton);
        ShowCategory(System.Array.Find(categories, c => c.Name == "Terrain").Entries);

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

        _helpDialog = new AcceptDialog { Title = "Editor help", Theme = theme };
        _helpDialog.AddChild(new Label { Text = HelpText() });
        ui.AddChild(_helpDialog);
    }

    // One selectable brush in the palette: a tile (TileId >= 0) or an object.
    private record PaletteEntry(string Name, Color Color, int TileId, string ObjectType);

    // Categories follow the README's palette grouping.
    private static (string Name, PaletteEntry[] Entries)[] PaletteCategories()
    {
        PaletteEntry Tile(TileDef def) => new(def.Name, def.Color, def.Id, null);
        PaletteEntry Obj(string type)
        {
            var def = ObjectDefs.All.First(d => d.Type == type);
            return new PaletteEntry(def.Name, def.Color, -1, type);
        }

        var wallIds = new[]
        {
            TileDefs.Wall, TileDefs.DestructibleWall, TileDefs.StoneBlocks, TileDefs.RedBrick,
            TileDefs.SteelPlate, TileDefs.Girder, TileDefs.Concrete, TileDefs.Sandbags,
            TileDefs.Container, TileDefs.PipeWall,
            TileDefs.BackgroundMesh, TileDefs.BackgroundRock, TileDefs.BackgroundPanel,
            TileDefs.BackgroundBrick, TileDefs.BackgroundCircuit, TileDefs.BackgroundHangar,
            TileDefs.BackgroundSky, TileDefs.BackgroundSkyDay,
        };
        var walls = wallIds.Select(id => Tile(TileDefs.Get(id))).ToArray();

        var terrain = TileDefs.All.Where(d => !wallIds.Contains(d.Id)).Select(Tile)
            .Append(Obj(ObjectDefs.Door))
            .Append(Obj(ObjectDefs.Teleport))
            .Append(Obj(ObjectDefs.Elevator))
            .Append(Obj(ObjectDefs.MovingPlatform))
            .Append(Obj(ObjectDefs.ElectricDoor))
            .Append(Obj(ObjectDefs.FlameTrap))
            .ToArray();

        return new[]
        {
            ("Mission", new[] { Obj(ObjectDefs.AgentStart), Obj(ObjectDefs.Checkpoint), Obj(ObjectDefs.ExitDoor) }),
            ("Walls", walls),
            ("Terrain", terrain),
            ("Devices", new[]
            {
                Obj(ObjectDefs.Computer), Obj(ObjectDefs.Switch), Obj(ObjectDefs.Laser),
                Obj(ObjectDefs.SecurityDoor), Obj(ObjectDefs.FloorCircuit),
                Obj(ObjectDefs.Camera), Obj(ObjectDefs.RocketLauncher), Obj(ObjectDefs.Closet),
            }),
            ("Movables", new[] { Obj(ObjectDefs.Barrel) }),
            ("Enemies", EnemyDefs.All.Select(d => new PaletteEntry(d.Name, d.Color, -1, d.Type)).ToArray()),
            ("Items", new[]
            {
                Obj(ObjectDefs.DoorKey), Obj(ObjectDefs.UsbKey), Obj(ObjectDefs.HealthKit),
                Obj(ObjectDefs.AmmoPack), Obj(ObjectDefs.Vest), Obj(ObjectDefs.Briefcase),
                Obj(ObjectDefs.InfraGoggles),
            }),
            ("Weapons", new[]
            {
                Obj(ObjectDefs.WeaponChainsaw), Obj(ObjectDefs.WeaponAssaultRifle),
                Obj(ObjectDefs.WeaponMinigun), Obj(ObjectDefs.WeaponCrossbow),
                Obj(ObjectDefs.WeaponDynamite),
            }),
            ("Collectibles", CollectibleDefs.All.Select(d => new PaletteEntry(d.Name, d.Color, -1, d.Type)).ToArray()),
            ("Decorations", DecorationDefs.All.Select(d => new PaletteEntry(d.Name, d.Color, -1, d.Type))
                .Append(Obj(ObjectDefs.Light)).ToArray()),
        };
    }

    private void ShowCategory(PaletteEntry[] entries)
    {
        foreach (var child in _objectRow.GetChildren())
            child.QueueFree();
        var group = new ButtonGroup();
        foreach (var entry in entries)
        {
            var captured = entry;
            var button = new Button
            {
                Icon = entry.TileId >= 0
                    ? TileSetBuilder.TileIcon(entry.TileId)
                    : Sprites.Icon(entry.ObjectType),
                ToggleMode = true,
                ButtonGroup = group,
                TooltipText = entry.Name,
                ButtonPressed = IsCurrentBrush(entry),
            };
            button.Pressed += () => SelectEntry(captured);
            _objectRow.AddChild(button);
        }
    }

    private bool IsCurrentBrush(PaletteEntry entry) => entry.TileId >= 0
        ? _brushKind == BrushKind.Tile && _brushTile == entry.TileId
        : _brushKind == BrushKind.Object && _brushObject == entry.ObjectType;

    private void SelectEntry(PaletteEntry entry)
    {
        if (entry.TileId >= 0)
            SetBrushTile(entry.TileId);
        else
            SetBrushObject(entry.ObjectType);
        _brushLabel.Text = entry.Name;
    }

    private void OnNewPressed()
    {
        if (IsPlaying)
            StopPlay();
        RecordUndo();
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
        RecordUndo();
        SetLevel(loaded);
        _status.Text = $"Loaded '{_level.Name}'";
    }

    // Replaces the edited level (load dialog, tools, tests).
    public void SetLevel(LevelData level)
    {
        _level = level;
        RedrawLevel();
    }

    public void ToggleSky()
    {
        RecordUndo();
        _level.Sky = _level.Sky == "day" ? "night" : "day";
        RedrawLevel();
    }
}
