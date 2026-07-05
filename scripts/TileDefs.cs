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
    };

    public static TileDef Get(int id)
    {
        foreach (var def in All)
            if (def.Id == id)
                return def;
        return null;
    }
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

    public static bool IsPickup(string type) =>
        type is DoorKey or HealthKit or AmmoPack or Vest or Briefcase or InfraGoggles;
}
