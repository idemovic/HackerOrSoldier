using Godot;

namespace HackerOrSoldier;

public record DecorationDef(string Type, string Name, Color Color, bool Solid, int Height = 32);

// README decorations. Most are strictly visual; tables and crates are solid
// and can be jumped on.
public static class DecorationDefs
{
    public static readonly DecorationDef[] All =
    {
        new("deco_clock", "Digital clock", new Color(0.15f, 0.90f, 0.30f), Solid: false),
        new("deco_poster", "Poster", new Color(0.80f, 0.70f, 0.50f), Solid: false),
        new("deco_plant", "Plant", new Color(0.25f, 0.65f, 0.30f), Solid: false),
        new("deco_toilet", "Toilet", new Color(0.90f, 0.90f, 0.92f), Solid: false),
        new("deco_pipe", "Pipe", new Color(0.50f, 0.55f, 0.60f), Solid: false),
        new("deco_sign", "Sign", new Color(0.90f, 0.75f, 0.30f), Solid: false),
        new("deco_machine", "Machine", new Color(0.45f, 0.50f, 0.60f), Solid: false),
        new("deco_table", "Table", new Color(0.60f, 0.42f, 0.22f), Solid: true, Height: 18),
        new("deco_crate_wood", "Wooden crate", new Color(0.70f, 0.52f, 0.28f), Solid: true),
        new("deco_crate_steel", "Steel crate", new Color(0.55f, 0.58f, 0.62f), Solid: true),
    };

    public static DecorationDef Get(string type)
    {
        foreach (var def in All)
            if (def.Type == type)
                return def;
        return null;
    }
}

public partial class Decoration : StaticBody2D
{
    public DecorationDef Def { get; set; }

    public override void _Ready()
    {
        CollisionLayer = Def.Solid ? 1u : 0u;
        CollisionMask = 0;
        if (Def.Solid)
        {
            // Anchored to the bottom of the cell so tables are jump-on height.
            AddChild(new CollisionShape2D
            {
                Position = new Vector2(0, 16 - Def.Height / 2f),
                Shape = new RectangleShape2D { Size = new Vector2(32, Def.Height) },
            });
        }
        AddChild(new Sprite2D { Texture = Sprites.ForObject(Def.Type) });
    }
}
