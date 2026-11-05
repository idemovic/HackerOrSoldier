using System.Collections.Generic;
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
    private const float SpearStateTime = 3f;   // spears: holes, tips, out - 3s each
    private const float ItemDuration = 30f; // README: briefcase and goggles last 30s
    private const float RobotCycle = 10f;   // README: robots stop and turn off every 10s
    private const float RobotOffTime = 1f;
    private const float AlarmDuration = 30f;         // README: alarm stays on 30s from the last trigger
    private const float ExplosionRadius = 5 * 32f;   // README: dynamite kills in a 5 square radius
    // How long a lost life holds the screen: the agent crumples, the death
    // effect dissolves the screen to black, and only then does the respawn
    // (or the fail screen) happen behind it.
    private const float DeathDuration = 1.2f;

    public Agent Agent { get; private set; }
    public int Lives { get; private set; } = 3;   // README: default 3 lives
    public int Ammo { get; private set; } = 10;   // README: pistol, default 10 ammo
    public string Weapon { get; private set; } = "pistol";
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
    private Checkpoint _checkpoint;   // the lit one, null until the first is touched
    private Camera2D _camera;
    private readonly List<Vector2I> _spikeCells = new();
    private Vector2I _spikeCoords = new(-1, -1);
    private readonly List<Vector2I> _spearCells = new();
    private int _spearFrame = -1;
    private HBoxContainer _hearts;
    private TextureRect _weaponIcon;
    private Label _weaponCount;
    private HBoxContainer _scoreStat;
    private Label _scoreCount;
    private Label _hud;
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

        foreach (var obj in _level.Objects)
        {
            var pos = new Vector2(obj.X * tile + tile / 2f, obj.Y * tile + tile / 2f);
            switch (obj.Type)
            {
                case ObjectDefs.AgentStart:
                    _respawnPoint = pos;
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
                    AddChild(new SecurityCamera { Position = pos, Session = this });
                    break;
                case ObjectDefs.Closet:
                    var closet = new Closet { Position = pos };
                    _closets.Add(closet);
                    AddChild(closet);
                    if (Agent != null)
                        MoveChild(closet, Agent.GetIndex());   // draw behind the agent
                    break;
                case ObjectDefs.Barrel:
                    AddChild(new Barrel { Position = pos });
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
                        AddChild(new Decoration { Position = pos, Def = decoDef });
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

        var canvas = new CanvasLayer();
        AddChild(canvas);
        var hudRow = new HBoxContainer
        {
            Position = new Vector2(4, 4),
            Theme = new Theme { DefaultFontSize = 10 },
        };
        hudRow.AddThemeConstantOverride("separation", 8);
        canvas.AddChild(hudRow);
        _hearts = new HBoxContainer();
        hudRow.AddChild(_hearts);
        _weaponIcon = new TextureRect { StretchMode = TextureRect.StretchModeEnum.KeepCentered };
        _weaponCount = new Label { VerticalAlignment = VerticalAlignment.Center };
        hudRow.AddChild(HudStat(_weaponIcon, _weaponCount));
        _scoreCount = new Label { VerticalAlignment = VerticalAlignment.Center };
        _scoreStat = HudStat(new TextureRect
        {
            Texture = Sprites.Get("score"),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        }, _scoreCount);
        hudRow.AddChild(_scoreStat);
        _hud = new Label { VerticalAlignment = VerticalAlignment.Center };
        hudRow.AddChild(_hud);
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
            _grids.Main.Modulate = new Color(1f, 0.72f, 0.72f);   // README: rooms change appearance during an alarm
            if (_alarmTime <= 0)
                _grids.Main.Modulate = Colors.White;
        }

        // Doors open on contact when the agent carries the matching key.
        for (int i = _doors.Count - 1; i >= 0; i--)
        {
            var door = _doors[i];
            var offset = (door.Position - Agent.Position).Abs();
            if (_keys.Contains(door.ColorName) && offset.X < 36 && offset.Y < 36)
            {
                door.QueueFree();
                _doors.RemoveAt(i);
                Sfx.PlayAt(Sound.DoorOpen, door.Position);
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
    }

    // Spike cells show the frame for the clock; the editor redraws the
    // level when play stops, which puts the icon tile back.
    private void UpdateSpikes()
    {
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
        _spearFrame = frame;
        foreach (var cell in _spearCells)
            _grids.Main.SetCell(cell, TileSetBuilder.SourceId, TileSetBuilder.SpearCoordsFor(frame));
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
        Sfx.PlayAt(Sound.WallCrumble, new Vector2(cell.X * tile + tile / 2f, cell.Y * tile + tile / 2f));
        _destroyed.Add(cell);
        _grids.EraseCell(cell);
    }

    // Returns true when the weapon actually went off, so the caller can play
    // the shoot animation only for a real shot.
    public bool TryShoot(Agent agent)
    {
        switch (Weapon)
        {
            case "chainsaw":
                // Melee sweep in front of the agent; no ammo, no alarm.
                foreach (var enemy in _enemies.ToArray())
                {
                    var to = enemy.Position - agent.Position;
                    if (Mathf.Abs(to.Y) < 28 && to.X * agent.Facing > 0 && Mathf.Abs(to.X) < 44)
                        enemy.TakeHit();
                }
                Sfx.Play(Sound.Chainsaw);
                return true;
            case "crossbow":
                if (Arrows <= 0)
                    return Misfire();
                Arrows--;
                ShotsFired++;
                Sfx.Play(Sound.Crossbow);
                SpawnAgentBullet(agent, 0);   // README: the crossbow does not trigger the alarm
                return true;
            case "dynamite":
                if (DynamiteSticks <= 0)
                    return Misfire();
                DynamiteSticks--;
                SpawnDynamite(agent.Position);
                return true;
            default:
                // README: pistol 1 bullet, mini gun 2, assault rifle 3 per shot,
                // all feeding on the shared bullet pool.
                int burst = Weapon == "assault_rifle" ? 3 : Weapon == "minigun" ? 2 : 1;
                burst = Mathf.Min(burst, Ammo);
                if (burst <= 0)
                    return Misfire();
                Ammo -= burst;
                ShotsFired += burst;
                TriggerAlarm();   // README: a gun shot triggers the alarm
                Sfx.Play(Weapon switch
                {
                    "assault_rifle" => Sound.ShotRifle,
                    "minigun" => Sound.ShotMinigun,
                    _ => Sound.ShotPistol,
                });
                for (int i = 0; i < burst; i++)
                    SpawnAgentBullet(agent, (i - (burst - 1) / 2f) * 6f);
                return true;
        }
    }

    private static bool Misfire()
    {
        Sfx.Play(Sound.EmptyClick);
        return false;
    }

    private void SpawnAgentBullet(Agent agent, float offsetY) =>
        AddChild(new Bullet
        {
            Session = this,
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

    // The activate action, by priority: leave a closet, hide in a nearby
    // closet, otherwise use a held manual item.
    public void ActivateAction()
    {
        if (Agent.IsHiding)
        {
            Agent.SetHiding(false);
            Sfx.Play(Sound.Unhide);
            return;
        }
        foreach (var closet in _closets)
        {
            var offset = (closet.Position - Agent.Position).Abs();
            if (offset.X < 24 && offset.Y < 24)
            {
                Agent.SetHiding(true);
                Sfx.Play(Sound.Hide);
                Agent.Position = closet.Position;
                return;
            }
        }
        ActivateHeldItem();
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
    public void SpawnTombstone(Vector2 at)
    {
        var pickup = new Pickup
        {
            Position = at,
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
                if (TileAt(x, y) == TileDefs.DestructibleWall && center.DistanceTo(cellCenter) <= ExplosionRadius)
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

    public void ActivateHeldItem()
    {
        // Goggles affect platforming, so they win when both items are held.
        string item = _heldItems.Contains(ObjectDefs.InfraGoggles) ? ObjectDefs.InfraGoggles
            : _heldItems.Contains(ObjectDefs.Briefcase) ? ObjectDefs.Briefcase
            : null;
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
                Sfx.Play(Sound.PickupHealth);
                break;
            case ObjectDefs.AmmoPack:
                Ammo += 10;
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
            // Weapon pickups equip the weapon and grant its README default ammo.
            case ObjectDefs.WeaponChainsaw:
                Weapon = "chainsaw";
                Sfx.Play(Sound.PickupWeapon);
                break;
            case ObjectDefs.WeaponAssaultRifle:
                Weapon = "assault_rifle";
                Ammo += 10;
                Sfx.Play(Sound.PickupWeapon);
                break;
            case ObjectDefs.WeaponMinigun:
                Weapon = "minigun";
                Ammo += 20;
                Sfx.Play(Sound.PickupWeapon);
                break;
            case ObjectDefs.WeaponCrossbow:
                Weapon = "crossbow";
                Arrows += 20;
                Sfx.Play(Sound.PickupWeapon);
                break;
            case ObjectDefs.WeaponDynamite:
                Weapon = "dynamite";
                DynamiteSticks += 3;
                Sfx.Play(Sound.PickupWeapon);
                break;
            default:
                var collectible = CollectibleDefs.Get(pickup.Source.Type);
                if (collectible != null)
                {
                    var jingle = Sound.Coin;
                    Score += collectible.Score;
                    CoinsCollected += collectible.Coins;
                    if (pickup.FromLevel)
                        _collectibleGot++;
                    if (CollectibleDefs.IsLetter(pickup.Source.Type))
                    {
                        _letters.Add(pickup.Source.Type);
                        if (_letters.Count == CollectibleDefs.LetterTypes.Length)
                        {
                            Score += CollectibleDefs.LetterSetBonusScore;
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

    private static string ColorOf(LevelObject obj) =>
        obj.Properties.TryGetValue("color", out var color) ? color : ObjectDefs.KeyColors[0];

    private static string NumberOf(LevelObject obj) =>
        obj.Properties.TryGetValue("number", out var number) ? number : ObjectDefs.Numbers[0];

    private void UpdateHud()
    {
        var (weaponSprite, weaponCount) = Weapon switch
        {
            "chainsaw" => (ObjectDefs.WeaponChainsaw, (int?)null),
            "crossbow" => (ObjectDefs.WeaponCrossbow, Arrows),
            "dynamite" => (ObjectDefs.WeaponDynamite, DynamiteSticks),
            "assault_rifle" => (ObjectDefs.WeaponAssaultRifle, Ammo),
            "minigun" => (ObjectDefs.WeaponMinigun, Ammo),
            _ => ("pistol", Ammo),
        };
        _weaponIcon.Texture = Sprites.Trimmed(weaponSprite);
        _weaponCount.Visible = weaponCount != null;
        _weaponCount.Text = weaponCount.ToString();
        _scoreStat.Visible = Score > 0;
        _scoreCount.Text = Score.ToString();

        int hearts = Mathf.Max(Lives, 0);
        while (_hearts.GetChildCount() > hearts)
        {
            var heart = _hearts.GetChild(_hearts.GetChildCount() - 1);
            _hearts.RemoveChild(heart);
            heart.QueueFree();
        }
        while (_hearts.GetChildCount() < hearts)
            _hearts.AddChild(new TextureRect
            {
                Texture = Sprites.Get("heart"),
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            });

        var parts = new List<string>();
        if (_keys.Count > 0)
            parts.Add("Keys: " + string.Join(" ", _keys));
        if (_usbKeys.Count > 0)
            parts.Add("USB: " + string.Join(" ", _usbKeys));
        if (Agent != null && Agent.IsHiding)
            parts.Add("Hidden (C to leave)");
        if (CoinsCollected > 0)
            parts.Add($"Coins +{CoinsCollected}");
        if (HasVest)
            parts.Add("Vest");
        foreach (var item in _heldItems)
            parts.Add(item == ObjectDefs.InfraGoggles ? "Goggles (press C)" : "Briefcase (press C)");
        if (GogglesActive)
            parts.Add($"Infra {Mathf.CeilToInt(_gogglesTime)}s");
        if (_disguiseTime > 0)
            parts.Add($"Disguise {Mathf.CeilToInt(_disguiseTime)}s");
        if (AlarmActive)
            parts.Add($"ALARM {Mathf.CeilToInt(_alarmTime)}s");
        _hud.Text = string.Join("  ", parts);
    }
}
