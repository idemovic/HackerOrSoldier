using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Owns one play-through: the agent, lives, ammo, inventory, the hazard clock,
// the HUD and every node spawned from the level objects. Returning to edit
// mode frees the whole session, which restores the level to its pre-play
// state. Losing a life respawns at the agent start but keeps level state
// (opened doors stay open, picked items stay picked).
public partial class PlaySession : Node2D
{
    [Signal]
    public delegate void CompletedEventHandler();

    [Signal]
    public delegate void FailedEventHandler();

    private const float SpikeCycle = 10f;   // README: spike trap cycles every 10s
    private const float SpikeUpTime = 2f;   // raised during the last 2s of each cycle
    private const float ItemDuration = 30f; // README: briefcase and goggles last 30s
    private const float RobotCycle = 10f;   // README: robots stop and turn off every 10s
    private const float RobotOffTime = 1f;
    private const float AlarmDuration = 30f;         // README: alarm stays on 30s from the last trigger
    private const float ExplosionRadius = 5 * 32f;   // README: dynamite kills in a 5 square radius

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
    public float Time { get; set; }               // settable so tests can skip ahead
    public bool SpikesUp => Time % SpikeCycle >= SpikeCycle - SpikeUpTime;
    public bool RobotsOff => Time % RobotCycle >= RobotCycle - RobotOffTime;
    public bool AlarmActive => _alarmTime > 0;
    public bool DisguiseActive => _disguiseTime > 0;
    public bool ElectricFloorsOff { get; private set; }
    public float DropChance { get; set; } = 0.3f;   // README: some enemies "sometimes" drop an ammo pack
    public System.Collections.Generic.IReadOnlyList<Enemy> Enemies => _enemies;
    public System.Collections.Generic.IReadOnlyList<WallSwitch> Switches => _switches;
    public float LevelBottom => _level.Height * TileSetBuilder.TileSize;
    public float LevelRight => _level.Width * TileSetBuilder.TileSize;

    private LevelData _level;
    private TileMapLayer _grid;
    private TileMapLayer _hiddenGrid;
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
    private bool _teleportLock;
    private readonly HashSet<string> _usbKeys = new();
    private readonly HashSet<string> _circuitsOff = new();
    private float _alarmTime;
    private float _gogglesTime;
    private float _disguiseTime;
    private Vector2 _startPosition;
    private Node2D _spikes;
    private Label _hud;
    private bool _ended;

    public void Setup(LevelData level, TileMapLayer grid, TileMapLayer hiddenGrid, PackedScene agentScene, RunState run = null)
    {
        _level = level;
        _grid = grid;
        _hiddenGrid = hiddenGrid;
        _agentScene = agentScene;
        _run = run;
    }

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        _hiddenGrid.Modulate = new Color(1, 1, 1, 0);

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
                    _startPosition = pos;
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
                    break;
                case ObjectDefs.ExitDoor:
                    var exit = new ExitDoor { Position = pos };
                    exit.Reached += () => End(SignalName.Completed);
                    AddChild(exit);
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
                    break;
                case ObjectDefs.Barrel:
                    AddChild(new Barrel { Position = pos });
                    break;
                case ObjectDefs.Teleport:
                    var capsule = new Teleporter { Position = pos, Number = NumberOf(obj) };
                    _teleports.Add(capsule);
                    AddChild(capsule);
                    break;
                case ObjectDefs.Elevator:
                    AddChild(new MovingPlatform { Position = pos, Session = this, Vertical = true });
                    break;
                case ObjectDefs.MovingPlatform:
                    AddChild(new MovingPlatform { Position = pos, Session = this, Vertical = false });
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

        _spikes = new Node2D { Visible = false };
        AddChild(_spikes);
        for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                if (_level.GetTile(x, y) == TileDefs.Spikes)
                    _spikes.AddChild(new ColorRect
                    {
                        Position = new Vector2(x * tile + 6, y * tile + 6),
                        Size = new Vector2(tile - 12, tile - 6),
                        Color = new Color(0.95f, 0.95f, 1f),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    });

        var canvas = new CanvasLayer();
        AddChild(canvas);
        _hud = new Label
        {
            Position = new Vector2(4, 34),
            Theme = new Theme { DefaultFontSize = 10 },
        };
        canvas.AddChild(_hud);
        UpdateHud();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ended)
            return;
        float dt = (float)delta;
        Time += dt;
        _spikes.Visible = SpikesUp;

        if (_gogglesTime > 0)
            _gogglesTime -= dt;
        _hiddenGrid.Modulate = GogglesActive ? new Color(1f, 0.7f, 0.6f) : new Color(1, 1, 1, 0);
        if (_disguiseTime > 0)
            _disguiseTime -= dt;

        if (_alarmTime > 0)
        {
            _alarmTime -= dt;
            _grid.Modulate = new Color(1f, 0.72f, 0.72f);   // README: rooms change appearance during an alarm
            if (_alarmTime <= 0)
                _grid.Modulate = Colors.White;
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
                    _teleportLock = true;
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
                TurnOffCircuit(computer.Number);
            }
        }

        UpdateHud();
    }

    public int TileAt(int x, int y) =>
        _destroyed.Contains(new Vector2I(x, y)) ? TileDefs.Empty : _level.GetTile(x, y);

    public int TileAtPosition(Vector2 position)
    {
        int tile = TileSetBuilder.TileSize;
        return TileAt((int)Mathf.Floor(position.X / tile), (int)Mathf.Floor(position.Y / tile));
    }

    public bool InLevel(Vector2I cell) => _level.InBounds(cell.X, cell.Y);

    public void DestroyTile(Vector2I cell)
    {
        _destroyed.Add(cell);
        _grid.EraseCell(cell);
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
                return true;
            case "crossbow":
                if (Arrows <= 0)
                    return false;
                Arrows--;
                ShotsFired++;
                SpawnAgentBullet(agent, 0);   // README: the crossbow does not trigger the alarm
                return true;
            case "dynamite":
                if (DynamiteSticks <= 0)
                    return false;
                DynamiteSticks--;
                SpawnDynamite(agent.Position);
                return true;
            default:
                // README: pistol 1 bullet, mini gun 2, assault rifle 3 per shot,
                // all feeding on the shared bullet pool.
                int burst = Weapon == "assault_rifle" ? 3 : Weapon == "minigun" ? 2 : 1;
                burst = Mathf.Min(burst, Ammo);
                if (burst <= 0)
                    return false;
                Ammo -= burst;
                ShotsFired += burst;
                TriggerAlarm();   // README: a gun shot triggers the alarm
                for (int i = 0; i < burst; i++)
                    SpawnAgentBullet(agent, (i - (burst - 1) / 2f) * 6f);
                return true;
        }
    }

    private void SpawnAgentBullet(Agent agent, float offsetY) =>
        AddChild(new Bullet
        {
            Session = this,
            Direction = agent.Facing,
            Position = agent.Position + new Vector2(agent.Facing * 20, -2 + offsetY),
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

    public void TriggerAlarm() => _alarmTime = AlarmDuration;

    public bool IsCircuitOff(string number) => _circuitsOff.Contains(number);

    // README: computers and switches switch off "laser beam / floor". Lasers
    // are matched per circuit number; electric floors have no per-tile
    // properties yet, so the first hack disables all of them.
    public void TurnOffCircuit(string number)
    {
        _circuitsOff.Add(number);
        if (ElectricFloorsOff)
            return;
        ElectricFloorsOff = true;
        int tile = TileSetBuilder.TileSize;
        for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                if (_level.GetTile(x, y) == TileDefs.ElectricFloor)
                    AddChild(new ColorRect
                    {
                        Position = new Vector2(x * tile, y * tile),
                        Size = new Vector2(tile, tile),
                        Color = new Color(0.1f, 0.1f, 0.15f, 0.55f),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    });
    }

    // The activate action, by priority: leave a closet, hide in a nearby
    // closet, otherwise use a held manual item.
    public void ActivateAction()
    {
        if (Agent.IsHiding)
        {
            Agent.SetHiding(false);
            return;
        }
        foreach (var closet in _closets)
        {
            var offset = (closet.Position - Agent.Position).Abs();
            if (offset.X < 24 && offset.Y < 24)
            {
                Agent.SetHiding(true);
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

    public void SpawnDynamite(Vector2 from) =>
        AddChild(new Dynamite { Session = this, Position = from });

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

        var flash = new ColorRect
        {
            Position = center - new Vector2(ExplosionRadius, ExplosionRadius),
            Size = new Vector2(ExplosionRadius * 2, ExplosionRadius * 2),
            Color = new Color(1f, 0.8f, 0.4f, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(flash);
        GetTree().CreateTimer(0.25).Timeout += () =>
        {
            if (IsInstanceValid(flash))
                flash.QueueFree();
        };
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
            _gogglesTime = ItemDuration;
        else
            _disguiseTime = ItemDuration;
    }

    public void AgentHit()
    {
        if (_ended)
            return;
        if (HasVest)
        {
            HasVest = false;   // README: the vest saves 1 life, then is destroyed
        }
        else
        {
            Lives--;
            if (Lives <= 0)
            {
                End(SignalName.Failed);
                return;
            }
        }
        Agent.ResetForRespawn(_startPosition);
    }

    private void End(StringName signal)
    {
        if (_ended)
            return;
        _ended = true;
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
                break;
            case ObjectDefs.HealthKit:
                Lives++;
                break;
            case ObjectDefs.AmmoPack:
                Ammo += 10;
                break;
            case ObjectDefs.Vest:
                HasVest = true;
                break;
            case ObjectDefs.Briefcase:
            case ObjectDefs.InfraGoggles:
                _heldItems.Add(pickup.Source.Type);
                break;
            case ObjectDefs.UsbKey:
                _usbKeys.Add(NumberOf(pickup.Source));
                break;
            // Weapon pickups equip the weapon and grant its README default ammo.
            case ObjectDefs.WeaponChainsaw:
                Weapon = "chainsaw";
                break;
            case ObjectDefs.WeaponAssaultRifle:
                Weapon = "assault_rifle";
                Ammo += 10;
                break;
            case ObjectDefs.WeaponMinigun:
                Weapon = "minigun";
                Ammo += 20;
                break;
            case ObjectDefs.WeaponCrossbow:
                Weapon = "crossbow";
                Arrows += 20;
                break;
            case ObjectDefs.WeaponDynamite:
                Weapon = "dynamite";
                DynamiteSticks += 3;
                break;
            default:
                var collectible = CollectibleDefs.Get(pickup.Source.Type);
                if (collectible != null)
                {
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
                        }
                    }
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
        string weaponText = Weapon switch
        {
            "chainsaw" => "Chainsaw",
            "crossbow" => $"Crossbow {Arrows}",
            "dynamite" => $"Dynamite {DynamiteSticks}",
            "assault_rifle" => $"Assault rifle {Ammo}",
            "minigun" => $"Mini gun {Ammo}",
            _ => $"Pistol {Ammo}",
        };
        var text = $"Lives {Lives}  {weaponText}";
        if (_keys.Count > 0)
            text += "  Keys: " + string.Join(" ", _keys);
        if (_usbKeys.Count > 0)
            text += "  USB: " + string.Join(" ", _usbKeys);
        if (Agent != null && Agent.IsHiding)
            text += "  Hidden (C to leave)";
        if (Score > 0)
            text += $"  Score {Score}";
        if (CoinsCollected > 0)
            text += $"  Coins +{CoinsCollected}";
        if (HasVest)
            text += "  Vest";
        foreach (var item in _heldItems)
            text += item == ObjectDefs.InfraGoggles ? "  Goggles (press C)" : "  Briefcase (press C)";
        if (GogglesActive)
            text += $"  Infra {Mathf.CeilToInt(_gogglesTime)}s";
        if (_disguiseTime > 0)
            text += $"  Disguise {Mathf.CeilToInt(_disguiseTime)}s";
        if (AlarmActive)
            text += $"  ALARM {Mathf.CeilToInt(_alarmTime)}s";
        _hud.Text = text;
    }
}
