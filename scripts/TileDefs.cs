using Godot;

namespace HackerOrSoldier;

public record TileDef(int Id, string Name, Color Color, bool Solid);

public record ObjectDef(string Type, string Name, Color Color);

public static class TileDefs
{
    public const int Empty = 0;
    public const int Wall = 1;
    public const int DestructibleWall = 2;
    public const int AlarmFloor = 3;
    public const int ElectricFloor = 4;
    public const int Water = 5;
    public const int Lava = 6;
    public const int Spikes = 7;
    public const int Spear = 8;
    public const int HiddenPlatform = 9;
    public const int StoneBlocks = 10;
    public const int RedBrick = 11;
    public const int SteelPlate = 12;
    public const int Girder = 13;
    public const int BackgroundMesh = 14;
    public const int BackgroundRock = 15;
    public const int BackgroundPanel = 16;
    public const int Concrete = 17;
    public const int Sandbags = 18;
    public const int Container = 19;
    public const int PipeWall = 20;
    public const int BackgroundBrick = 21;
    public const int BackgroundCircuit = 22;
    public const int BackgroundHangar = 23;
    public const int DestructibleStone = 26;
    public const int DestructibleRed = 27;
    public const int DestructibleSteel = 28;
    public const int DestructibleMoss = 29;
    public const int DestructibleSand = 30;

    // The destructible wall in each brick color, plain clay first. One
    // palette slot paints them all; the status bar picks the color.
    public static readonly int[] DestructibleWalls =
        { DestructibleWall, DestructibleStone, DestructibleRed, DestructibleSteel, DestructibleMoss, DestructibleSand };

    public static bool IsDestructible(int id) => System.Array.IndexOf(DestructibleWalls, id) >= 0;

    // The palette tile that paints this id: a colored destructible wall
    // folds into the plain one.
    public static int PaletteTile(int id) => IsDestructible(id) ? DestructibleWall : id;

    public static readonly TileDef[] All =
    {
        new(Wall, "Wall", new Color(0.35f, 0.37f, 0.45f), Solid: true),
        new(DestructibleWall, "Destructible wall", new Color(0.55f, 0.45f, 0.30f), Solid: true),
        new(DestructibleStone, "Destructible wall (stone)", new Color(0.42f, 0.42f, 0.48f), Solid: true),
        new(DestructibleRed, "Destructible wall (red)", new Color(0.56f, 0.16f, 0.13f), Solid: true),
        new(DestructibleSteel, "Destructible wall (steel)", new Color(0.44f, 0.50f, 0.60f), Solid: true),
        new(DestructibleMoss, "Destructible wall (moss)", new Color(0.18f, 0.36f, 0.16f), Solid: true),
        new(DestructibleSand, "Destructible wall (sand)", new Color(0.71f, 0.57f, 0.35f), Solid: true),
        new(AlarmFloor, "Alarm floor", new Color(0.85f, 0.65f, 0.10f), Solid: true),
        new(ElectricFloor, "Electric floor", new Color(0.90f, 0.85f, 0.20f), Solid: true),
        new(Water, "Water", new Color(0.10f, 0.35f, 0.80f), Solid: false),
        new(Lava, "Lava", new Color(0.95f, 0.25f, 0.05f), Solid: false),
        new(Spikes, "Spikes", new Color(0.75f, 0.75f, 0.80f), Solid: false),
        new(Spear, "Spear", new Color(0.60f, 0.20f, 0.25f), Solid: false),
        new(HiddenPlatform, "Hidden platform", new Color(0.45f, 0.75f, 0.85f), Solid: true),
        new(StoneBlocks, "Stone blocks", new Color(0.42f, 0.42f, 0.48f), Solid: true),
        new(RedBrick, "Red brick", new Color(0.56f, 0.16f, 0.13f), Solid: true),
        new(SteelPlate, "Steel plate", new Color(0.55f, 0.60f, 0.72f), Solid: true),
        new(Girder, "Girder", new Color(0.45f, 0.48f, 0.55f), Solid: true),
        new(Concrete, "Concrete", new Color(0.42f, 0.43f, 0.47f), Solid: true),
        new(Sandbags, "Sandbags", new Color(0.65f, 0.53f, 0.32f), Solid: true),
        new(Container, "Container", new Color(0.22f, 0.45f, 0.25f), Solid: true),
        new(PipeWall, "Pipe wall", new Color(0.16f, 0.52f, 0.52f), Solid: true),
        // Background walls: scenery behind the agent, no collision.
        new(BackgroundMesh, "Background mesh", new Color(0.10f, 0.11f, 0.14f), Solid: false),
        new(BackgroundRock, "Background rock", new Color(0.24f, 0.25f, 0.29f), Solid: false),
        new(BackgroundPanel, "Background panel", new Color(0.13f, 0.15f, 0.21f), Solid: false),
        new(BackgroundBrick, "Background brick", new Color(0.14f, 0.15f, 0.18f), Solid: false),
        new(BackgroundCircuit, "Background circuit", new Color(0.08f, 0.14f, 0.10f), Solid: false),
        new(BackgroundHangar, "Background hangar", new Color(0.12f, 0.13f, 0.16f), Solid: false),
    };

    public static TileDef Get(int id)
    {
        foreach (var def in All)
            if (def.Id == id)
                return def;
        return null;
    }

    public static bool CastsShadow(int id) => Get(id) is { Solid: true };

    public static bool ReceivesShadow(int id) =>
        id is not (Empty or Water or Lava) && Get(id) is { Solid: false };
}

public static class ObjectDefs
{
    public const string AgentStart = "agent_start";
    public const string ExitDoor = "exit_door";
    public const string Checkpoint = "checkpoint";
    public const string Door = "door";
    public const string DoorKey = "door_key";
    public const string HealthKit = "health_kit";
    public const string AmmoPack = "ammo_pack";
    public const string Vest = "vest";
    public const string Briefcase = "briefcase";
    public const string InfraGoggles = "infra_goggles";
    public const string Computer = "computer";
    public const string UsbKey = "usb_key";
    public const string Switch = "switch";
    public const string Laser = "laser";
    public const string SecurityDoor = "security_door";
    public const string FloorCircuit = "floor_circuit";
    public const string Camera = "camera";
    public const string Closet = "closet";
    public const string Barrel = "barrel";
    public const string SteelBarrel = "barrel_steel";
    public const string Teleport = "teleport";
    public const string Elevator = "elevator";
    public const string MovingPlatform = "moving_platform";
    public const string ElectricDoor = "electric_door";
    public const string FlameTrap = "flame_trap";
    public const string RocketLauncher = "rocket_launcher";
    public const string Light = "light";
    public const string WeaponChainsaw = "weapon_chainsaw";
    public const string WeaponAssaultRifle = "weapon_assault_rifle";
    public const string WeaponMinigun = "weapon_minigun";
    public const string WeaponCrossbow = "weapon_crossbow";
    public const string WeaponDynamite = "weapon_dynamite";

    public static readonly ObjectDef[] All =
    {
        new(AgentStart, "Agent start", new Color(1f, 0.16f, 0.43f)),
        new(ExitDoor, "Exit door", new Color(0.2f, 0.9f, 0.4f)),
        new(Checkpoint, "Checkpoint", new Color(0.25f, 0.68f, 0.31f)),
        new(Door, "Door", new Color(0.9f, 0.2f, 0.2f)),
        new(DoorKey, "Door key", new Color(0.9f, 0.2f, 0.2f)),
        new(HealthKit, "Health kit", new Color(0.95f, 0.95f, 0.95f)),
        new(AmmoPack, "Ammo pack", new Color(0.65f, 0.60f, 0.35f)),
        new(Vest, "Bulletproof vest", new Color(0.35f, 0.55f, 0.65f)),
        new(Briefcase, "Briefcase", new Color(0.55f, 0.35f, 0.20f)),
        new(InfraGoggles, "Infra goggles", new Color(0.90f, 0.45f, 0.15f)),
        new(Computer, "Computer", new Color(0.25f, 0.70f, 0.70f)),
        new(UsbKey, "USB key", new Color(0.85f, 0.75f, 0.25f)),
        new(Switch, "Switch", new Color(0.75f, 0.55f, 0.75f)),
        new(Laser, "Laser beam", new Color(0.95f, 0.30f, 0.30f)),
        new(SecurityDoor, "Security door", new Color(0.55f, 0.62f, 0.75f)),
        new(FloorCircuit, "Floor circuit", new Color(0.90f, 0.85f, 0.20f)),
        new(Camera, "Security camera", new Color(0.65f, 0.65f, 0.72f)),
        new(Closet, "Closet", new Color(0.50f, 0.33f, 0.18f)),
        new(Barrel, "Barrel", new Color(0.62f, 0.44f, 0.24f)),
        new(SteelBarrel, "Steel barrel", new Color(0.48f, 0.54f, 0.65f)),
        new(Teleport, "Teleport", new Color(0.45f, 0.30f, 0.90f)),
        new(Elevator, "Elevator", new Color(0.55f, 0.60f, 0.70f)),
        new(MovingPlatform, "Moving platform", new Color(0.60f, 0.65f, 0.55f)),
        new(ElectricDoor, "Electric door", new Color(0.90f, 0.90f, 0.40f)),
        new(FlameTrap, "Flame trap", new Color(0.95f, 0.50f, 0.15f)),
        new(RocketLauncher, "Rocket launcher", new Color(0.50f, 0.35f, 0.35f)),
        new(Light, "Light", new Color(1.00f, 0.95f, 0.70f)),
        new(WeaponChainsaw, "Chainsaw", new Color(0.80f, 0.45f, 0.20f)),
        new(WeaponAssaultRifle, "Assault rifle", new Color(0.35f, 0.40f, 0.30f)),
        new(WeaponMinigun, "Mini gun", new Color(0.30f, 0.35f, 0.40f)),
        new(WeaponCrossbow, "Crossbow", new Color(0.50f, 0.40f, 0.25f)),
        new(WeaponDynamite, "Dynamite", new Color(0.90f, 0.15f, 0.10f)),
    };

    // Colors available for door / key pairs (README: matching colors).
    public static readonly string[] KeyColors = { "red", "blue", "yellow", "magenta" };

    // Underside paints for elevators and moving platforms; purely visual.
    public static readonly string[] PlatformPaints = { "steel", "hazard", "rust", "teal", "olive", "maroon" };

    // The fill for a sprite's 'X' slot, for any name in either list.
    public static Color SlotColor(string name) => name switch
    {
        "blue" => new Color(0.25f, 0.50f, 0.95f),
        "yellow" => new Color(0.95f, 0.85f, 0.20f),
        "magenta" => new Color(0.85f, 0.25f, 0.85f),
        "steel" => new Color("4a5570"),
        "hazard" => new Color("c8a020"),
        "rust" => new Color("b8562a"),
        "teal" => new Color("2f8a7a"),
        "olive" => new Color("5e7a2e"),
        "maroon" => new Color("8a2f3a"),
        _ => new Color(0.90f, 0.20f, 0.20f),
    };

    // The colors a type can carry, or null when it has no color property.
    public static string[] ColorChoices(string type) => type switch
    {
        Door or DoorKey => KeyColors,
        Elevator or MovingPlatform => PlatformPaints,
        _ => null,
    };

    public static bool HasColorProperty(string type) => ColorChoices(type) != null;

    // The object's stored color, or its type's first choice when unset or unknown.
    public static string ColorOf(string type, string stored)
    {
        var choices = ColorChoices(type);
        return System.Array.IndexOf(choices, stored) >= 0 ? stored : choices[0];
    }

    // Circuits: a computer or switch with number N turns off the lasers,
    // security doors and floor circuits with number N; USB keys hack the
    // computer with the matching number. Teleports pair by number too.
    public static readonly string[] Numbers = { "1", "2", "3", "4", "5", "6", "7", "8" };

    // In the world a circuit or teleport pair shows as a colored line along
    // the bottom of each member, one color per number.
    private static readonly (string Name, Color Color)[] CircuitColors =
    {
        ("red", new Color("e83b3b")), ("orange", new Color("f08a24")), ("yellow", new Color("f2e03a")),
        ("green", new Color("3bd45a")), ("cyan", new Color("3fd8e8")), ("blue", new Color("3b6ef0")),
        ("purple", new Color("b04be8")), ("white", new Color("f0f0f0")),
    };

    public static Color CircuitColor(string number) => CircuitColors[CircuitIndex(number)].Color;

    public static string CircuitColorName(string number) => CircuitColors[CircuitIndex(number)].Name;

    private static int CircuitIndex(string number) => Mathf.Max(0, System.Array.IndexOf(Numbers, number));

    public static bool HasNumberProperty(string type) =>
        type is Computer or UsbKey or Switch or Laser or Teleport or SecurityDoor or FloorCircuit;

    // Security cameras face right or left. Right comes first so a level saved
    // without a facing keeps its right-facing cameras.
    public static readonly string[] Facings = { "right", "left" };

    public static bool HasFacingProperty(string type) => type == Camera;

    public static bool FacesLeft(LevelObject obj) =>
        HasFacingProperty(obj.Type) && obj.Properties.TryGetValue("facing", out var facing) && facing == Facings[1];

    public static bool IsPickup(string type) =>
        type is DoorKey or HealthKit or AmmoPack or Vest or Briefcase or InfraGoggles or UsbKey
            or WeaponChainsaw or WeaponAssaultRifle or WeaponMinigun or WeaponCrossbow or WeaponDynamite;
}
