using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// Helper scene: renders a contact sheet of every weapon to the path given
// after `--` on the command line (default user://weapons.png), then quits.
// Row 1: pickup and HUD icons. Row 2: the agent firing each gun, facing
// right and left. Row 3: the armed soldiers firing. Row 4: projectiles.
// Every cell is drawn at 3x on a dark and a mid-tone half.
public partial class WeaponsPreview : Node
{
    private const int Zoom = 3;
    private const int Cell = 48 * Zoom;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        string path = args.Length > 0 ? args[0] : "user://weapons.png";

        var rows = new List<List<Image>>
        {
            Icons(),
            AgentShots(),
            EnemyShots(),
            Projectiles(),
        };
        int columns = 0;
        foreach (var row in rows)
            columns = Mathf.Max(columns, row.Count);
        var sheet = Image.CreateEmpty(columns * Cell, rows.Count * Cell, false, Image.Format.Rgba8);
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < columns; c++)
            {
                var cellRect = new Rect2I(c * Cell, r * Cell, Cell, Cell);
                sheet.FillRect(new Rect2I(cellRect.Position, new Vector2I(Cell, Cell / 2)), new Color("101018"));
                sheet.FillRect(new Rect2I(cellRect.Position + new Vector2I(0, Cell / 2), new Vector2I(Cell, Cell / 2)), new Color("5a6070"));
                if (c >= rows[r].Count)
                    continue;
                var img = rows[r][c];
                img.Resize(img.GetWidth() * Zoom, img.GetHeight() * Zoom, Image.Interpolation.Nearest);
                var at = cellRect.Position + (new Vector2I(Cell, Cell) - img.GetSize()) / 2;
                sheet.BlendRect(img, new Rect2I(Vector2I.Zero, img.GetSize()), at);
            }
        sheet.SavePng(path);
        GD.Print($"weapons sheet: {ProjectSettings.GlobalizePath(path)}");
        GetTree().Quit();
    }

    private static List<Image> Icons()
    {
        var list = new List<Image>();
        foreach (var key in new[] { "pistol", ObjectDefs.WeaponAssaultRifle, ObjectDefs.WeaponMinigun,
                     ObjectDefs.WeaponCrossbow, ObjectDefs.WeaponChainsaw, ObjectDefs.WeaponDynamite })
            list.Add(Sprites.Get(key).GetImage());
        return list;
    }

    private static List<Image> AgentShots()
    {
        var list = new List<Image>();
        foreach (var weapon in WeaponDefs.All)
            foreach (var shot in new[] { Shot.Fire, Shot.Aim })
                list.Add(BodyPoses.Texture(new BodyFrame(BodyPoses.Agent, BodyClip.Idle, 0, shot, Gun: weapon.Kind)).GetImage());
        list.Add(BodyPoses.Texture(new BodyFrame(BodyPoses.Agent, BodyClip.Run, 3, Shot.Fire, Left: true, Gun: Weapon.AssaultRifle)).GetImage());
        return list;
    }

    private static List<Image> EnemyShots()
    {
        var list = new List<Image>();
        foreach (var who in new[] { EnemyDefs.Soldier, EnemyDefs.SuperSoldier })
            foreach (var shot in new[] { Shot.Fire, Shot.Aim })
                list.Add(BodyPoses.Texture(new BodyFrame(who, BodyClip.Idle, 0, shot, Gun: BodyPoses.GunOf(who))).GetImage());
        return list;
    }

    private static List<Image> Projectiles()
    {
        var list = new List<Image>();
        var keys = new List<string>();
        foreach (var weapon in WeaponDefs.All)
            if (weapon.Projectile != null && !keys.Contains(weapon.Projectile))
                keys.Add(weapon.Projectile);
        keys.AddRange(new[] { "plasma", "rocket" });
        foreach (var key in keys)
            list.Add(Sprites.Get(key).GetImage());
        return list;
    }
}
