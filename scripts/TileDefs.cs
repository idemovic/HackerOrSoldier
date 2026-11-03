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
    public const int BackgroundSky = 24;
    public const int BackgroundSkyDay = 25;

    public static readonly TileDef[] All =
    {
        new(Wall, "Wall", new Color(0.35f, 0.37f, 0.45f), Solid: true),
        new(DestructibleWall, "Destructible wall", new Color(0.55f, 0.45f, 0.30f), Solid: true),
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
        new(BackgroundSky, "Night sky", new Color(0.05f, 0.06f, 0.10f), Solid: false),
        new(BackgroundSkyDay, "Day sky", new Color(0.23f, 0.44f, 0.88f), Solid: false),
    };

    public static TileDef Get(int id)
    {
        foreach (var def in All)
            if (def.Id == id)
                return def;
        return null;
    }

    // Solid tiles shade the painted scenery next to them. Hidden platforms
    // never cast (a shadow would give them away in play); the open sky and
    // the liquids never receive.
    public static bool CastsShadow(int id) => id != HiddenPlatform && Get(id) is { Solid: true };

    public static bool ReceivesShadow(int id) =>
        id is not (Empty or Water or Lava or BackgroundSky or BackgroundSkyDay) && Get(id) is { Solid: false };
}

public static class ObjectDefs
{
    public const string AgentStart = "agent_start";
    public const string ExitDoor = "exit_door";
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
    public const string Camera = "camera";
    public const string Closet = "closet";
    public const string Barrel = "barrel";
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
        new(Camera, "Security camera", new Color(0.65f, 0.65f, 0.72f)),
        new(Closet, "Closet", new Color(0.50f, 0.33f, 0.18f)),
        new(Barrel, "Barrel", new Color(0.62f, 0.44f, 0.24f)),
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

    public static Color KeyColor(string name) => name switch
    {
        "blue" => new Color(0.25f, 0.50f, 0.95f),
        "yellow" => new Color(0.95f, 0.85f, 0.20f),
        "magenta" => new Color(0.85f, 0.25f, 0.85f),
        _ => new Color(0.90f, 0.20f, 0.20f),
    };

    public static bool HasColorProperty(string type) => type is Door or DoorKey;

    // Circuits: a computer or switch with number N turns off lasers with
    // number N; USB keys hack the computer with the matching number.
    public static readonly string[] Numbers = { "1", "2", "3", "4" };

    public static bool HasNumberProperty(string type) =>
        type is Computer or UsbKey or Switch or Laser or Teleport;

    public static bool IsPickup(string type) =>
        type is DoorKey or HealthKit or AmmoPack or Vest or Briefcase or InfraGoggles or UsbKey
            or WeaponChainsaw or WeaponAssaultRifle or WeaponMinigun or WeaponCrossbow or WeaponDynamite;
}
