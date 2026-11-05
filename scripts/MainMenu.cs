using Godot;

namespace HackerOrSoldier;

// Main menu (README): New game, Continue level, Settings, Create level,
// Exit game. Settings covers difficulty, key rebinding and volumes.
public partial class MainMenu : Control
{
    private static readonly string[] RebindableActions =
        { "move_left", "move_right", "jump", "shoot", "activate" };

    private ConfirmationDialog _newGameConfirm;
    private PanelContainer _settingsPanel;
    private string _rebindAction;
    private Button _rebindButton;

    public override void _Ready()
    {
        Profile.Current = Profile.Load();
        Profile.Current.ApplyKeyBinds();
        Sfx.ApplySettings();

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color(0.058f, 0.058f, 0.09f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var theme = new Theme { DefaultFontSize = 10 };
        var box = new VBoxContainer { Theme = theme, CustomMinimumSize = new Vector2(180, 0) };
        PlaceFromCenter(box, new Vector2(-90, -80));
        AddChild(box);

        box.AddChild(new Label
        {
            Text = "HACKER OR SOLDIER",
            Theme = new Theme { DefaultFontSize = 16 },
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        AddMenuButton(box, "New game", () => _newGameConfirm.PopupCentered());
        AddMenuButton(box, "Continue level", () => GetTree().ChangeSceneToFile("res://scenes/game.tscn"));
        AddMenuButton(box, "Settings", () => _settingsPanel.Visible = !_settingsPanel.Visible);
        AddMenuButton(box, "Create level", () => GetTree().ChangeSceneToFile("res://scenes/editor.tscn"));
        AddMenuButton(box, "Exit game", () => GetTree().Quit());

        _newGameConfirm = new ConfirmationDialog
        {
            Title = "New game",
            DialogText = "Are you sure you want to start from the beginning?",
            Theme = theme,
        };
        _newGameConfirm.Confirmed += () =>
        {
            Profile.Current.LevelIndex = 0;
            Profile.Current.Save();
            GetTree().ChangeSceneToFile("res://scenes/game.tscn");
        };
        AddChild(_newGameConfirm);

        BuildSettingsPanel(theme);
    }

    // Pins a control's top-left corner at a fixed offset from the screen
    // center, so the menu stays together as the window grows; the control
    // then grows right and down to fit its content.
    private static void PlaceFromCenter(Control control, Vector2 offset)
    {
        control.SetAnchorsPreset(LayoutPreset.Center);
        control.OffsetLeft = control.OffsetRight = offset.X;
        control.OffsetTop = control.OffsetBottom = offset.Y;
    }

    private void AddMenuButton(VBoxContainer box, string text, System.Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += () =>
        {
            Sfx.Play(Sound.UiClick);
            onPressed();
        };
        box.AddChild(button);
    }

    private void BuildSettingsPanel(Theme theme)
    {
        _settingsPanel = new PanelContainer { Theme = theme, Visible = false };
        PlaceFromCenter(_settingsPanel, new Vector2(110, -90));
        AddChild(_settingsPanel);
        var box = new VBoxContainer();
        _settingsPanel.AddChild(box);

        box.AddChild(new Label { Text = "Difficulty" });
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

        box.AddChild(MakeToggle("Music", Profile.Current.MusicOn, on => Profile.Current.MusicOn = on));
        box.AddChild(new Label { Text = "Music volume" });
        box.AddChild(MakeVolumeSlider(Profile.Current.MusicVolume, v => Profile.Current.MusicVolume = v));
        box.AddChild(MakeToggle("Sound", Profile.Current.SoundOn, on => Profile.Current.SoundOn = on));
        box.AddChild(new Label { Text = "Sound volume" });
        box.AddChild(MakeVolumeSlider(Profile.Current.SoundVolume, v => Profile.Current.SoundVolume = v));

        box.AddChild(new Label { Text = "Controls (click, then press a key)" });
        foreach (var action in RebindableActions)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = action, CustomMinimumSize = new Vector2(70, 0) });
            var button = new Button { Text = CurrentKeyName(action) };
            string captured = action;
            button.Pressed += () =>
            {
                _rebindAction = captured;
                _rebindButton = button;
                button.Text = "press a key...";
            };
            row.AddChild(button);
            box.AddChild(row);
        }
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
