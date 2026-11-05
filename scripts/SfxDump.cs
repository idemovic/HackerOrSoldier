using Godot;

namespace HackerOrSoldier;

// Writes every sound effect to user://sfx/<name>.wav for auditioning:
//   godot --headless --path . res://scenes/sfx_dump.tscn
public partial class SfxDump : Node
{
    public override void _Ready()
    {
        foreach (var (name, bytes, peak) in Sfx.DumpWavs("user://sfx"))
            GD.Print($"{name}: {bytes} bytes, peak {peak:0.00}");
        GD.Print($"Wrote {ProjectSettings.GlobalizePath("user://sfx")}");
        GetTree().Quit();
    }
}
