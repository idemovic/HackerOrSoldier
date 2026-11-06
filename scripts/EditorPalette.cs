using System;
using System.Linq;
using Godot;

namespace HackerOrSoldier;

// A key plus exact modifiers: Ctrl+Shift+P does not match Ctrl+P.
// Letters match the layout's label (Ctrl+Z is Z on QWERTZ too); digits and
// brackets match the key position, since layouts like Slovak type letters
// on the number row.
public readonly record struct Chord(Key Key, bool Ctrl = false, bool Shift = false, bool Alt = false)
{
    private bool ByPosition => Key is >= Key.Key0 and <= Key.Key9 or Key.Bracketleft or Key.Bracketright or Key.Minus or Key.Equal;

    public bool Matches(InputEventKey e) =>
        (ByPosition && e.PhysicalKeycode != Key.None ? e.PhysicalKeycode : e.Keycode) == Key
        && e.CtrlPressed == Ctrl && e.ShiftPressed == Shift && e.AltPressed == Alt && !e.MetaPressed;

    public override string ToString() =>
        (Ctrl ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + KeyName(Key);

    public static string KeyName(Key key) => key switch
    {
        Key.Bracketleft => "[",
        Key.Bracketright => "]",
        Key.Minus => "-",
        Key.Equal => "=",
        Key.Escape => "Esc",
        _ => OS.GetKeycodeString(key),
    };
}

// One editor action: every button, menu item and key that triggers it, and
// its help line, come from this record.
public record EditorCommand(string Name, string Group, Action Run, params Chord[] Chords)
{
    public string Keys => string.Join(" / ", Chords.Select(c => c.ToString()));
    public string Tooltip => Chords.Length == 0 ? Name : $"{Name}  ({Keys})";
}

// One selectable brush: a tile (TileId >= 0) or an object type.
public record PaletteEntry(string Name, int TileId, string ObjectType)
{
    public bool IsTile => TileId >= 0;

    public Texture2D Icon => IsTile ? TileSetBuilder.TileIcon(TileId) : Sprites.Icon(ObjectType);
}

public record PaletteCategory(string Name, PaletteEntry Icon, PaletteEntry[] Entries);

// The palette and its keyboard map. Categories sit on Alt+1..Alt+0; inside
// a category, items 1-10 sit on 1..0 and items 11-20 on Shift+1..Shift+0.
public static class EditorPalette
{
    private static readonly Key[] Digits =
        { Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9, Key.Key0 };

    public const int SlotsPerTier = 10;
    public const int MaxSlots = 2 * SlotsPerTier;

    public static Chord SlotChord(int index) => new(Digits[index % SlotsPerTier], Shift: index >= SlotsPerTier);

    public static Chord CategoryChord(int index) => new(Digits[index], Alt: true);

    // The slot a key selects, or -1.
    public static int SlotFor(InputEventKey e)
    {
        for (int i = 0; i < MaxSlots; i++)
            if (SlotChord(i).Matches(e))
                return i;
        return -1;
    }

    public static int CategoryFor(InputEventKey e)
    {
        for (int i = 0; i < Digits.Length; i++)
            if (CategoryChord(i).Matches(e))
                return i;
        return -1;
    }

    public static readonly PaletteCategory[] Categories = Build();

    // Categories follow the README's palette grouping.
    private static PaletteCategory[] Build()
    {
        PaletteEntry Tile(int id) => new(TileDefs.Get(id).Name, id, null);
        PaletteEntry Obj(string type)
        {
            var def = ObjectDefs.All.First(d => d.Type == type);
            return new PaletteEntry(def.Name, -1, type);
        }

        var wallIds = new[]
        {
            TileDefs.Wall, TileDefs.DestructibleWall, TileDefs.StoneBlocks, TileDefs.RedBrick,
            TileDefs.SteelPlate, TileDefs.Girder, TileDefs.Concrete, TileDefs.Sandbags,
            TileDefs.Container, TileDefs.PipeWall,
            TileDefs.BackgroundMesh, TileDefs.BackgroundRock, TileDefs.BackgroundPanel,
            TileDefs.BackgroundBrick, TileDefs.BackgroundCircuit, TileDefs.BackgroundHangar,
        };
        var walls = wallIds.Select(Tile).ToArray();

        var terrain = TileDefs.All.Where(d => !wallIds.Contains(d.Id) && TileDefs.PaletteTile(d.Id) == d.Id).Select(d => Tile(d.Id))
            .Append(Obj(ObjectDefs.Door))
            .Append(Obj(ObjectDefs.Teleport))
            .Append(Obj(ObjectDefs.Elevator))
            .Append(Obj(ObjectDefs.MovingPlatform))
            .Append(Obj(ObjectDefs.ElectricDoor))
            .Append(Obj(ObjectDefs.FlameTrap))
            .ToArray();

        var enemies = EnemyDefs.All.Select(d => new PaletteEntry(d.Name, -1, d.Type)).ToArray();
        var collectibles = CollectibleDefs.All.Select(d => new PaletteEntry(d.Name, -1, d.Type)).ToArray();
        var decorations = DecorationDefs.All.Select(d => new PaletteEntry(d.Name, -1, d.Type))
            .Append(Obj(ObjectDefs.Light)).ToArray();

        PaletteCategory Category(string name, PaletteEntry[] entries, int iconIndex = 0) =>
            entries.Length <= MaxSlots
                ? new(name, entries[iconIndex], entries)
                : throw new InvalidOperationException($"palette category {name} has {entries.Length} entries; only {MaxSlots} have keys");

        var categories = new[]
        {
            Category("Mission", new[] { Obj(ObjectDefs.AgentStart), Obj(ObjectDefs.Checkpoint), Obj(ObjectDefs.ExitDoor) }),
            Category("Walls", walls),
            Category("Terrain", terrain, Array.FindIndex(terrain, e => e.TileId == TileDefs.Spikes)),
            Category("Devices", new[]
            {
                Obj(ObjectDefs.Computer), Obj(ObjectDefs.Switch), Obj(ObjectDefs.Laser),
                Obj(ObjectDefs.SecurityDoor), Obj(ObjectDefs.FloorCircuit),
                Obj(ObjectDefs.Camera), Obj(ObjectDefs.RocketLauncher), Obj(ObjectDefs.Closet),
            }),
            Category("Movables", new[] { Obj(ObjectDefs.Barrel), Obj(ObjectDefs.SteelBarrel) }),
            Category("Enemies", enemies, Array.FindIndex(enemies, e => e.ObjectType == EnemyDefs.Soldier)),
            Category("Items", new[]
            {
                Obj(ObjectDefs.DoorKey), Obj(ObjectDefs.UsbKey), Obj(ObjectDefs.HealthKit),
                Obj(ObjectDefs.AmmoPack), Obj(ObjectDefs.Vest), Obj(ObjectDefs.Briefcase),
                Obj(ObjectDefs.InfraGoggles),
            }, 2),
            Category("Weapons", new[]
            {
                Obj(ObjectDefs.WeaponChainsaw), Obj(ObjectDefs.WeaponAssaultRifle),
                Obj(ObjectDefs.WeaponMinigun), Obj(ObjectDefs.WeaponCrossbow),
                Obj(ObjectDefs.WeaponDynamite),
            }, 1),
            Category("Collectibles", collectibles, Array.FindIndex(collectibles, e => e.Name == "Diamond")),
            Category("Decorations", decorations, Array.FindIndex(decorations, e => e.Name == "Plant")),
        };
        if (categories.Length > Digits.Length)
            throw new InvalidOperationException($"{categories.Length} palette categories; only {Digits.Length} have keys");
        return categories;
    }

    // Where a tile or object lives in the palette.
    public static (int Category, int Slot) Find(int tileId, string objectType)
    {
        for (int c = 0; c < Categories.Length; c++)
        {
            int slot = Array.FindIndex(Categories[c].Entries,
                e => objectType != null ? e.ObjectType == objectType : e.TileId == TileDefs.PaletteTile(tileId));
            if (slot >= 0)
                return (c, slot);
        }
        return (-1, -1);
    }
}
