using Godot;

namespace HackerOrSoldier;

// README: lights are strictly visual. A warm additive glow around a small
// fixture, using a radial gradient generated at runtime.
public partial class LevelLight : Node2D
{
    public override void _Ready()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.95f, 0.8f, 0.7f));
        gradient.SetColor(1, new Color(1f, 0.95f, 0.8f, 0f));
        AddChild(new PointLight2D
        {
            Texture = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 128,
                Height = 128,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f, 0f),
            },
            Energy = 0.9f,
        });
        AddChild(new Sprite2D { Texture = Sprites.Get(ObjectDefs.Light) });
    }
}
