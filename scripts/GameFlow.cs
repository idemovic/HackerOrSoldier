using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Campaign mode: a world journey through the saved levels, one per stop of
// Campaign.Stops, each naming its level file. Between levels the world map is the backdrop:
// the plane flies to the next city, its story types out in the panel (docked
// away from the city), and Start begins the level. Statistics screen (with
// shop) after a level, pause menu with restart, fail screen with retry.
// Progress and coins persist in the profile; lives and ammo carry across
// levels within a run.
public partial class GameFlow : Node2D
{
    // Which screen is up. Pause overlays Level.
    public enum Screen { Intro, Flight, City, Level, Stats, Shop, Outro, Failed }

    private const float TypeCharsPerSecond = 40f;

    public Screen Current { get; private set; } = Screen.Intro;
    public PanelContainer Panel => _panel;
    public WorldMap Map => _map;

    private SkyBackdrop _sky;
    private WorldMap _map;
    private LevelGrids _grids;
    private PackedScene _agentScene;
    private PlaySession _session;
    private RunState _run;
    private LevelData _level;

    private CanvasLayer _ui;
    private PanelContainer _panel;
    private VBoxContainer _panelBox;
    private Theme _theme;
    private bool _paused;
    private HelpOverlay _help;
    private Label _typingLabel;
    private float _typed;

    public override void _Ready()
    {
        // The flow node keeps processing while the tree is paused so the
        // pause menu can unpause; the session is created Pausable.
        ProcessMode = ProcessModeEnum.Always;

        _agentScene = GD.Load<PackedScene>("res://scenes/agent.tscn");
        _sky = new SkyBackdrop();
        AddChild(_sky);   // behind the grids: empty cells show the sky
        _map = new WorldMap();
        AddChild(_map);
        _map.Landed += OnLanded;
        _grids = new LevelGrids(this);
        _grids.Hidden.Modulate = new Color(1, 1, 1, 0);

        _theme = EditorTheme.Build();
        _theme.DefaultFontSize = 10;
        var panelBox = (StyleBox)_theme.GetStylebox("panel", "PanelContainer").Duplicate();
        panelBox.SetContentMarginAll(10);
        _theme.SetStylebox("panel", "PanelContainer", panelBox);
        _ui = new CanvasLayer();
        AddChild(_ui);
        // The container fits the panel to each screen's content and docks it
        // at the top, bottom or middle; the fixed width makes long text wrap
        // instead of pushing the panel off screen.
        var dock = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        dock.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            dock.AddThemeConstantOverride($"margin_{side}", 6);
        _ui.AddChild(dock);
        _panel = new PanelContainer { Theme = _theme, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        dock.AddChild(_panel);
        _panelBox = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        _panel.AddChild(_panelBox);

        _run = RunState.NewRun();
        if (Profile.Current.LevelIndex == 0)
            ShowIntro();
        else
            ShowStop(fly: false);
    }

    // A click or Enter/Space finishes typing the story before anything else.
    public override void _Input(InputEvent @event)
    {
        if (_typingLabel == null)
            return;
        bool press = @event is InputEventMouseButton { Pressed: true } || @event.IsActionPressed("ui_accept");
        if (!press)
            return;
        GetViewport().SetInputAsHandled();
        FinishTyping();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session == null || !@event.IsActionPressed("pause"))
            return;
        if (_paused)
        {
            Sfx.Play(Sound.Unpause);
            Unpause();
        }
        else
        {
            ShowPauseMenu();
        }
    }

    // The help screen shows while the help key is held and freezes the level
    // meanwhile, so the player can read it with enemies on screen. Polled
    // rather than event-driven so a release lost to a focus change still
    // closes it.
    public override void _Process(double delta)
    {
        if (_typingLabel != null)
        {
            _typed += (float)delta * TypeCharsPerSecond;
            _typingLabel.VisibleCharacters = (int)_typed;
            if (_typed >= _typingLabel.GetTotalCharacterCount())
                FinishTyping();
        }
        bool held = _session != null && !_paused && Input.IsActionPressed(HelpOverlay.Action);
        if (held && _help == null)
        {
            _help = new HelpOverlay(_level);
            AddChild(_help);
            GetTree().Paused = true;
        }
        else if (!held && _help != null)
        {
            _help.QueueFree();
            _help = null;
            GetTree().Paused = _paused;   // Esc while holding leaves the pause menu up
        }
    }

    private void ShowIntro()
    {
        Current = Screen.Intro;
        ShowMap(taken: 0, parkedAt: -1);
        DockAwayFrom(-1);
        AddPanelLabel("Operation White Silence", 14);
        var story = AddPanelLabel(Campaign.Intro);
        AddPanelButton("Continue", () => ShowStop(fly: true));
        BeginTyping(story);
    }

    // The map for the stop the profile is on: the plane flies in from the
    // previous city, or is already parked there (a retry or a restart).
    private void ShowStop(bool fly)
    {
        int stop = Profile.Current.LevelIndex;
        if (Campaign.IsComplete(stop))
        {
            ShowOutro();
            return;
        }
        ShowMap(taken: stop, parkedAt: fly ? stop - 1 : stop);
        if (!fly)
        {
            ShowCityDialog();
            return;
        }
        Current = Screen.Flight;
        _panel.Visible = false;
        _map.Fly(stop - 1, stop);
    }

    private void OnLanded()
    {
        if (Current == Screen.Flight)
            ShowCityDialog();
    }

    // README: the level briefing shows the level name and description, now
    // under the city's story.
    private void ShowCityDialog()
    {
        Current = Screen.City;
        int stop = Profile.Current.LevelIndex;
        ClearPanel();
        DockAwayFrom(stop);
        AddPanelLabel(Campaign.Stops[stop].City, 14);
        var story = AddPanelLabel(Campaign.Stops[stop].Story);
        string file = Campaign.Stops[stop].LevelFile;
        if (!LevelStore.Exists(file))
        {
            AddPanelLabel($"Mission not built yet. Save a level as {file} in the editor.");
            AddPanelButton("Main menu", GoToMenu);
            BeginTyping(story);
            return;
        }

        _level = LevelStore.Load(file);
        if (_level == null || _level.Validate() != null)
        {
            AddPanelLabel($"Level '{file}' failed to load.");
            AddPanelButton("Skip it", NextLevel);
            AddPanelButton("Main menu", GoToMenu);
            BeginTyping(story);
            return;
        }

        AddPanelLabel($"Mission: {_level.Name}", 14);
        if (_level.Description.Length > 0)
            AddPanelLabel(_level.Description);
        AddPanelLabel($"Lives {_run.Lives}   Ammo {_run.Ammo}   Coins {Profile.Current.Coins}");
        AddPanelButton("Start", StartLevel);
        AddPanelButton("Main menu", GoToMenu);
        BeginTyping(story);
    }

    private void ShowOutro()
    {
        Current = Screen.Outro;
        ShowMap(taken: Campaign.Stops.Length, parkedAt: Campaign.Stops.Length - 1);
        DockAwayFrom(Campaign.Stops.Length - 1);
        AddPanelLabel("Mission complete", 14);
        var story = AddPanelLabel(Campaign.Outro);
        AddPanelButton("Main menu", GoToMenu);
        BeginTyping(story);
    }

    // Shows the map as the backdrop with the plane parked, and clears the panel.
    private void ShowMap(int taken, int parkedAt)
    {
        ClearSession();
        ClearPanel();
        _sky.Visible = false;
        _map.Visible = true;
        _map.SetProgress(taken);
        _map.Park(parkedAt);
    }

    private void DockAwayFrom(int stop) =>
        _panel.SizeFlagsVertical = WorldMap.DialogAtTop(stop) ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ShrinkEnd;

    // Types the label out and keeps the screen's buttons off until it is done.
    private void BeginTyping(Label story)
    {
        _typingLabel = story;
        _typed = 0f;
        story.VisibleCharacters = 0;
        foreach (var button in _panelBox.GetChildren().OfType<Button>())
            button.Disabled = true;
    }

    public void FinishTyping()
    {
        if (_typingLabel == null)
            return;
        _typingLabel.VisibleCharacters = -1;
        _typingLabel = null;
        Button first = null;
        foreach (var button in _panelBox.GetChildren().OfType<Button>())
        {
            button.Disabled = false;
            first ??= button;
        }
        first?.GrabFocus();
    }

    private void StartLevel()
    {
        ClearPanel();
        _panel.Visible = false;
        _map.Visible = false;
        _sky.Visible = true;
        _sky.Apply(_level);
        _grids.Fill(_level);

        Current = Screen.Level;
        _session = new PlaySession { ProcessMode = ProcessModeEnum.Pausable };
        _session.Setup(_level, _grids, _agentScene, _run);
        _session.Completed += () => Callable.From(OnLevelCompleted).CallDeferred();
        _session.Failed += () => Callable.From(OnLevelFailed).CallDeferred();
        AddChild(_session);
    }

    // README statistics: time, score, kills, accuracy, completeness, and the
    // Hacker tag for a non-violent run. Buttons: Shop, Next level, Exit game.
    private void OnLevelCompleted()
    {
        _run.Lives = _session.Lives;
        _run.Ammo = _session.Ammo;   // README: ammo transfers across levels
        Profile.Current.Coins += _session.CoinsCollected;
        Profile.Current.Save();

        int stop = Profile.Current.LevelIndex;
        ShowMap(taken: stop + 1, parkedAt: stop);
        Current = Screen.Stats;
        DockAwayFrom(stop);
        AddPanelLabel("Mission accomplished", 14);
        AddPanelLabel($"Level time {(int)(_session.Time / 60)}:{(int)_session.Time % 60:00}");
        AddPanelLabel($"Level score {_session.Score}");
        AddPanelLabel($"Kills {_session.Kills}");
        AddPanelLabel($"Accuracy {_session.AccuracyPercent}%");
        AddPanelLabel($"Completeness {_session.CompletenessPercent}%");
        if (_session.IsHackerRun)
            AddPanelLabel("HACKER - non-violent run");
        AddPanelLabel($"Coins {Profile.Current.Coins}");
        AddPanelButton("Shop", ShowShop);
        AddPanelButton(stop + 1 >= Campaign.Stops.Length ? "Finish" : "Next level", NextLevel);
        AddPanelButton("Exit game", () => GetTree().Quit());
    }

    private void OnLevelFailed()
    {
        int stop = Profile.Current.LevelIndex;
        ShowMap(taken: stop, parkedAt: stop);
        Current = Screen.Failed;
        DockAwayFrom(stop);
        AddPanelLabel("Mission failed", 14);
        AddPanelButton("Retry", () =>
        {
            _run = RunState.NewRun();
            ShowStop(fly: false);
        });
        AddPanelButton("Main menu", GoToMenu);
    }

    private void ShowShop()
    {
        Current = Screen.Shop;
        ClearPanel();
        DockAwayFrom(Profile.Current.LevelIndex);
        AddPanelLabel("Shop", 14);
        var coinsLabel = new Label { Text = $"Coins {Profile.Current.Coins}" };
        _panelBox.AddChild(coinsLabel);
        foreach (var item in Shop.Items)
        {
            var captured = item;
            var button = new Button { Text = $"{item.Name} ({item.Price} coins)" };
            if (item.Id == Shop.HackingTool && Profile.Current.HasHackingTool)
            {
                button.Text = $"{item.Name} (owned)";
                button.Disabled = true;
            }
            button.Pressed += () =>
            {
                if (!Shop.TryBuy(captured.Id, _run))
                {
                    Sfx.Play(Sound.CantAfford);
                }
                else
                {
                    Sfx.Play(Sound.Purchase);
                    Profile.Current.Save();
                    coinsLabel.Text = $"Coins {Profile.Current.Coins}";
                    if (captured.Id == Shop.HackingTool)
                    {
                        button.Text = $"{captured.Name} (owned)";
                        button.Disabled = true;
                    }
                }
            };
            _panelBox.AddChild(button);
        }
        AddPanelButton("Next level", NextLevel);
        AddPanelButton("Main menu", GoToMenu);
    }

    // README: pause triggers a menu with the Restart level option.
    private void ShowPauseMenu()
    {
        _paused = true;
        Sfx.Play(Sound.Pause);
        GetTree().Paused = true;
        ClearPanel();
        _panel.Visible = true;
        AddPanelLabel("Paused", 14);
        AddPanelButton("Resume", Unpause);
        AddPanelButton("Restart level", () =>
        {
            Unpause();
            ShowStop(fly: false);
        });
        AddPanelButton("Main menu", () =>
        {
            Unpause();
            GoToMenu();
        });
        AddPanelButton("Exit game", () => GetTree().Quit());
    }

    private void Unpause()
    {
        _paused = false;
        GetTree().Paused = false;
        ClearPanel();
        _panel.Visible = _session == null;
    }

    private void NextLevel()
    {
        Profile.Current.LevelIndex++;
        Profile.Current.Save();
        ShowStop(fly: !Campaign.IsComplete(Profile.Current.LevelIndex));
    }

    private void GoToMenu() => GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");

    // Menu screens sit on the world map, not on the corner of the level just left.
    private void ClearSession()
    {
        if (_session != null)
        {
            _session.QueueFree();
            _session = null;
        }
        _grids.Clear();
        _grids.Tint(Colors.White);
        _grids.Hidden.Modulate = new Color(1, 1, 1, 0);
    }

    private void ClearPanel()
    {
        _typingLabel = null;
        _panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        foreach (var child in _panelBox.GetChildren())
        {
            _panelBox.RemoveChild(child);
            child.QueueFree();
        }
        _panel.Visible = true;
    }

    private Label AddPanelLabel(string text, int fontSize = 0)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        if (fontSize > 0)
        {
            label.ThemeTypeVariation = EditorTheme.Heading;
            label.AddThemeFontSizeOverride("font_size", fontSize);
        }
        _panelBox.AddChild(label);
        return label;
    }

    private void AddPanelButton(string text, System.Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += () =>
        {
            Sfx.Play(Sound.UiClick);
            onPressed();
        };
        _panelBox.AddChild(button);
    }
}
