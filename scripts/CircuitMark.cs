using Godot;

namespace HackerOrSoldier;

// The colored line along the bottom of a cell that tells which circuit or
// teleport pair an object belongs to. Its parent sits at the cell centre.
public partial class CircuitMark : ColorRect
{
    public string Number { get; set; } = "1";

    public override void _Ready()
    {
        Color = ObjectDefs.CircuitColor(Number);
        Position = new Vector2(-8, 13);
        Size = new Vector2(16, 2);
        MouseFilter = MouseFilterEnum.Ignore;
    }
}
