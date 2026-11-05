using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Campaign mode: plays the saved levels in file order. Briefing screen
// before each level, statistics screen (with shop) after it, pause menu
// with restart, fail screen with retry. Progress and coins persist in the
// profile; lives and ammo carry across levels within a run.
public partial class GameFlow : Node2D
{
    private SkyBackdrop _sky;
    private LevelGrids _grids;
    private PackedScene _agentScene;
    private PlaySession _session;
    private RunState _run;
    private LevelData _level;
    private string[] _files;

    private CanvasLayer _ui;
    private PanelContainer _panel;
    private VBoxContainer _panelBox;
    private Theme _theme;
    private bool _paused;
    private HelpOverlay _help;

    public override void _Ready()
    {
        // The flow node keeps processing while the tree is paused so the
        // pause menu can unpause; the session is created Pausable.
        ProcessMode = ProcessModeEnum.Always;

        _agentScene = GD.Load<PackedScene>("res://scenes/agent.tscn");
        _sky = new SkyBackdrop();
        AddChild(_sky);   // behind the grids: empty cells show the sky
        _grids = new LevelGrids(this);
        _grids.Hidden.Modulate = new Color(1, 1, 1, 0);

        _theme = EditorTheme.Build();
        _theme.DefaultFontSize = 10;
        var panelBox = (StyleBox)_theme.GetStylebox("panel", "PanelContainer").Duplicate();
        panelBox.SetContentMarginAll(10);
        _theme.SetStylebox("panel", "PanelContainer", panelBox);
        _ui = new CanvasLayer();
        AddChild(_ui);
        // The center container fits the panel to each screen's content and
        // keeps it centered; the fixed width makes long text wrap instead of
        // pushing the panel off screen.
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _ui.AddChild(center);
        _panel = new PanelContainer { Theme = _theme };
        center.AddChild(_panel);
        _panelBox = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        _panel.AddChild(_panelBox);

        _files = LevelStore.ListFiles().OrderBy(f => f).ToArray();
        _run = RunState.NewRun();
        ShowBriefing();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session == null || !@event.IsActionPressed("pause"))
            return;
        if (_paused)
            Unpause();
        else
            ShowPauseMenu();
    }

    // The help screen shows while the help key is held and freezes the level
    // meanwhile, so the player can read it with enemies on screen. Polled
    // rather than event-driven so a release lost to a focus change still
    // closes it.
    public override void _Process(double delta)
    {
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

    // README: the level briefing shows the level name and description.
    private void ShowBriefing()
    {
        ClearSession();
        ClearPanel();
        if (_files.Length == 0)
        {
            AddPanelLabel("No levels yet.");
            AddPanelLabel("Create one in the level editor first.");
            AddPanelButton("Main menu", GoToMenu);
            return;
        }
        if (Profile.Current.LevelIndex >= _files.Length)
        {
            AddPanelLabel("Campaign complete!");
            AddPanelLabel("All levels finished. Make more in the editor.");
            AddPanelButton("Main menu", GoToMenu);
            return;
        }

        _level = LevelStore.Load(_files[Profile.Current.LevelIndex]);
        if (_level == null || _level.Validate() != null)
        {
            AddPanelLabel($"Level '{_files[Profile.Current.LevelIndex]}' failed to load.");
            AddPanelButton("Skip it", NextLevel);
            AddPanelButton("Main menu", GoToMenu);
            return;
        }

        AddPanelLabel($"Mission: {_level.Name}", 14);
        if (_level.Description.Length > 0)
            AddPanelLabel(_level.Description);
        AddPanelLabel($"Lives {_run.Lives}   Ammo {_run.Ammo}   Coins {Profile.Current.Coins}");
        AddPanelButton("Start", StartLevel);
        AddPanelButton("Main menu", GoToMenu);
    }

    private void StartLevel()
    {
        ClearPanel();
        _panel.Visible = false;
        _sky.Apply(_level);
        _grids.Fill(_level);

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

        ClearPanel();
        _panel.Visible = true;
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
        AddPanelButton("Next level", () =>
        {
            NextLevel();
        });
        AddPanelButton("Exit game", () => GetTree().Quit());
        ClearSession();
    }

    private void OnLevelFailed()
    {
        ClearSession();
        ClearPanel();
        _panel.Visible = true;
        AddPanelLabel("Mission failed", 14);
        AddPanelButton("Retry", () =>
        {
            _run = RunState.NewRun();
            ShowBriefing();
        });
        AddPanelButton("Main menu", GoToMenu);
    }

    private void ShowShop()
    {
        ClearPanel();
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
                if (Shop.TryBuy(captured.Id, _run))
                {
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
        GetTree().Paused = true;
        ClearPanel();
        _panel.Visible = true;
        AddPanelLabel("Paused", 14);
        AddPanelButton("Resume", Unpause);
        AddPanelButton("Restart level", () =>
        {
            Unpause();
            ShowBriefing();
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
        ShowBriefing();
    }

    private void GoToMenu() => GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");

    // Menu screens sit on the night sky alone, filling the screen, not on
    // the corner of the level just left.
    private void ClearSession()
    {
        if (_session != null)
        {
            _session.QueueFree();
            _session = null;
        }
        _grids.Clear();
        _grids.Main.Modulate = Colors.White;
        _grids.Hidden.Modulate = new Color(1, 1, 1, 0);
        _sky.Apply(GetViewportRect().Size, day: false);
    }

    private void ClearPanel()
    {
        foreach (var child in _panelBox.GetChildren())
        {
            _panelBox.RemoveChild(child);
            child.QueueFree();
        }
        _panel.Visible = true;
    }

    private void AddPanelLabel(string text, int fontSize = 0)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        if (fontSize > 0)
        {
            label.ThemeTypeVariation = EditorTheme.Heading;
            label.AddThemeFontSizeOverride("font_size", fontSize);
        }
        _panelBox.AddChild(label);
    }

    private void AddPanelButton(string text, System.Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += () => onPressed();
        _panelBox.AddChild(button);
    }
}
