using Godot;

namespace HackerOrSoldier;

public record TileDef(int Id, string Name, Color Color, bool Solid);

public record ObjectDef(string Type, string Name, Color Color);

public static class TileDefs
{
    public const int Empty = 0;

    public static readonly TileDef[] All =
    {
        new(1, "Wall", new Color(0.35f, 0.37f, 0.45f), Solid: true),
        new(2, "Destructible wall", new Color(0.55f, 0.45f, 0.30f), Solid: true),
        new(3, "Alarm floor", new Color(0.85f, 0.65f, 0.10f), Solid: true),
        new(4, "Electric floor", new Color(0.90f, 0.85f, 0.20f), Solid: true),
        new(5, "Water", new Color(0.10f, 0.35f, 0.80f), Solid: false),
        new(6, "Lava", new Color(0.95f, 0.25f, 0.05f), Solid: false),
        new(7, "Spikes", new Color(0.75f, 0.75f, 0.80f), Solid: false),
    };
}

public static class ObjectDefs
{
    public const string AgentStart = "agent_start";
    public const string ExitDoor = "exit_door";

    public static readonly ObjectDef[] All =
    {
        new(AgentStart, "Agent start", new Color(1f, 0.16f, 0.43f)),
        new(ExitDoor, "Exit door", new Color(0.2f, 0.9f, 0.4f)),
    };
}
