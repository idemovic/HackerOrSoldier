using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

// The in-game help screen: the controls on the left, read from the input
// map so rebound keys show, and on the right what each thing in the current
// level does. Only things the level contains are listed, in palette order;
// scenery has no help line and drops out.
public partial class HelpOverlay : CanvasLayer
{
    public const string Action = "help";

    // Above the session HUD, which would otherwise draw over the panel.
    private const int HelpLayer = 10;
    // Past this many entries two columns overflow the 360px screen.
    private const int TwoColumnLimit = 26;

    // Name plus text stays near 44 characters: two lines in a three-column
    // layout, so a level holding nearly every object still fits the screen.
    private static readonly Dictionary<int, string> TileHelp = new()
    {
        [TileDefs.DestructibleWall] = "Dynamite breaks it.",
        [TileDefs.AlarmFloor] = "Stepping on it sounds the alarm.",
        [TileDefs.ElectricFloor] = "Shocks until its circuit is cut.",
        [TileDefs.Water] = "Slows you. Jump to swim up. Has eels.",
        [TileDefs.Lava] = "Instant death.",
        [TileDefs.Spikes] = "Each spike rises in turn every 10s.",
        [TileDefs.Spear] = "Tips warn 3s before deadly spears.",
        [TileDefs.HiddenPlatform] = "Appears with infra goggles.",
    };

    private static readonly Dictionary<string, string> ObjectHelp = new()
    {
        [ObjectDefs.ExitDoor] = "Reach it to finish the mission.",
        [ObjectDefs.Checkpoint] = "Respawn here after losing a life.",
        [ObjectDefs.Door] = "Opens with the key of its color.",
        [ObjectDefs.Teleport] = "Takes you to the paired capsule.",
        [ObjectDefs.Elevator] = "Rides up and down.",
        [ObjectDefs.MovingPlatform] = "Rides left and right.",
        [ObjectDefs.ElectricDoor] = "Its field zaps every 10s.",
        [ObjectDefs.FlameTrap] = "Shoots flame every 10s.",
        [ObjectDefs.Computer] = "USB key hacks it, cuts the circuit.",
        [ObjectDefs.Switch] = "Shoot it to cut its circuit.",
        [ObjectDefs.Laser] = "Deadly until its circuit is cut.",
        [ObjectDefs.SecurityDoor] = "Opens when its circuit is cut.",
        [ObjectDefs.Camera] = "Spots you, sounds the alarm.",
        [ObjectDefs.RocketLauncher] = "Fires rockets.",
        [ObjectDefs.Closet] = "Activate beside it to hide.",
        [ObjectDefs.Barrel] = "Push it to climb to high ledges.",
        [ObjectDefs.DoorKey] = "Opens the door of its color.",
        [ObjectDefs.UsbKey] = "Hacks the computer of its number.",
        [ObjectDefs.HealthKit] = "Adds a life.",
        [ObjectDefs.AmmoPack] = "Adds 10 ammo.",
        [ObjectDefs.Vest] = "Absorbs one hit.",
        [ObjectDefs.Briefcase] = "Activate to disguise for 30s.",
        [ObjectDefs.InfraGoggles] = "Activate for 30s of infra vision.",
        [ObjectDefs.WeaponChainsaw] = "Melee. No ammo, no alarm.",
        [ObjectDefs.WeaponAssaultRifle] = "3 bullets per shot.",
        [ObjectDefs.WeaponMinigun] = "2 bullets per shot.",
        [ObjectDefs.WeaponCrossbow] = "Arrows don't sound the alarm.",
        [ObjectDefs.WeaponDynamite] = "Blasts 5 tiles around after 5s.",
        [EnemyDefs.Fighter] = "Punches up close. 2 hits.",
        [EnemyDefs.Soldier] = "Shoots a pistol along its row.",
        [EnemyDefs.SuperSoldier] = "Mini gun. A hit demotes it.",
        [EnemyDefs.UltimateSoldier] = "Chainsaw. A hit demotes it.",
        [EnemyDefs.Drone] = "Flies, sees all around, drops dynamite.",
        [EnemyDefs.Robodog] = "Fast biter. 2 hits.",
        [EnemyDefs.Turret] = "Fires plasma in every direction.",
        [EnemyDefs.Robot] = "Hit it in its 1s shutdown every 10s.",
        [EnemyDefs.Landmine] = "Blows up on touch. Indestructible.",
        [EnemyDefs.ElectricEel] = "Shocks on touch in water.",
    };

    private const string CollectiblesHelp = "Score and coins. Spell AGENT for a bonus.";

    private static readonly (string Name, string InputAction, string Note)[] Controls =
    {
        ("Left", "move_left", null),
        ("Right", "move_right", null),
        ("Jump", "jump", "Swims up in water."),
        ("Shoot", "shoot", "Fires the weapon you hold. Guns sound the alarm."),
        ("Activate", "activate", "Hides in a closet, or uses a briefcase or goggles."),
        ("Pause", "pause", null),
        ("Help", Action, "Hold to show this screen."),
    };

    private static readonly string[] Tips =
    {
        "Keys, USB keys, health, ammo and vests work on their own.",
        "The alarm lasts 30s. Enemies move twice as fast.",
        "Finish without a kill to earn the Hacker tag.",
    };

    public HelpOverlay(LevelData level)
    {
        Layer = HelpLayer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.8f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(backdrop);
        var panel = new PanelContainer { Theme = EditorTheme.Build() };
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.OffsetLeft = panel.OffsetTop = 6;
        panel.OffsetRight = panel.OffsetBottom = -6;
        AddChild(panel);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 8);
        panel.AddChild(columns);
        columns.AddChild(BuildControls());
        columns.AddChild(new VSeparator());
        columns.AddChild(BuildLevelGuide(level));
    }

    // Every palette entry with a help line that the level contains, in
    // palette order. Collectibles fold into one line.
    public static IEnumerable<(Texture2D Icon, string Name, string Text)> EntriesFor(LevelData level)
    {
        var tiles = level.Tiles.ToHashSet();
        var types = level.Objects.Select(o => o.Type).ToHashSet();
        foreach (var category in EditorPalette.Categories)
        {
            var present = category.Entries
                .Where(e => e.IsTile ? tiles.Contains(e.TileId) : types.Contains(e.ObjectType))
                .ToArray();
            if (category.Name == "Collectibles")
            {
                if (present.Length > 0)
                    yield return (present[0].Icon, "Collectibles", CollectiblesHelp);
                continue;
            }
            foreach (var entry in present)
            {
                string text = entry.IsTile ? TileHelp.GetValueOrDefault(entry.TileId) : ObjectHelp.GetValueOrDefault(entry.ObjectType);
                if (text != null)
                    yield return (entry.Icon, entry.Name, text);
            }
        }
    }

    private static Control BuildControls()
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0) };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(Heading("Controls"));
        foreach (var (name, inputAction, note) in Controls)
        {
            string text = $"[color=#{EditorTheme.Ink.ToHtml(false)}]{name}[/color]  [color=#{EditorTheme.Accent.ToHtml(false)}]{KeysFor(inputAction)}[/color]";
            if (note != null)
                text += $"\n[color=#{EditorTheme.Dim.ToHtml(false)}]{note}[/color]";
            box.AddChild(Rich(text));
        }
        box.AddChild(new HSeparator());
        box.AddChild(Heading("Tips"));
        foreach (var tip in Tips)
            box.AddChild(Rich($"[color=#{EditorTheme.Dim.ToHtml(false)}]{tip}[/color]"));
        return box;
    }

    private static Control BuildLevelGuide(LevelData level)
    {
        var entries = EntriesFor(level).ToArray();
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(Heading(level.Name.Length > 0 ? $"In {level.Name}" : "In this level"));

        // Each column stacks on its own, so a wrapped entry never stretches
        // the entries beside it; filled top-down to keep categories together.
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);
        var columns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(columns);
        int columnCount = entries.Length > TwoColumnLimit ? 3 : 2;
        int perColumn = Mathf.Max(1, Mathf.CeilToInt(entries.Length / (float)columnCount));
        for (int c = 0; c < columnCount; c++)
        {
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", 3);
            columns.AddChild(column);
            foreach (var (icon, name, text) in entries.Skip(c * perColumn).Take(perColumn))
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);
                row.AddChild(new TextureRect
                {
                    Texture = icon,
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
                });
                var label = Rich($"[color=#{EditorTheme.Gold.ToHtml(false)}]{name}[/color] {text}");
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(label);
                column.AddChild(row);
            }
        }
        if (entries.Length == 0)
            box.AddChild(Rich($"[color=#{EditorTheme.Dim.ToHtml(false)}]Nothing special here. Find the exit door.[/color]"));
        return box;
    }

    // The keyboard keys bound to an action, by the label they carry on the
    // player's layout.
    private static string KeysFor(string action) =>
        string.Join(" / ", InputMap.ActionGetEvents(action).OfType<InputEventKey>().Select(e =>
            Chord.KeyName(e.PhysicalKeycode != Key.None
                ? DisplayServer.KeyboardGetKeycodeFromPhysical(e.PhysicalKeycode)
                : e.Keycode)));

    private static Label Heading(string text) => new() { Text = text, ThemeTypeVariation = EditorTheme.Heading };

    private static RichTextLabel Rich(string bbcode) => new()
    {
        BbcodeEnabled = true,
        Text = bbcode,
        FitContent = true,
        ScrollActive = false,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
}
