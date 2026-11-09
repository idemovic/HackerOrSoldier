using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

// Owns one play-through: the agent, lives, ammo, inventory, the hazard clock,
// the HUD and every node spawned from the level objects. Returning to edit
// mode frees the whole session, which restores the level to its pre-play
// state. Losing a life respawns at the last checkpoint touched, or the agent
// start before any, and keeps level state (opened doors stay open, picked
// items stay picked).
public partial class PlaySession : Node2D
{
    [Signal]
    public delegate void CompletedEventHandler();

    [Signal]
    public delegate void FailedEventHandler();

    private const float SpikeCycle = 10f;   // README: spike trap cycles every 10s
    private const float SpikeUpTime = 2f;   // each of the two spikes is raised 2s per cycle
    private const float SpikeMoveTime = 0.15f;   // the rise and the sink, inside the up time
    private const float HazardHearRange = 8 * 32f;   // traps are heard within 8 tiles
    private const float SpearStateTime = 3f;   // spears: holes, tips, out - 3s each
    private const float ItemDuration = 30f; // README: briefcase and goggles last 30s
    private const float RobotCycle = 10f;   // README: robots stop and turn off every 10s
    private const float RobotOffTime = 1f;
    private const float AlarmDuration = 20f;         // README: alarm stays on 20s from the last trigger
    private const float ExplosionRadius = 5 * 32f;   // README: dynamite kills in a 5 square radius
    // How long a lost life holds the screen: the agent crumples, the death
    // effect dissolves the screen to black, and only then does the respawn
    // (or the fail screen) happen behind it.
    private const float DeathDuration = 1.2f;

    public Agent Agent { get; private set; }
    public int Lives { get; private set; } = 3;   // README: default 3 lives
    public int Ammo { get; private set; } = 10;   // README: pistol, default 10 ammo
    public Weapon Weapon { get; private set; } = Weapon.Pistol;
    public int Arrows { get; private set; }          // crossbow ammo, separate pool
    public int DynamiteSticks { get; private set; }
    public bool HasVest { get; private set; }
    public int Score { get; private set; }
    public int CoinsCollected { get; private set; }
    public int Kills { get; private set; }
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    public bool IsHackerRun => Kills == 0;   // README: non-violent runs earn the Hacker tag
    public int AccuracyPercent => ShotsFired == 0 ? 100 : Mathf.RoundToInt(100f * ShotsHit / ShotsFired);
    public int CompletenessPercent => _collectibleTotal == 0 ? 100 : Mathf.RoundToInt(100f * _collectibleGot / _collectibleTotal);
    public bool GogglesActive => _gogglesTime > 0;
    // Editor playtest switch: hidden platforms behave as if goggles were on.
    public bool RevealHidden { get; set; }
    private bool HiddenRevealed => GogglesActive || RevealHidden;
    public float Time { get; set; }               // settable so tests can skip ahead
    // The two spikes of a spike tile take turns: the left one (0) is raised
    // during seconds 3-5 of each cycle, the right one (1) during the last
    // 2s. -1 while both are down.
    public int RaisedSpike
    {
        get
        {
            float turn = SpikeCycle / 2f;
            return Time % turn >= turn - SpikeUpTime ? (int)(Time % SpikeCycle / turn) : -1;
        }
    }
    // The spear state for the clock, also its tile frame: 0 bare holes,
    // 1 tips in the holes (a warning, harmless), 2 spears out (deadly).
    private int SpearFrame => (int)(Time / SpearStateTime) % TileSetBuilder.SpearFrames;
    public bool SpearsOut => SpearFrame == TileSetBuilder.SpearFrames - 1;

    // The spike tile frame for the clock, rising and sinking like the spears.
    private Vector2I SpikeCoords
    {
        get
        {
            int spike = RaisedSpike;
            if (spike < 0)
                return TileSetBuilder.SpikeCoordsFor(0, 0);
            float turn = SpikeCycle / 2f;
            float since = Time % turn - (turn - SpikeUpTime);
            float edge = Mathf.Min(since, SpikeUpTime - since);
            int full = TileSetBuilder.SpikeLevels;
            int level = edge >= SpikeMoveTime ? full : 1 + (int)(edge / SpikeMoveTime * (full - 1));
            return TileSetBuilder.SpikeCoordsFor(spike, level);
        }
    }
    public bool RobotsOff => Time % RobotCycle >= RobotCycle - RobotOffTime;
    public bool AlarmActive => _alarmTime > 0;
    public bool DisguiseActive => _disguiseTime > 0;
    public bool AgentDown => _agentDown;
    public float DropChance { get; set; } = 0.3f;   // README: some enemies "sometimes" drop an ammo pack
    public System.Collections.Generic.IReadOnlyList<Enemy> Enemies => _enemies;
    public System.Collections.Generic.IReadOnlyList<WallSwitch> Switches => _switches;
    public float LevelBottom => _level.Height * TileSetBuilder.TileSize;
    public float LevelRight => _level.Width * TileSetBuilder.TileSize;

    private LevelData _level;
    private LevelGrids _grids;
    private PackedScene _agentScene;
    private RunState _run;
    private readonly HashSet<string> _letters = new();
    private int _collectibleTotal;
    private int _collectibleGot;

    private readonly HashSet<Vector2I> _destroyed = new();
    private readonly HashSet<string> _keys = new();
    private readonly List<string> _heldItems = new();
    private readonly List<LockedDoor> _doors = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Computer> _computers = new();
    private readonly List<WallSwitch> _switches = new();
    private readonly List<Closet> _closets = new();
    private readonly List<Teleporter> _teleports = new();
    private readonly HashSet<Vector2I> _platformCells = new();
    private bool _teleportLock;
    private readonly HashSet<string> _usbKeys = new();
    private readonly HashSet<string> _circuitsOff = new();
    private readonly List<SecurityDoor> _securityDoors = new();
    // Electric floor cell -> circuit number, from each floor_circuit marker's
    // connected region. Cells absent here are live forever.
    private readonly Dictionary<Vector2I, string> _floorCircuits = new();
    private float _alarmTime;
    private float _gogglesTime;
    private float _disguiseTime;
    private Vector2 _respawnPoint;
    private Vector2 _startPoint;
    private Checkpoint _checkpoint;   // the lit one, null until the first is touched
    private readonly List<Checkpoint> _checkpoints = new();   // level object order
    private Camera2D _camera;
    private readonly List<Vector2I> _spikeCells = new();
    private Vector2I _spikeCoords = new(-1, -1);
    private readonly List<Vector2I> _spearCells = new();
    private int _spearFrame = -1;
    private int _raisedSpike = -1;
    private readonly List<Vector2I> _electricCells = new();
    private TerrainAmbience _ambience;
    private HeartRow _hearts;
    private TextureRect _weaponIcon;
    private RollingLabel _weaponCount;
    private string _hudWeapon;
    private HBoxContainer _scoreStat;
    private RollingLabel _scoreCount;
    private HBoxContainer _coinStat;
    private RollingLabel _coinCount;
    private HBoxContainer _alarmStat;
    private Label _alarmCount;
    private HBoxContainer _items;
    private string _itemsShown;
    private Label _activatePrompt;
    private readonly string _activateKey = HelpOverlay.KeysFor("activate");   // keys are rebound only in the main menu
    private bool _ended;
    private bool _agentDown;      // a life is being lost right now
    private bool _failAfterDeath; // it was the last one
    private float _deathTime;
    private DeathEffect _death;

    public void Setup(LevelData level, LevelGrids grids, PackedScene agentScene, RunState run = null)
    {
        _level = level;
        _grids = grids;
        _agentScene = agentScene;
        _run = run;
    }

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        _grids.Hidden.Modulate = new Color(1, 1, 1, 0);

        Lives = _run?.Lives ?? 3 + Profile.Current.AgentLivesBonus;
        Ammo = _run?.Ammo ?? 10;
        for (int i = 0; i < (_run?.Briefcases ?? 0); i++)
            _heldItems.Add(ObjectDefs.Briefcase);

        var decorations = new Node2D { Name = "Decorations" };
        AddChild(decorations);   // first child, so everything that moves draws over it
        foreach (var obj in _level.Objects)
        {
            var pos = new Vector2(obj.X * tile + tile / 2f, obj.Y * tile + tile / 2f);
            switch (obj.Type)
            {
                case ObjectDefs.AgentStart:
                    _respawnPoint = pos;
                    _startPoint = pos;
                    Agent = _agentScene.Instantiate<Agent>();
                    Agent.Position = pos;
                    Agent.Session = this;
                    var camera = new Camera2D
                    {
                        LimitLeft = 0,
                        LimitTop = 0,
                        LimitRight = Mathf.Max(_level.Width * tile, 640),
                        LimitBottom = Mathf.Max(_level.Height * tile, 360),
                    };
                    Agent.AddChild(camera);
                    AddChild(Agent);
                    camera.MakeCurrent();
                    _camera = camera;
                    break;
                case ObjectDefs.ExitDoor:
                    var exit = new ExitDoor { Position = pos };
                    exit.Reached += () => End(SignalName.Completed);
                    AddChild(exit);
                    break;
                case ObjectDefs.Checkpoint:
                    var checkpoint = new Checkpoint { Position = pos };
                    checkpoint.Reached += OnCheckpoint;
                    _checkpoints.Add(checkpoint);
                    AddChild(checkpoint);
                    break;
                case ObjectDefs.Door:
                    var door = new LockedDoor { Position = pos, ColorName = ColorOf(obj) };
                    _doors.Add(door);
                    AddChild(door);
                    break;
                case ObjectDefs.Computer:
                    var computer = new Computer { Position = pos, Number = NumberOf(obj) };
                    _computers.Add(computer);
                    AddChild(computer);
                    break;
                case ObjectDefs.SecurityDoor:
                    var securityDoor = new SecurityDoor { Position = pos, Number = NumberOf(obj) };
                    _securityDoors.Add(securityDoor);
                    AddChild(securityDoor);
                    break;
                case ObjectDefs.FloorCircuit:
                    break;   // editor-only marker, read by MapFloorCircuits
                case ObjectDefs.Switch:
                    var wallSwitch = new WallSwitch { Position = pos, Number = NumberOf(obj) };
                    _switches.Add(wallSwitch);
                    AddChild(wallSwitch);
                    break;
                case ObjectDefs.Laser:
                    AddChild(new LaserBeam { Position = pos, Number = NumberOf(obj), Session = this });
                    break;
                case ObjectDefs.Camera:
                    AddChild(new SecurityCamera { Position = pos, Session = this, FacesLeft = ObjectDefs.FacesLeft(obj) });
                    break;
                case ObjectDefs.Closet:
                    var closet = new Closet { Position = pos };
                    _closets.Add(closet);
                    AddChild(closet);
                    if (Agent != null)
                        MoveChild(closet, Agent.GetIndex());   // draw behind the agent
                    break;
                case ObjectDefs.Barrel or ObjectDefs.SteelBarrel:
                    AddChild(new Barrel { Position = pos, Type = obj.Type });
                    break;
                case ObjectDefs.Teleport:
                    var capsule = new Teleporter { Position = pos, Number = NumberOf(obj) };
                    _teleports.Add(capsule);
                    AddChild(capsule);
                    if (Agent != null)
                        MoveChild(capsule, Agent.GetIndex());   // the agent stands in front of it
                    break;
                case ObjectDefs.Elevator:
                    SpawnPlatform(obj, pos, vertical: true);
                    break;
                case ObjectDefs.MovingPlatform:
                    SpawnPlatform(obj, pos, vertical: false);
                    break;
                case ObjectDefs.ElectricDoor:
                    AddChild(new ElectricDoorField { Position = pos, Session = this });
                    break;
                case ObjectDefs.FlameTrap:
                    AddChild(new FlameTrap { Position = pos, Session = this });
                    break;
                case ObjectDefs.RocketLauncher:
                    AddChild(new RocketLauncher { Position = pos, Session = this });
                    break;
                case ObjectDefs.Light:
                    AddChild(new LevelLight { Position = pos });
                    break;
                default:
                    var enemyDef = EnemyDefs.Get(obj.Type);
                    if (enemyDef != null)
                    {
                        var enemy = new Enemy { Position = pos, Def = enemyDef, Session = this };
                        _enemies.Add(enemy);
                        AddChild(enemy);
                        break;
                    }
                    var decoDef = DecorationDefs.Get(obj.Type);
                    if (decoDef != null)
                    {
                        decorations.AddChild(new Decoration { Position = pos, Def = decoDef });
                        break;
                    }
                    var pickup = new Pickup { Position = pos, Source = obj, FromLevel = true };
                    pickup.Collected += OnPickup;
                    AddChild(pickup);
                    if (CollectibleDefs.Get(obj.Type) != null)
                        _collectibleTotal++;
                    break;
            }
        }

        MapFloorCircuits();

        for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                if (_level.GetTile(x, y) == TileDefs.Spikes)
                    _spikeCells.Add(new Vector2I(x, y));
        UpdateSpikes();
        for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                if (_level.GetTile(x, y) == TileDefs.Spear)
                    _spearCells.Add(new Vector2I(x, y));
        UpdateSpears();

        for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                if (_level.GetTile(x, y) == TileDefs.ElectricFloor)
                    _electricCells.Add(new Vector2I(x, y));
        _ambience = new TerrainAmbience { Session = this };
        _ambience.AddTimed(Sound.ElectricBuzz, _electricCells, cell => !IsFloorOff(cell), 0.3f, 0.7f);
        AddChild(_ambience);
        _grids.Spit.Spat += _ambience.OnLavaSpat;   // the grids outlive the session in the editor

        var canvas = new CanvasLayer();
        AddChild(canvas);
        var hudRow = new HBoxContainer
        {
            Position = new Vector2(4, 4),
            Theme = new Theme { DefaultFontSize = 10 },
        };
        hudRow.AddThemeConstantOverride("separation", 8);
        canvas.AddChild(hudRow);
        _hearts = new HeartRow();
        hudRow.AddChild(_hearts);
        _weaponIcon = new TextureRect { StretchMode = TextureRect.StretchModeEnum.KeepCentered };
        _weaponCount = new RollingLabel { VerticalAlignment = VerticalAlignment.Center };
        hudRow.AddChild(HudStat(_weaponIcon, _weaponCount));
        _scoreCount = new RollingLabel { VerticalAlignment = VerticalAlignment.Center };
        _scoreStat = HudStat(new TextureRect
        {
            Texture = Sprites.Get("score"),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        }, _scoreCount);
        hudRow.AddChild(_scoreStat);
        _coinCount = new RollingLabel { VerticalAlignment = VerticalAlignment.Center };
        _coinStat = HudStat(new TextureRect
        {
            Texture = Sprites.Get("coin"),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        }, _coinCount);
        hudRow.AddChild(_coinStat);
        _alarmCount = new Label { VerticalAlignment = VerticalAlignment.Center };
        _alarmStat = HudStat(new TextureRect
        {
            Texture = Sprites.Get("alarm"),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        }, _alarmCount);
        hudRow.AddChild(_alarmStat);
        _items = new HBoxContainer();
        _items.AddThemeConstantOverride("separation", 8);
        hudRow.AddChild(_items);

        // Centered, a little below the agent the camera keeps in the middle.
        _activatePrompt = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Theme = new Theme { DefaultFontSize = 10 },
            Visible = false,
        };
        _activatePrompt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _activatePrompt.OffsetTop = 72;
        _activatePrompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _activatePrompt.AddThemeConstantOverride("outline_size", 4);
        canvas.AddChild(_activatePrompt);
        UpdateHud();
    }

    private static HBoxContainer HudStat(TextureRect icon, Label count)
    {
        var stat = new HBoxContainer();
        stat.AddThemeConstantOverride("separation", 3);
        stat.AddChild(icon);
        stat.AddChild(count);
        return stat;
    }

    // Holding look_down slides the view a few tiles below the agent and holds
    // it there; letting go slides it back. The camera limits still stop it
    // at the level's bottom edge.
    private const float LookDownDistance = 3 * TileSetBuilder.TileSize;
    private const float LookDownSpeed = 320f;   // px per second

    private void UpdateLookDown(float dt)
    {
        float target = Input.IsActionPressed("look_down") && !Agent.IsDead ? LookDownDistance : 0f;
        _camera.Position = new Vector2(0, Mathf.MoveToward(_camera.Position.Y, target, LookDownSpeed * dt));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ended)
            return;
        float dt = (float)delta;

        // The level keeps running behind the death effect - only the agent is
        // down, and it cannot be hurt again until the respawn.
        if (_agentDown)
        {
            _deathTime -= dt;
            if (_deathTime <= 0f)
            {
                FinishDeath();
                if (_ended)
                    return;
            }
        }

        UpdateLookDown(dt);
        Time += dt;
        UpdateSpikes();
        UpdateSpears();

        if (_gogglesTime > 0)
            _gogglesTime -= dt;
        _grids.Hidden.Modulate = HiddenRevealed ? new Color(1f, 0.7f, 0.6f) : new Color(1, 1, 1, 0);
        _grids.Hidden.CollisionEnabled = HiddenRevealed;   // hidden platforms are only solid while revealed
        if (_disguiseTime > 0)
            _disguiseTime -= dt;

        if (_alarmTime > 0)
        {
            _alarmTime -= dt;
            _grids.Tint(new Color(1f, 0.72f, 0.72f));   // README: rooms change appearance during an alarm
            if (_alarmTime <= 0)
                _grids.Tint(Colors.White);
        }

        // Doors open on contact when the agent carries the matching key.
        for (int i = _doors.Count - 1; i >= 0; i--)
        {
            var door = _doors[i];
            var offset = (door.Position - Agent.Position).Abs();
            if (_keys.Contains(door.ColorName) && offset.X < 36 && offset.Y < 36)
            {
                door.Open();
                _doors.RemoveAt(i);
            }
        }

        // Entering a teleport capsule moves the agent to its paired capsule;
        // the lock prevents ping-ponging until the agent steps away.
        bool nearCapsule = false;
        foreach (var capsule in _teleports)
        {
            var offset = (capsule.Position - Agent.Position).Abs();
            if (offset.X >= 20 || offset.Y >= 26)
                continue;
            nearCapsule = true;
            if (_teleportLock)
                break;
            foreach (var partner in _teleports)
                if (partner != capsule && partner.Number == capsule.Number)
                {
                    Agent.Position = partner.Position;
                    capsule.Open();
                    partner.Open();
                    _teleportLock = true;
                    Sfx.Play(Sound.Teleport);
                    break;
                }
            break;
        }
        if (!nearCapsule)
            _teleportLock = false;

        // Computers hack automatically on proximity with the matching USB key.
        foreach (var computer in _computers)
        {
            if (computer.Hacked)
                continue;
            var offset = (computer.Position - Agent.Position).Abs();
            if (offset.X < 36 && offset.Y < 36
                && (_usbKeys.Contains(computer.Number) || Profile.Current.HasHackingTool))
            {
                computer.SetHacked();
                Sfx.Play(Sound.ComputerHack);
                TurnOffCircuit(computer.Number);
            }
        }

        UpdateHud();
        UpdateActivatePrompt();
    }

    // Spike cells show the frame for the clock; the editor redraws the
    // level when play stops, which puts the icon tile back.
    private void UpdateSpikes()
    {
        int raised = RaisedSpike;
        if (raised >= 0 && _raisedSpike < 0 && _spikeCoords.X >= 0)
            PlayTrap(Sound.SpikesUp, _spikeCells);
        _raisedSpike = raised;
        var coords = SpikeCoords;
        if (coords == _spikeCoords)
            return;
        _spikeCoords = coords;
        foreach (var cell in _spikeCells)
            _grids.Main.SetCell(cell, TileSetBuilder.SourceId, coords);
    }

    // Spear cells show the frame for the clock. The editor redraws the
    // level when play stops, which puts the fully-out tile back.
    private void UpdateSpears()
    {
        int frame = SpearFrame;
        if (frame == _spearFrame)
            return;
        if (_spearFrame >= 0 && frame >= 1)
            PlayTrap(frame == 1 ? Sound.SpearsTips : Sound.SpearsOut, _spearCells);
        _spearFrame = frame;
        foreach (var cell in _spearCells)
            _grids.Main.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.SpearCoordsFor(frame));
    }

    private void PlayTrap(Sound sound, List<Vector2I> cells)
    {
        if (Agent == null || _agentDown)
            return;
        if (TerrainAmbience.NearestWithin(cells, Agent.Position, HazardHearRange) is Vector2 at)
            Sfx.PlayAt(sound, at, HazardHearRange);
    }

    public override void _ExitTree()
    {
        if (_ambience != null)
            _grids.Spit.Spat -= _ambience.OnLavaSpat;
    }

    // Hidden platforms only exist while the goggles reveal them.
    public int TileAt(int x, int y)
    {
        int id = _destroyed.Contains(new Vector2I(x, y)) ? TileDefs.Empty : _level.GetTile(x, y);
        return id == TileDefs.HiddenPlatform && !HiddenRevealed ? TileDefs.Empty : id;
    }

    public int TileAtPosition(Vector2 position)
    {
        int tile = TileSetBuilder.TileSize;
        return TileAt((int)Mathf.Floor(position.X / tile), (int)Mathf.Floor(position.Y / tile));
    }

    public bool InLevel(Vector2I cell) => _level.InBounds(cell.X, cell.Y);

    public void DestroyTile(Vector2I cell)
    {
        int tile = TileSetBuilder.TileSize;
        var center = new Vector2(cell.X * tile + tile / 2f, cell.Y * tile + tile / 2f);
        Sfx.PlayAt(Sound.WallCrumble, center);
        AddChild(new WallDebris { Position = center, TileId = TileAt(cell.X, cell.Y) });
        _destroyed.Add(cell);
        _grids.EraseCell(cell);
    }

    // Returns true when the weapon actually went off, so the caller can play
    // the shoot animation only for a real shot.
    public bool TryShoot(Agent agent)
    {
        var def = WeaponDefs.Get(Weapon);
        switch (Weapon)
        {
            case Weapon.Chainsaw:
                // Melee sweep in front of the agent; no ammo, no alarm.
                foreach (var enemy in _enemies.ToArray())
                {
                    var to = enemy.Position - agent.Position;
                    if (Mathf.Abs(to.Y) < 28 && to.X * agent.Facing > 0 && Mathf.Abs(to.X) < 44)
                        enemy.TakeHit();
                }
                Sfx.Play(Sound.Chainsaw);
                return true;
            case Weapon.Crossbow:
                if (Arrows <= 0)
                    return Misfire();
                Arrows--;
                ShotsFired++;
                Sfx.Play(Sound.Crossbow);
                SpawnAgentBullet(agent, def.Projectile, 0);   // README: the crossbow does not trigger the alarm
                return true;
            case Weapon.Dynamite:
                if (DynamiteSticks <= 0)
                    return Misfire();
                DynamiteSticks--;
                SpawnDynamite(agent.Position);
                return true;
            default:
                // Pistol, rifle and mini gun feed on the shared bullet pool.
                int burst = Mathf.Min(def.Burst, Ammo);
                if (burst <= 0)
                    return Misfire();
                Ammo -= burst;
                ShotsFired += burst;
                TriggerAlarm();   // README: a gun shot triggers the alarm
                Sfx.Play(def.Shot.Value);
                for (int i = 0; i < burst; i++)
                    SpawnAgentBullet(agent, def.Projectile, (i - (burst - 1) / 2f) * 6f);
                return true;
        }
    }

    private static bool Misfire()
    {
        Sfx.Play(Sound.EmptyClick);
        return false;
    }

    private void SpawnAgentBullet(Agent agent, string spriteKey, float offsetY) =>
        AddChild(new Bullet
        {
            Session = this,
            SpriteKey = spriteKey,
            Direction = agent.Facing,
            Position = agent.Position + new Vector2(agent.Facing * 20, -2 + offsetY),
            FromX = agent.Position.X,
        });

    // README ammo table: rockets take a life and move at 1x agent speed.
    public void SpawnRocket(Vector2 from, int direction) =>
        AddChild(new EnemyBullet
        {
            Session = this,
            Position = from,
            Direction = new Vector2(direction, 0),
            Speed = Agent.RunSpeed,
            SpriteKey = "rocket",
        });

    public void TriggerAlarm()
    {
        if (_alarmTime <= 0)
            Sfx.Play(Sound.Alarm);
        _alarmTime = AlarmDuration;
    }

    public bool IsCircuitOff(string number) => _circuitsOff.Contains(number);

    public bool IsFloorOff(Vector2I cell) =>
        _floorCircuits.TryGetValue(cell, out var number) && IsCircuitOff(number);

    // README: computers and switches switch off "laser beam / floor". Lasers
    // and security doors match by circuit number; an electric floor goes off
    // only when its own region's circuit does.
    public void TurnOffCircuit(string number)
    {
        if (!_circuitsOff.Add(number))
            return;
        Sfx.Play(Sound.PowerDown);
        foreach (var door in _securityDoors)
            if (door.Number == number)
                door.Open();
        int tile = TileSetBuilder.TileSize;
        foreach (var (cell, circuit) in _floorCircuits)
            if (circuit == number)
                AddChild(new ColorRect
                {
                    Position = new Vector2(cell.X * tile, cell.Y * tile),
                    Size = new Vector2(tile, tile),
                    Color = new Color(0.1f, 0.1f, 0.15f, 0.55f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                });
    }

    // Touching platforms of the same type (sharing an edge) become one body so
    // they move together. The group spawns at its first cell in object order;
    // later cells of the group are skipped.
    private void SpawnPlatform(LevelObject obj, Vector2 pos, bool vertical)
    {
        var start = new Vector2I(obj.X, obj.Y);
        if (!_platformCells.Add(start))
            return;
        var same = new Dictionary<Vector2I, LevelObject>();
        foreach (var other in _level.Objects)
            if (other.Type == obj.Type)
                same[new Vector2I(other.X, other.Y)] = other;
        int tile = TileSetBuilder.TileSize;
        var cells = new List<PlatformCell>();
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var paint = ObjectDefs.ColorOf(obj.Type, same[cell].Properties.GetValueOrDefault("color"));
            cells.Add(new PlatformCell((Vector2)(cell - start) * tile, paint));
            foreach (var step in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
                if (same.ContainsKey(cell + step) && _platformCells.Add(cell + step))
                    queue.Enqueue(cell + step);
        }
        AddChild(new MovingPlatform { Position = pos, Session = this, Vertical = vertical, Cells = cells });
    }

    // Flood-fills the 4-connected electric floor region under each marker.
    // A marker off the floor maps nothing, and a cell already claimed by an
    // earlier marker keeps that marker's circuit.
    private void MapFloorCircuits()
    {
        foreach (var marker in _level.Objects)
        {
            if (marker.Type != ObjectDefs.FloorCircuit)
                continue;
            string number = NumberOf(marker);
            var pending = new Stack<Vector2I>();
            pending.Push(new Vector2I(marker.X, marker.Y));
            while (pending.Count > 0)
            {
                var cell = pending.Pop();
                if (_level.GetTile(cell.X, cell.Y) != TileDefs.ElectricFloor || !_floorCircuits.TryAdd(cell, number))
                    continue;
                pending.Push(cell + Vector2I.Left);
                pending.Push(cell + Vector2I.Right);
                pending.Push(cell + Vector2I.Up);
                pending.Push(cell + Vector2I.Down);
            }
        }
    }

    private enum Activation { None, LeaveCloset, Hide, UseItem }

    // What the activate action does right now, by priority. The key press and
    // the on-screen prompt both read this, so the prompt never promises
    // something the key would not do.
    private (Activation Kind, Node2D Target) CurrentActivation()
    {
        if (Agent == null || _agentDown)
            return (Activation.None, null);
        if (Agent.IsHiding)
            return (Activation.LeaveCloset, null);
        foreach (var closet in _closets)
            if (WithinReach(closet))
                return (Activation.Hide, closet);
        if (HeldManualItem() != null)
            return (Activation.UseItem, null);
        return (Activation.None, null);
    }

    private bool WithinReach(Node2D node)
    {
        var offset = (node.Position - Agent.Position).Abs();
        return offset.X < 24 && offset.Y < 24;
    }

    public void ActivateAction()
    {
        var (kind, target) = CurrentActivation();
        switch (kind)
        {
            case Activation.LeaveCloset:
                Agent.SetHiding(false);
                Sfx.Play(Sound.Unhide);
                break;
            case Activation.Hide:
                Agent.SetHiding(true);
                Sfx.Play(Sound.Hide);
                Agent.Position = target.Position;
                break;
            case Activation.UseItem:
                ActivateHeldItem();
                break;
        }
    }

    // Only a shot flips a switch; the activate key does not.
    public void FlipSwitch(WallSwitch wallSwitch)
    {
        wallSwitch.Activate();
        TurnOffCircuit(wallSwitch.Number);
    }

    // Held items already show their key in the HUD, so the centered prompt
    // only covers things the agent is standing next to.
    private void UpdateActivatePrompt()
    {
        string verb = CurrentActivation().Kind switch
        {
            Activation.LeaveCloset => "leave the closet",
            Activation.Hide => "hide",
            _ => null,
        };
        _activatePrompt.Visible = verb != null;
        if (verb != null)
            _activatePrompt.Text = $"Press {_activateKey} to {verb}";
    }

    public bool LineClear(Vector2 from, Vector2 to)
    {
        float distance = from.DistanceTo(to);
        int steps = Mathf.CeilToInt(distance / 16f);
        for (int i = 1; i < steps; i++)
            if (TileDefs.Get(TileAtPosition(from.Lerp(to, i / (float)steps)))?.Solid == true)
                return false;
        return true;
    }

    public void OnEnemyDied(Enemy enemy) => _enemies.Remove(enemy);

    public void RegisterKill() => Kills++;

    public void RegisterHit() => ShotsHit++;

    public void SpawnEnemyBullet(Vector2 from, Vector2 direction, string spriteKey) =>
        AddChild(new EnemyBullet { Session = this, Position = from, Direction = direction, SpriteKey = spriteKey });

    public void SpawnDynamite(Vector2 from)
    {
        Sfx.PlayAt(Sound.DynamiteThrow, from);
        AddChild(new Dynamite { Session = this, Position = from });
    }

    // Editor playtest cheat: every ammo pool full.
    public const int FullAmmo = 99;

    public void RefillAmmo()
    {
        Ammo = FullAmmo;
        Arrows = FullAmmo;
        DynamiteSticks = FullAmmo;
        Sfx.Play(Sound.PickupAmmo);
    }

    public void SpawnAmmoPack(Vector2 at)
    {
        var pickup = new Pickup
        {
            Position = at,
            Source = new LevelObject { Type = ObjectDefs.AmmoPack },
        };
        pickup.Collected += OnPickup;
        AddChild(pickup);
    }

    // README: tombstones are collectibles worth 1k score.
    // The stone rests on the floor below the body, even if the soldier died
    // mid-fall: it takes the bottom of the first cell above solid ground.
    public void SpawnTombstone(Vector2 at)
    {
        int tile = TileSetBuilder.TileSize;
        int x = (int)Mathf.Floor(at.X / tile);
        int y = (int)Mathf.Floor(at.Y / tile);
        while (y < _level.Height - 1 && TileDefs.Get(TileAt(x, y + 1))?.Solid != true)
            y++;
        var pickup = new Pickup
        {
            Position = new Vector2(at.X, y * tile + tile / 2f),
            Source = new LevelObject { Type = CollectibleDefs.Tombstone },
        };
        pickup.Collected += OnPickup;
        AddChild(pickup);
    }

    // Explosions kill outright: no degradation, no drops. Enemies with
    // infinite lives survive (README: does not apply to infinite lives).
    public void Explode(Vector2 center)
    {
        Sfx.PlayAt(Sound.Explosion, center);
        TriggerAlarm();
        int tile = TileSetBuilder.TileSize;
        int minX = (int)Mathf.Floor((center.X - ExplosionRadius) / tile);
        int maxX = (int)Mathf.Floor((center.X + ExplosionRadius) / tile);
        int minY = (int)Mathf.Floor((center.Y - ExplosionRadius) / tile);
        int maxY = (int)Mathf.Floor((center.Y + ExplosionRadius) / tile);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var cellCenter = new Vector2(x * tile + tile / 2f, y * tile + tile / 2f);
                if (TileDefs.IsDestructible(TileAt(x, y)) && center.DistanceTo(cellCenter) <= ExplosionRadius)
                    DestroyTile(new Vector2I(x, y));
            }
        foreach (var enemy in _enemies.ToArray())
            if (!enemy.Def.InfiniteLives && center.DistanceTo(enemy.Position) <= ExplosionRadius)
            {
                _enemies.Remove(enemy);
                enemy.QueueFree();
                Kills++;
            }
        if (Agent != null && center.DistanceTo(Agent.Position) <= ExplosionRadius)
            AgentHit();

        AddChild(new Explosion { Position = center, Radius = ExplosionRadius, Camera = _camera });
    }

    // Goggles affect platforming, so they win when both items are held.
    private string HeldManualItem() =>
        _heldItems.Contains(ObjectDefs.InfraGoggles) ? ObjectDefs.InfraGoggles
        : _heldItems.Contains(ObjectDefs.Briefcase) ? ObjectDefs.Briefcase
        : null;

    private void ActivateHeldItem()
    {
        string item = HeldManualItem();
        if (item == null)
            return;
        _heldItems.Remove(item);
        if (item == ObjectDefs.InfraGoggles)
        {
            _gogglesTime = ItemDuration;
            Sfx.Play(Sound.GogglesOn);
        }
        else
        {
            _disguiseTime = ItemDuration;
            Sfx.Play(Sound.DisguiseOn);
        }
    }

    // Editor playtest toggle: hits do nothing and falling out of the level
    // puts the agent back at its respawn point.
    public bool Invincible { get; set; }

    public void AgentFell()
    {
        if (Invincible)
            Agent.ResetForRespawn(_respawnPoint);
        else
            AgentHit();
    }

    public void AgentHit()
    {
        if (_ended || _agentDown || Invincible)   // one hit per death; the body is immune
            return;
        if (HasVest)
        {
            // README: the vest saves 1 life, then is destroyed. Surviving a
            // hit is not a death, so it costs the agent its ground, not the
            // whole death sequence.
            HasVest = false;
            Sfx.Play(Sound.VestBreak);
            Agent.ResetForRespawn(_respawnPoint);
            return;
        }

        // The life is gone the moment the hit lands; only the respawn waits
        // for the death sequence to play out.
        Lives--;
        ShowChange(Sprites.Get("heart"), -1, LossColor);
        _agentDown = true;
        _failAfterDeath = Lives <= 0;
        Sfx.Play(_failAfterDeath ? Sound.Death : Sound.Hurt);
        _deathTime = DeathDuration;
        Agent.Die();
        _death = new DeathEffect { Duration = DeathDuration, Camera = _camera };
        AddChild(_death);
    }

    private void OnCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == _checkpoint || _agentDown)
            return;
        if (_checkpoint != null)
            _checkpoint.Lit = false;
        _checkpoint = checkpoint;
        checkpoint.Lit = true;
        Sfx.Play(Sound.Checkpoint);
        _respawnPoint = checkpoint.Position;
    }

    // Editor playtest shortcut: moves the agent to the next (step 1) or
    // previous (step -1) stop on the route - the agent start, then each
    // checkpoint in level object order, wrapping around - and makes that stop
    // the respawn point, as if the agent had walked there. Returns the stop's
    // name for the status line, or null when the agent cannot move now.
    public string JumpCheckpoint(int step)
    {
        if (_ended || _agentDown || Agent == null)
            return null;
        int stops = _checkpoints.Count + 1;   // stop 0 is the agent start
        int current = _checkpoint == null ? 0 : _checkpoints.IndexOf(_checkpoint) + 1;
        int next = ((current + step) % stops + stops) % stops;
        if (next == 0)
        {
            if (_checkpoint != null)
                _checkpoint.Lit = false;
            _checkpoint = null;
            _respawnPoint = _startPoint;
        }
        else
            OnCheckpoint(_checkpoints[next - 1]);
        Agent.ResetForRespawn(_respawnPoint);
        return next == 0 ? "the agent start" : $"checkpoint {next} of {_checkpoints.Count}";
    }

    private void FinishDeath()
    {
        _agentDown = false;
        if (IsInstanceValid(_death))
            _death.QueueFree();
        _death = null;
        if (_failAfterDeath)
        {
            End(SignalName.Failed);
            return;
        }
        Agent.ResetForRespawn(_respawnPoint);
    }

    private void End(StringName signal)
    {
        if (_ended)
            return;
        _ended = true;
        Sfx.Play(signal == SignalName.Completed ? Sound.LevelComplete : Sound.MissionFailed);
        Agent?.SetPhysicsProcess(false);
        foreach (var enemy in _enemies)
            enemy.SetPhysicsProcess(false);
        EmitSignal(signal);
    }

    private void OnPickup(Pickup pickup)
    {
        switch (pickup.Source.Type)
        {
            case ObjectDefs.DoorKey:
                _keys.Add(ColorOf(pickup.Source));
                Sfx.Play(Sound.PickupKey);
                break;
            case ObjectDefs.HealthKit:
                Lives++;
                ShowChange(Sprites.Get("heart"), 1, LifeColor);
                Sfx.Play(Sound.PickupHealth);
                break;
            case ObjectDefs.AmmoPack:
                Ammo += 10;
                ShowChange(Sprites.Trimmed(ObjectDefs.AmmoPack), 10, AmmoColor);
                Sfx.Play(Sound.PickupAmmo);
                break;
            case ObjectDefs.Vest:
                HasVest = true;
                Sfx.Play(Sound.PickupVest);
                break;
            case ObjectDefs.Briefcase:
            case ObjectDefs.InfraGoggles:
                _heldItems.Add(pickup.Source.Type);
                Sfx.Play(Sound.PickupItem);
                break;
            case ObjectDefs.UsbKey:
                _usbKeys.Add(NumberOf(pickup.Source));
                Sfx.Play(Sound.PickupKey);
                break;
            case var type when WeaponDefs.ForPickup(type) is { } weapon:
                // Weapon pickups equip the weapon and grant its README default ammo.
                Weapon = weapon.Kind;
                int granted = weapon.Kind switch
                {
                    Weapon.AssaultRifle => 10,
                    Weapon.Minigun or Weapon.Crossbow => 20,
                    Weapon.Dynamite => 3,
                    _ => 0,
                };
                switch (weapon.Kind)
                {
                    case Weapon.Crossbow: Arrows += granted; break;
                    case Weapon.Dynamite: DynamiteSticks += granted; break;
                    default: Ammo += granted; break;
                }
                if (granted > 0)
                    ShowChange(Sprites.Trimmed(weapon.Icon), granted, AmmoColor);
                Sfx.Play(Sound.PickupWeapon);
                break;
            default:
                var collectible = CollectibleDefs.Get(pickup.Source.Type);
                if (collectible != null)
                {
                    var jingle = Sound.Coin;
                    Score += collectible.Score;
                    ShowChange(Sprites.Get("score"), collectible.Score, ScoreColor);
                    CoinsCollected += collectible.Coins;
                    if (pickup.FromLevel)
                        _collectibleGot++;
                    if (CollectibleDefs.IsLetter(pickup.Source.Type))
                    {
                        _letters.Add(pickup.Source.Type);
                        if (_letters.Count == CollectibleDefs.LetterTypes.Length)
                        {
                            Score += CollectibleDefs.LetterSetBonusScore;
                            ShowChange(Sprites.Get("score"), CollectibleDefs.LetterSetBonusScore, ScoreColor);
                            CoinsCollected += CollectibleDefs.LetterSetBonusCoins;
                            jingle = Sound.LetterBonus;
                        }
                    }
                    Sfx.Play(jingle);
                }
                break;
        }
        pickup.QueueFree();
        UpdateHud();
    }

    private static readonly Color ScoreColor = new(1f, 0.85f, 0.3f);
    private static readonly Color AmmoColor = new(0.95f, 0.95f, 0.85f);
    private static readonly Color LifeColor = new(0.5f, 1f, 0.5f);
    private static readonly Color LossColor = new(1f, 0.35f, 0.3f);

    // A number rises off the agent's head for each change to score, ammo or
    // lives; the ones already up step aside for it.
    private void ShowChange(Texture2D icon, int amount, Color color)
    {
        if (Agent == null)
            return;
        foreach (var child in GetChildren())
            if (child is FloatingNumber older)
                older.MakeRoom();
        AddChild(new FloatingNumber
        {
            Follow = Agent,
            Position = Agent.Position,
            Icon = icon,
            Amount = amount,
            Color = color,
        });
    }

    private static string ColorOf(LevelObject obj) =>
        obj.Properties.TryGetValue("color", out var color) ? color : ObjectDefs.KeyColors[0];

    private static string NumberOf(LevelObject obj) =>
        obj.Properties.TryGetValue("number", out var number) ? number : ObjectDefs.Numbers[0];

    private void UpdateHud()
    {
        int? weaponCount = Weapon switch
        {
            Weapon.Chainsaw => null,
            Weapon.Crossbow => Arrows,
            Weapon.Dynamite => DynamiteSticks,
            _ => Ammo,
        };
        string weaponSprite = WeaponDefs.Get(Weapon).Icon;
        _weaponIcon.Texture = Sprites.Trimmed(weaponSprite);
        _weaponCount.Visible = weaponCount != null;
        if (weaponSprite != _hudWeapon)
            _weaponCount.Snap(weaponCount ?? 0);   // another pool: no roll up from the old one
        else
            _weaponCount.Value = weaponCount ?? 0;
        _hudWeapon = weaponSprite;
        _scoreStat.Visible = Score > 0;
        _scoreCount.Value = Score;
        _coinStat.Visible = CoinsCollected > 0;
        _coinCount.Value = CoinsCollected;
        _alarmStat.Visible = AlarmActive;
        _alarmCount.Text = $"{Mathf.CeilToInt(_alarmTime)}s";
        _hearts.Count = Mathf.Max(Lives, 0);

        var items = new List<(Texture2D Icon, string Text, Color? Mark)>();
        foreach (var color in _keys)
            items.Add((Sprites.Trimmed(ObjectDefs.DoorKey, color), null, null));
        foreach (var number in _usbKeys)
            items.Add((Sprites.Trimmed(ObjectDefs.UsbKey), null, ObjectDefs.CircuitColor(number)));
        if (Agent != null && Agent.IsHiding)
            items.Add((null, "Hidden", null));
        if (HasVest)
            items.Add((Sprites.Trimmed(ObjectDefs.Vest), null, null));
        string manual = HeldManualItem();
        foreach (var item in new[] { ObjectDefs.InfraGoggles, ObjectDefs.Briefcase })
        {
            int count = _heldItems.FindAll(held => held == item).Count;
            if (count == 0)
                continue;
            string text = ((count > 1 ? $"x{count} " : "") + (item == manual ? $"(press {_activateKey})" : "")).Trim();
            items.Add((Sprites.Trimmed(item), text == "" ? null : text, null));
        }
        if (GogglesActive)
            items.Add((Sprites.Trimmed(ObjectDefs.InfraGoggles), $"{Mathf.CeilToInt(_gogglesTime)}s", null));
        if (_disguiseTime > 0)
            items.Add((Sprites.Trimmed(ObjectDefs.Briefcase), $"{Mathf.CeilToInt(_disguiseTime)}s", null));
        ShowItems(items);
    }

    // Rebuilds the held items row only when what it shows changes, since the
    // HUD updates every frame.
    private void ShowItems(List<(Texture2D Icon, string Text, Color? Mark)> items)
    {
        string shown = string.Join("|", items.Select(i => $"{i.Icon?.GetInstanceId()}/{i.Text}/{i.Mark}"));
        if (shown == _itemsShown)
            return;
        _itemsShown = shown;
        foreach (var child in _items.GetChildren())
        {
            _items.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var (icon, text, mark) in items)
        {
            var label = text == null ? null : new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
            if (icon == null)
            {
                _items.AddChild(label);
                continue;
            }
            var rect = new TextureRect
            {
                Texture = icon,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            if (mark is Color color)   // the USB key's circuit color, as on the pickup
                rect.AddChild(new ColorRect
                {
                    Color = color,
                    Position = new Vector2(0, icon.GetHeight() + 1),
                    Size = new Vector2(icon.GetWidth(), 2),
                });
            if (label == null)
                _items.AddChild(rect);
            else
                _items.AddChild(HudStat(rect, label));
        }
    }
}
