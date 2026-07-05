using Godot;

namespace HackerOrSoldier;

public enum EnemyLoS { None, Row, All }

public enum EnemyAttack { Contact, Pistol, MiniGun, Plasma, Dynamite, Mine }

// One row of the README enemy table. Lives means hits to kill; the soldier
// chain instead degrades one stage per hit (Ultimate -> Super -> Soldier).
public record EnemyDef(string Type, string Name, Color Color, int Lives, int Speed,
    EnemyLoS LoS, EnemyAttack Attack,
    bool Flies = false, bool Swims = false, bool DropsAmmo = false,
    bool InfiniteLives = false, string DegradesTo = null);

public static class EnemyDefs
{
    public const string Fighter = "fighter";
    public const string Soldier = "soldier";
    public const string SuperSoldier = "super_soldier";
    public const string UltimateSoldier = "ultimate_soldier";
    public const string Drone = "drone";
    public const string Robodog = "robodog";
    public const string Turret = "turret";
    public const string Robot = "robot";
    public const string Landmine = "landmine";
    public const string ElectricEel = "electric_eel";

    public static readonly EnemyDef[] All =
    {
        new(Fighter, "Fighter", new Color(0.85f, 0.50f, 0.20f), Lives: 2, Speed: 2, EnemyLoS.Row, EnemyAttack.Contact),
        new(Soldier, "Soldier", new Color(0.40f, 0.60f, 0.30f), Lives: 1, Speed: 1, EnemyLoS.Row, EnemyAttack.Pistol,
            DropsAmmo: true),
        new(SuperSoldier, "Super soldier", new Color(0.50f, 0.65f, 0.75f), Lives: 1, Speed: 2, EnemyLoS.Row, EnemyAttack.MiniGun,
            DropsAmmo: true, DegradesTo: Soldier),
        new(UltimateSoldier, "Ultimate soldier", new Color(0.75f, 0.30f, 0.55f), Lives: 1, Speed: 3, EnemyLoS.Row, EnemyAttack.Contact,
            DegradesTo: SuperSoldier),
        new(Drone, "Drone", new Color(0.60f, 0.75f, 0.85f), Lives: 1, Speed: 1, EnemyLoS.All, EnemyAttack.Dynamite,
            Flies: true, DropsAmmo: true),
        new(Robodog, "Robodog", new Color(0.55f, 0.55f, 0.50f), Lives: 2, Speed: 3, EnemyLoS.Row, EnemyAttack.Contact,
            DropsAmmo: true),
        new(Turret, "Turret", new Color(0.70f, 0.40f, 0.40f), Lives: 1, Speed: 0, EnemyLoS.All, EnemyAttack.Plasma),
        new(Robot, "Robot", new Color(0.40f, 0.45f, 0.55f), Lives: 1, Speed: 1, EnemyLoS.Row, EnemyAttack.Plasma,
            DropsAmmo: true, InfiniteLives: true),
        new(Landmine, "Landmine", new Color(0.35f, 0.40f, 0.35f), Lives: 1, Speed: 0, EnemyLoS.None, EnemyAttack.Mine,
            InfiniteLives: true),
        new(ElectricEel, "Electric eel", new Color(0.30f, 0.80f, 0.80f), Lives: 1, Speed: 1, EnemyLoS.Row, EnemyAttack.Contact,
            Swims: true),
    };

    public static EnemyDef Get(string type)
    {
        foreach (var def in All)
            if (def.Type == type)
                return def;
        return null;
    }
}
