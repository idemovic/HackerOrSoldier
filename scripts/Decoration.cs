using Godot;

namespace HackerOrSoldier;

public record DecorationDef(string Type, string Name, Color Color);

// README decorations: strictly visual, nothing collides with them.
public static class DecorationDefs
{
    public static readonly DecorationDef[] All =
    {
        new("deco_clock", "Digital clock", new Color(0.15f, 0.90f, 0.30f)),
        new("deco_poster", "Poster", new Color(0.80f, 0.70f, 0.50f)),
        new("deco_plant", "Plant", new Color(0.25f, 0.65f, 0.30f)),
        new("deco_toilet", "Toilet", new Color(0.90f, 0.90f, 0.92f)),
        new("deco_machine", "Machine", new Color(0.45f, 0.50f, 0.60f)),
        new("deco_copier", "Copy machine", new Color(0.62f, 0.62f, 0.66f)),
        new("deco_table", "Table", new Color(0.60f, 0.42f, 0.22f)),
        new("deco_crate_wood", "Wooden crate", new Color(0.70f, 0.52f, 0.28f)),
        new("deco_crate_steel", "Steel crate", new Color(0.55f, 0.58f, 0.62f)),
    };

    public static DecorationDef Get(string type)
    {
        foreach (var def in All)
            if (def.Type == type)
                return def;
        return null;
    }
}

// Drawn behind everything that moves: PlaySession parents decorations to a
// layer it adds before any other play object.
public partial class Decoration : Sprite2D
{
    public DecorationDef Def { get; set; }

    public override void _Ready() => Texture = Sprites.ForObject(Def.Type);
}
