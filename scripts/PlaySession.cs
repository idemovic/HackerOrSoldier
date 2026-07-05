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

    public Agent Agent { get; private set; }
    public int Lives { get; private set; } = 3;   // README: default 3 lives
    public int Ammo { get; private set; } = 10;   // README: pistol, default 10 ammo
    public bool HasVest { get; private set; }
    public bool GogglesActive => _gogglesTime > 0;
    public float Time { get; set; }               // settable so tests can skip ahead
    public bool SpikesUp => Time % SpikeCycle >= SpikeCycle - SpikeUpTime;
    public float LevelBottom => _level.Height * TileSetBuilder.TileSize;

    private LevelData _level;
    private TileMapLayer _grid;
    private TileMapLayer _hiddenGrid;
    private PackedScene _agentScene;

    private readonly HashSet<Vector2I> _destroyed = new();
    private readonly HashSet<string> _keys = new();
    private readonly List<string> _heldItems = new();
    private readonly List<LockedDoor> _doors = new();
    private float _gogglesTime;
    private float _disguiseTime;
    private Vector2 _startPosition;
    private Node2D _spikes;
    private Label _hud;
    private bool _ended;

    public void Setup(LevelData level, TileMapLayer grid, TileMapLayer hiddenGrid, PackedScene agentScene)
    {
        _level = level;
        _grid = grid;
        _hiddenGrid = hiddenGrid;
        _agentScene = agentScene;
    }

    public override void _Ready()
    {
        int tile = TileSetBuilder.TileSize;
        _hiddenGrid.Modulate = new Color(1, 1, 1, 0);

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
                default:
                    var pickup = new Pickup { Position = pos, Source = obj };
                    pickup.Collected += OnPickup;
                    AddChild(pickup);
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

    public void TryShoot(Agent agent)
    {
        if (Ammo <= 0)
            return;
        Ammo--;
        AddChild(new Bullet
        {
            Session = this,
            Direction = agent.Facing,
            Position = agent.Position + new Vector2(agent.Facing * 20, -2),
        });
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
        }
        pickup.QueueFree();
        UpdateHud();
    }

    private static string ColorOf(LevelObject obj) =>
        obj.Properties.TryGetValue("color", out var color) ? color : ObjectDefs.KeyColors[0];

    private void UpdateHud()
    {
        var text = $"Lives {Lives}  Ammo {Ammo}";
        if (_keys.Count > 0)
            text += "  Keys: " + string.Join(" ", _keys);
        if (HasVest)
            text += "  Vest";
        foreach (var item in _heldItems)
            text += item == ObjectDefs.InfraGoggles ? "  Goggles (press C)" : "  Briefcase (press C)";
        if (GogglesActive)
            text += $"  Infra {Mathf.CeilToInt(_gogglesTime)}s";
        if (_disguiseTime > 0)
            text += $"  Disguise {Mathf.CeilToInt(_disguiseTime)}s";
        _hud.Text = text;
    }
}
