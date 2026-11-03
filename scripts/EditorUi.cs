using System.Linq;
using Godot;

namespace HackerOrSoldier;

// The editor's widgets: top bar (categories, tools, toggles, play), item
// bar, status bar, file menu, play bar and dialogs. Editor.cs owns the
// state they show.
public partial class Editor
{
    private void BuildUi()
    {
        _theme = EditorTheme.Build();
        var ui = new CanvasLayer();
        AddChild(ui);

        _editUi = new Control { Theme = _theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        _editUi.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ui.AddChild(_editUi);

        _bars = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _bars.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _bars.AddThemeConstantOverride("separation", 0);
        _editUi.AddChild(_bars);
        _bars.AddChild(BuildTopBar());

        var itemRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _bars.AddChild(itemRow);
        itemRow.AddChild(BuildItemBar());

        _editUi.AddChild(BuildStatusBar());

        _minimap = new Minimap();
        _minimap.Jumped += world =>
        {
            _camera.Position = world;
            ClampCamera();
        };
        ui.AddChild(_minimap);
        _bars.Resized += () =>
        {
            _minimap.Top = _bars.Size.Y + 4;
            _minimap.Setup(_level, _camera);
        };

        ui.AddChild(_menuPanel);   // above the minimap
        _playBar = BuildPlayBar();
        ui.AddChild(_playBar);

        BuildDialogs(ui);
    }

    private Button IconButton(string icon, EditorCommand command, bool toggle = false)
    {
        var button = new Button
        {
            Icon = EditorTheme.Icon(icon),
            TooltipText = command.Tooltip,
            ToggleMode = toggle,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.Pressed += () =>
        {
            command.Run();
            if (toggle)
                RefreshBrushUi();
        };
        return button;
    }

    private Control BuildTopBar()
    {
        var panel = new PanelContainer();
        var row = new HBoxContainer();
        panel.AddChild(row);

        for (int i = 0; i < EditorPalette.Categories.Length; i++)
        {
            var category = EditorPalette.Categories[i];
            int index = i;
            var tab = new Button
            {
                Icon = category.Icon.Icon,
                ThemeTypeVariation = EditorTheme.Tab,
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"{category.Name}  ({EditorPalette.CategoryChord(i)})",
            };
            tab.Pressed += () => ShowCategory(index);
            _tabs.Add(tab);
            row.AddChild(tab);
        }

        row.AddChild(new VSeparator());
        foreach (var (tool, icon, name) in new[]
                 {
                     (Tool.Brush, "brush", "Brush"), (Tool.Rect, "rect", "Rectangle fill"),
                     (Tool.Eraser, "eraser", "Eraser"), (Tool.Picker, "picker", "Picker"),
                 })
        {
            var button = IconButton(icon, Command(name), toggle: true);
            button.TooltipText += tool switch
            {
                Tool.Rect => "\nor hold Shift while dragging",
                Tool.Eraser => "\nor right mouse button",
                Tool.Picker => "\nor Alt+click",
                _ => "",
            };
            _toolButtons[tool] = button;
            row.AddChild(button);
        }

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        _gridButton = IconButton("grid", Command("Grid"), toggle: true);
        _gridButton.ButtonPressed = _showGrid;
        row.AddChild(_gridButton);
        _skyButton = IconButton("moon", Command("Day / night sky"));
        row.AddChild(_skyButton);
        _godButton = IconButton("shield", Command("Invincible playtest"), toggle: true);
        row.AddChild(_godButton);
        row.AddChild(new VSeparator());
        row.AddChild(IconButton("undo", Command("Undo")));
        row.AddChild(IconButton("redo", Command("Redo")));
        row.AddChild(new VSeparator());

        Button menu = null;
        menu = IconButton("menu", new EditorCommand("Menu", "File", OpenMenu));
        row.AddChild(menu);
        row.AddChild(IconButton("help", Command("Help")));

        var play = new Button
        {
            Text = "PLAY",
            Icon = EditorTheme.Icon("play"),
            ThemeTypeVariation = EditorTheme.Play,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = Command("Play / stop").Tooltip,
        };
        play.Pressed += TogglePlay;
        row.AddChild(play);

        _menuPanel = BuildMenuPanel();
        return panel;

        void OpenMenu()
        {
            if (_menuPanel.Visible)
            {
                _menuPanel.Hide();
                return;
            }
            var at = menu.GetGlobalRect();
            _menuPanel.Show();
            _menuPanel.ResetSize();
            _menuPanel.Position = new Vector2(at.End.X - _menuPanel.Size.X, at.End.Y + 2);
        }
    }

    // File commands as a dropdown under the menu button.
    private Control BuildMenuPanel()
    {
        var panel = new PanelContainer { Visible = false, Theme = _theme };
        var list = new VBoxContainer();
        panel.AddChild(list);
        foreach (var command in _commands.Where(c => c.Group == "File"))
        {
            var item = new Button
            {
                Text = command.Name,
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(120, 0),
            };
            var keys = new Label
            {
                Text = command.Chords[^1].ToString(),
                ThemeTypeVariation = EditorTheme.KeyLabel,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            keys.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            keys.OffsetRight = -4;
            item.AddChild(keys);
            item.Pressed += () =>
            {
                panel.Hide();
                command.Run();
            };
            list.AddChild(item);
        }
        return panel;
    }

    private Control BuildItemBar()
    {
        var panel = new PanelContainer();
        var row = new HBoxContainer();
        panel.AddChild(row);
        _categoryLabel = new Label
        {
            ThemeTypeVariation = EditorTheme.Heading,
            CustomMinimumSize = new Vector2(64, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(_categoryLabel);
        _slotRow = new HBoxContainer();
        row.AddChild(_slotRow);
        return panel;
    }

    private Control BuildStatusBar()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        panel.GrowVertical = Control.GrowDirection.Begin;
        var row = new HBoxContainer();
        panel.AddChild(row);

        _brushIcon = new TextureRect { StretchMode = TextureRect.StretchModeEnum.KeepCentered };
        row.AddChild(_brushIcon);
        _brushLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(_brushLabel);

        _colorChips = new HBoxContainer();
        foreach (var color in ObjectDefs.KeyColors)
        {
            var swatch = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            swatch.Fill(ObjectDefs.KeyColor(color));
            string captured = color;
            var chip = new Button
            {
                Icon = ImageTexture.CreateFromImage(swatch),
                ThemeTypeVariation = EditorTheme.Chip,
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"{color}  ([ / ])",
            };
            chip.Pressed += () =>
            {
                _brushColor = captured;
                RefreshBrushUi();
            };
            _colorChips.AddChild(chip);
        }
        row.AddChild(_colorChips);

        _numberChips = new HBoxContainer();
        foreach (var number in ObjectDefs.Numbers)
        {
            string captured = number;
            var chip = new Button
            {
                Text = number,
                ThemeTypeVariation = EditorTheme.Chip,
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"Circuit {number}  ([ / ])",
                CustomMinimumSize = new Vector2(10, 0),
            };
            chip.Pressed += () =>
            {
                _brushNumber = captured;
                RefreshBrushUi();
            };
            _numberChips.AddChild(chip);
        }
        row.AddChild(_numberChips);
        _propertyHint = new Label { Text = "[ ]", ThemeTypeVariation = EditorTheme.DimLabel, VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(_propertyHint);

        row.AddChild(new VSeparator());
        _status = new Label
        {
            Text = "F1: every shortcut",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(_status);
        _cellLabel = new Label { ThemeTypeVariation = EditorTheme.DimLabel, CustomMinimumSize = new Vector2(34, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        row.AddChild(_cellLabel);
        row.AddChild(new VSeparator());
        _zoomLabel = new Label { Text = "100%", ThemeTypeVariation = EditorTheme.DimLabel, VerticalAlignment = VerticalAlignment.Center, TooltipText = "Zoom  (Ctrl+wheel, = / -)", MouseFilter = Control.MouseFilterEnum.Stop };
        row.AddChild(_zoomLabel);
        return panel;
    }

    private Control BuildPlayBar()
    {
        var panel = new PanelContainer { Theme = _theme, Visible = false };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 4);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        var row = new HBoxContainer();
        panel.AddChild(row);

        _playGodButton = new Button
        {
            Text = "MORTAL",
            Icon = EditorTheme.Icon("shield"),
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = Command("Invincible playtest").Tooltip,
        };
        _playGodButton.Pressed += ToggleInvincible;
        row.AddChild(_playGodButton);

        var stop = new Button
        {
            Text = "EDIT  Esc",
            Icon = EditorTheme.Icon("stop"),
            ThemeTypeVariation = EditorTheme.Play,
            FocusMode = Control.FocusModeEnum.None,
        };
        stop.Pressed += () => Callable.From(StopPlay).CallDeferred();
        row.AddChild(stop);
        return panel;
    }

    private void BuildDialogs(CanvasLayer ui)
    {
        _saveDialog = new AcceptDialog { Title = "Save level", OkButtonText = "Save", Theme = _theme };
        var form = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        form.AddChild(new Label { Text = "Name", ThemeTypeVariation = EditorTheme.Heading });
        _nameEdit = new LineEdit();
        form.AddChild(_nameEdit);
        form.AddChild(new Label { Text = "Description (shown in the level briefing)", ThemeTypeVariation = EditorTheme.Heading });
        _descEdit = new TextEdit { CustomMinimumSize = new Vector2(0, 80) };
        form.AddChild(_descEdit);
        _saveDialog.AddChild(form);
        _saveDialog.RegisterTextEnter(_nameEdit);
        _saveDialog.Confirmed += OnSaveConfirmed;
        ui.AddChild(_saveDialog);

        _loadDialog = new ConfirmationDialog { Title = "Load level", OkButtonText = "Load", Theme = _theme };
        _levelList = new ItemList { CustomMinimumSize = new Vector2(300, 150) };
        _levelList.ItemActivated += _ =>
        {
            _loadDialog.Hide();
            OnLoadConfirmed();
        };
        _loadDialog.AddChild(_levelList);
        _loadDialog.Confirmed += OnLoadConfirmed;
        ui.AddChild(_loadDialog);

        _helpDialog = new AcceptDialog { Title = "Editor shortcuts", Theme = _theme };
        _helpDialog.AddChild(BuildHelp());
        ui.AddChild(_helpDialog);
    }

    // The help sheet is generated from the command table, so a new command
    // or a changed key shows up here without editing text.
    private Control BuildHelp()
    {
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);

        var mouse = new[]
        {
            ("Left", "paint"), ("Right", "erase"), ("Shift+drag", "fill / erase a rectangle"),
            ("Alt+click", "pick the brush under the cursor"), ("Middle drag", "pan"), ("Space+drag", "pan"),
            ("Wheel", "next / prev item"), ("Shift+wheel", "next / prev category"), ("Ctrl+wheel", "zoom"),
            ("Minimap", "click to jump"),
        };
        var palette = new[]
        {
            ("1 - 0", "items 1-10"), ("Shift+1 - 0", "items 11-20"), ("Alt+1 - 0", "category"),
            ("Arrows/WASD", "pan (Shift: faster)"),
        };
        var groups = _commands.GroupBy(c => c.Group)
            .ToDictionary(g => g.Key, g => g.Select(c => (c.Keys, c.Name)).ToArray());

        AddColumn(columns, ("Mouse", mouse));
        AddColumn(columns, ("Palette", palette.Concat(groups["Palette"]).ToArray()), ("Tools", groups["Tools"]));
        AddColumn(columns, ("View", groups["View"]), ("Edit", groups["Edit"]), ("File", groups["File"]));
        return columns;
    }

    // One grid per column, so keys line up across its sections.
    private static void AddColumn(HBoxContainer columns, params (string Heading, (string Keys, string Name)[] Rows)[] sections)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 1);
        foreach (var (heading, rows) in sections)
        {
            if (grid.GetChildCount() > 0)
            {
                grid.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
                grid.AddChild(new Control());
            }
            grid.AddChild(new Label { Text = heading.ToUpperInvariant(), ThemeTypeVariation = EditorTheme.Heading });
            grid.AddChild(new Control());
            foreach (var (keys, name) in rows)
            {
                grid.AddChild(new Label { Text = keys, ThemeTypeVariation = EditorTheme.KeyLabel });
                grid.AddChild(new Label { Text = name });
            }
        }
        columns.AddChild(grid);
    }

    private void ShowCategory(int index)
    {
        _category = index;
        var category = EditorPalette.Categories[index];
        _categoryLabel.Text = category.Name.ToUpperInvariant();
        _categoryLabel.TooltipText = EditorPalette.CategoryChord(index).ToString();
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].SetPressedNoSignal(i == index);

        foreach (var child in _slotRow.GetChildren())
        {
            _slotRow.RemoveChild(child);
            child.QueueFree();
        }
        _slots.Clear();
        for (int i = 0; i < category.Entries.Length; i++)
        {
            if (i == EditorPalette.SlotsPerTier)
            {
                _slotRow.AddChild(new VSeparator());
                _slotRow.AddChild(new Label { Text = "SHIFT", ThemeTypeVariation = EditorTheme.KeyLabel, VerticalAlignment = VerticalAlignment.Center });
            }
            var entry = category.Entries[i];
            int slot = i;
            var chord = EditorPalette.SlotChord(i);
            var button = new Button
            {
                Icon = entry.Icon,
                ThemeTypeVariation = EditorTheme.Slot,
                ToggleMode = true,
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"{entry.Name}  ({chord})",
                CustomMinimumSize = new Vector2(22, 22),
                IconAlignment = HorizontalAlignment.Center,
            };
            button.AddChild(new Label
            {
                Text = Chord.KeyName(chord.Key),
                ThemeTypeVariation = chord.Shift ? EditorTheme.ShiftBadge : EditorTheme.Badge,
                Position = new Vector2(1, -2),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            button.Pressed += () => SelectSlot(slot);
            _slots.Add(button);
            _slotRow.AddChild(button);
        }
        RefreshBrushUi();
    }

    private void RefreshBrushUi()
    {
        if (_brush == null)
            return;
        var entries = EditorPalette.Categories[_category].Entries;
        bool painting = _tool is Tool.Brush or Tool.Rect;
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].SetPressedNoSignal(painting && entries[i] == _brush);
        foreach (var (tool, button) in _toolButtons)
            button.SetPressedNoSignal(tool == _tool);

        _brushIcon.Texture = _tool switch
        {
            Tool.Eraser => EditorTheme.Icon("eraser"),
            Tool.Picker => EditorTheme.Icon("picker"),
            _ => _brush.Icon,
        };
        _brushLabel.Text = _tool switch
        {
            Tool.Eraser => "Eraser",
            Tool.Picker => "Picker",
            _ => _brush.Name,
        };
        bool hasColor = painting && _brush.ObjectType != null && ObjectDefs.HasColorProperty(_brush.ObjectType);
        bool hasNumber = painting && _brush.ObjectType != null && ObjectDefs.HasNumberProperty(_brush.ObjectType);
        _colorChips.Visible = hasColor;
        _numberChips.Visible = hasNumber;
        _propertyHint.Visible = hasColor || hasNumber;
        for (int i = 0; i < ObjectDefs.KeyColors.Length; i++)
            ((Button)_colorChips.GetChild(i)).SetPressedNoSignal(ObjectDefs.KeyColors[i] == _brushColor);
        for (int i = 0; i < ObjectDefs.Numbers.Length; i++)
            ((Button)_numberChips.GetChild(i)).SetPressedNoSignal(ObjectDefs.Numbers[i] == _brushNumber);

        _ghost.Texture = _brush.IsTile
            ? TileSetBuilder.TileTexture(_brush.TileId)
            : Sprites.ForObject(_brush.ObjectType, hasColor ? _brushColor : null);
    }
}
