using Godot;

namespace HackerOrSoldier;

// Main menu (README): New game, Continue level, Settings, Create level,
// Exit game. Settings covers difficulty, key rebinding and volumes. It wears
// the in-game panel theme over the bundled level's rooftop, which pans slowly
// under the night sky.
public partial class MainMenu : Control
{
    private static readonly string[] RebindableActions =
        { "move_left", "move_right", "jump", "shoot", "activate", "look_down" };

    private const string BackdropLevel = "operation-night-lift.json";
    private const int BackdropRoofRow = 20;     // the rooftop sits this far down the level
    private const float BackdropRoofAt = 0.72f; // and this far down the screen
    private const float PanPeriod = 400f;       // seconds for one sweep there and back

    private PanelContainer _menuPanel;
    private PanelContainer _confirmPanel;
    private PanelContainer _settingsPanel;
    private string _rebindAction;
    private Button _rebindButton;

    private SkyBackdrop _sky;
    private Node2D _world;
    private float _worldWidth;
    private float _panTime;

    public override void _Ready()
    {
        Profile.Current = Profile.Load();
        Profile.Current.ApplyKeyBinds();
        Sfx.ApplySettings();

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildBackdrop();

        var theme = BuildTheme();
        var center = new CenterContainer { Theme = theme };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        center.AddChild(column);

        var title = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        title.AddThemeConstantOverride("separation", 12);
        title.AddChild(TitleWord("HACKER", EditorTheme.Accent));
        title.AddChild(TitleWord("OR", EditorTheme.Dim));
        title.AddChild(TitleWord("SOLDIER", EditorTheme.Gold));
        column.AddChild(title);
        column.AddChild(new Label
        {
            Text = "Go in loud, or go in quiet.",
            ThemeTypeVariation = EditorTheme.DimLabel,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        // The menu, the new game confirmation and the settings take turns in
        // one spot under the title, so everything fits the 360 px tall base
        // screen and keeps the panel look (an embedded dialog window would not).
        var box = AddPanel(column, out _menuPanel);
        AddMenuButton(box, "New game", () => Show(_confirmPanel));
        AddMenuButton(box, "Continue level", () => GetTree().ChangeSceneToFile("res://scenes/game.tscn"));
        AddMenuButton(box, "Settings", () => Show(_settingsPanel));
        AddMenuButton(box, "Create level", () => GetTree().ChangeSceneToFile("res://scenes/editor.tscn"));
        AddMenuButton(box, "Exit game", () => GetTree().Quit());

        var confirm = AddPanel(column, out _confirmPanel);
        confirm.AddChild(new Label { Text = "New game", ThemeTypeVariation = EditorTheme.Heading });
        confirm.AddChild(new Label { Text = "Start again from the first level?" });
        confirm.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        AddMenuButton(confirm, "Start over", () =>
        {
            Profile.Current.LevelIndex = 0;
            Profile.Current.Save();
            GetTree().ChangeSceneToFile("res://scenes/game.tscn");
        });
        AddMenuButton(confirm, "Cancel", () => Show(_menuPanel));

        BuildSettingsPanel(column);
        Show(_menuPanel);    }

    private static VBoxContainer AddPanel(VBoxContainer column, out PanelContainer panel)
    {
        panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        column.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        return box;
    }

    private void Show(PanelContainer panel)
    {
        _menuPanel.Visible = panel == _menuPanel;
        _confirmPanel.Visible = panel == _confirmPanel;
        _settingsPanel.Visible = panel == _settingsPanel;
    }

    // The in-game panel look (GameFlow's briefing and pause screens), plus
    // flat pixel sliders to match.
    private static Theme BuildTheme()
    {
        var theme = EditorTheme.Build();
        theme.DefaultFontSize = 10;
        var panel = (StyleBox)theme.GetStylebox("panel", "PanelContainer").Duplicate();
        panel.SetContentMarginAll(10);
        theme.SetStylebox("panel", "PanelContainer", panel);

        theme.SetStylebox("slider", "HSlider", new StyleBoxFlat
        {
            BgColor = new Color("05070f"),
            BorderColor = new Color("2c3463"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginTop = 3, ContentMarginBottom = 3,
            AntiAliasing = false,
        });
        var fill = new StyleBoxFlat { BgColor = new Color("0d4c5c"), BorderColor = EditorTheme.Accent, AntiAliasing = false };
        fill.SetBorderWidthAll(1);
        fill.ContentMarginTop = fill.ContentMarginBottom = 3;
        theme.SetStylebox("grabber_area", "HSlider", fill);
        theme.SetStylebox("grabber_area_highlight", "HSlider", fill);
        theme.SetIcon("grabber", "HSlider", Block(5, 10, EditorTheme.Ink));
        theme.SetIcon("grabber_highlight", "HSlider", Block(5, 10, Colors.White));
        return theme;
    }

    private static ImageTexture Block(int width, int height, Color color)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        img.Fill(color);
        return ImageTexture.CreateFromImage(img);
    }

    private static Label TitleWord(string text, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 32);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    // Night sky over the whole screen, the bundled level's rooftop drifting
    // past in front of it, and a dark veil so the panels stay readable. The
    // sky stays put while the level pans, which reads as depth. Without the
    // level file (an exported build keeps levels per player) the sky shows
    // alone.
    private void BuildBackdrop()
    {
        _sky = new SkyBackdrop();
        AddChild(_sky);
        _sky.Apply(GetViewportRect().Size, day: false);
        GetViewport().SizeChanged += () => _sky.Apply(GetViewportRect().Size, day: false);

        var level = LevelStore.Load(BackdropLevel);
        if (level != null)
        {
            _world = new Node2D();
            AddChild(_world);
            new LevelGrids(_world).Fill(level);
            _worldWidth = level.Width * TileSetBuilder.TileSize;
            _panTime = GD.Randf() * PanPeriod;   // a different stretch of rooftop each launch
            PanWorld();
        }

        var veil = new ColorRect { Color = new Color("05070f", 0.5f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(veil);
    }

    public override void _Process(double delta)
    {
        if (_world == null)
            return;
        _panTime = (_panTime + (float)delta) % PanPeriod;
        PanWorld();
    }

    // Eases back and forth across the level width, snapped to whole pixels.
    private void PanWorld()
    {
        var screen = GetViewportRect().Size;
        float range = Mathf.Max(0, _worldWidth - screen.X);
        float x = range * (0.5f - 0.5f * Mathf.Cos(_panTime / PanPeriod * Mathf.Tau));
        float y = screen.Y * BackdropRoofAt - BackdropRoofRow * TileSetBuilder.TileSize;
        _world.Position = new Vector2(-Mathf.Round(x), Mathf.Round(y));
    }

    private void AddMenuButton(VBoxContainer box, string text, System.Action onPressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 20) };
        button.Pressed += () =>
        {
            Sfx.Play(Sound.UiClick);
            onPressed();
        };
        box.AddChild(button);
    }

    private void BuildSettingsPanel(VBoxContainer column)
    {
        var outer = AddPanel(column, out _settingsPanel);
        outer.AddThemeConstantOverride("separation", 8);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 16);
        outer.AddChild(columns);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(140, 0) };
        columns.AddChild(box);
        var keys = new VBoxContainer();
        columns.AddChild(keys);

        box.AddChild(new Label { Text = "Difficulty", ThemeTypeVariation = EditorTheme.Heading });
        var difficulty = new OptionButton();
        difficulty.AddItem("Low");
        difficulty.AddItem("Medium");
        difficulty.AddItem("Hard");
        difficulty.Selected = Profile.Current.Difficulty switch { "low" => 0, "hard" => 2, _ => 1 };
        difficulty.ItemSelected += index =>
        {
            Profile.Current.Difficulty = index switch { 0 => "low", 2 => "hard", _ => "medium" };
            Profile.Current.Save();
        };
        box.AddChild(difficulty);

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        box.AddChild(new Label { Text = "Audio", ThemeTypeVariation = EditorTheme.Heading });
        box.AddChild(MakeToggle("Music", Profile.Current.MusicOn, on => Profile.Current.MusicOn = on));
        box.AddChild(new Label { Text = "Music volume", ThemeTypeVariation = EditorTheme.DimLabel });
        box.AddChild(MakeVolumeSlider(Profile.Current.MusicVolume, v => Profile.Current.MusicVolume = v));
        box.AddChild(MakeToggle("Sound", Profile.Current.SoundOn, on => Profile.Current.SoundOn = on));
        box.AddChild(new Label { Text = "Sound volume", ThemeTypeVariation = EditorTheme.DimLabel });
        box.AddChild(MakeVolumeSlider(Profile.Current.SoundVolume, v => Profile.Current.SoundVolume = v));

        keys.AddChild(new Label { Text = "Controls", ThemeTypeVariation = EditorTheme.Heading });
        keys.AddChild(new Label { Text = "Click, then press a key", ThemeTypeVariation = EditorTheme.DimLabel });
        foreach (var action in RebindableActions)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = action, CustomMinimumSize = new Vector2(70, 0) });
            var button = new Button { Text = CurrentKeyName(action), CustomMinimumSize = new Vector2(80, 0) };
            string captured = action;
            button.Pressed += () =>
            {
                _rebindAction = captured;
                _rebindButton = button;
                button.Text = "press a key...";
            };
            row.AddChild(button);
            keys.AddChild(row);
        }

        AddMenuButton(outer, "Back", () => Show(_menuPanel));
    }

    private static HSlider MakeVolumeSlider(int value, System.Action<int> onChanged)
    {
        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 10,
            Step = 1,
            Value = value,
            CustomMinimumSize = new Vector2(120, 12),
        };
        slider.ValueChanged += v =>
        {
            onChanged((int)v);
            Profile.Current.Save();
            Sfx.ApplySettings();
        };
        return slider;
    }

    private static CheckButton MakeToggle(string text, bool on, System.Action<bool> onChanged)
    {
        var toggle = new CheckButton { Text = text, ButtonPressed = on };
        toggle.Toggled += pressed =>
        {
            onChanged(pressed);
            Profile.Current.Save();
            Sfx.ApplySettings();
            Sfx.Play(Sound.UiClick);
        };
        return toggle;
    }

    private static string CurrentKeyName(string action)
    {
        foreach (var ev in InputMap.ActionGetEvents(action))
            if (ev is InputEventKey key)
                return OS.GetKeycodeString(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
        return "?";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_rebindAction == null || @event is not InputEventKey { Pressed: true } key)
            return;
        var keycode = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        Profile.Current.KeyBinds[_rebindAction] = (long)keycode;
        Profile.Current.ApplyKeyBinds();
        Profile.Current.Save();
        _rebindButton.Text = OS.GetKeycodeString(keycode);
        _rebindAction = null;
        _rebindButton = null;
        GetViewport().SetInputAsHandled();
    }
}
