using System.Collections.Generic;

namespace HackerOrSoldier;

// Every weapon a character can hold. The agent switches between them by
// picking them up; each soldier type carries one.
public enum Weapon { None, Pistol, AssaultRifle, Minigun, Crossbow, Chainsaw, Dynamite }

// What the agent's weapon does when fired. Icon is the sprite on the HUD;
// Projectile is the sprite each shot flies as (null for the melee chainsaw
// and the placed dynamite). README: pistol 1 bullet, mini gun 2, assault
// rifle 3 per shot. Pistol and rifle share a bullet because the burst
// already tells them apart; the minigun spits tracers and the crossbow bolts.
public sealed record WeaponDef(Weapon Kind, string Pickup, string Icon, string Projectile, int Burst, Sound? Shot);

public static class WeaponDefs
{
    public static readonly WeaponDef[] All =
    {
        new(Weapon.Pistol, null, "pistol", "bullet", 1, Sound.ShotPistol),
        new(Weapon.AssaultRifle, ObjectDefs.WeaponAssaultRifle, ObjectDefs.WeaponAssaultRifle, "bullet", 3, Sound.ShotRifle),
        new(Weapon.Minigun, ObjectDefs.WeaponMinigun, ObjectDefs.WeaponMinigun, "tracer", 2, Sound.ShotMinigun),
        new(Weapon.Crossbow, ObjectDefs.WeaponCrossbow, ObjectDefs.WeaponCrossbow, "bolt", 1, Sound.Crossbow),
        new(Weapon.Chainsaw, ObjectDefs.WeaponChainsaw, ObjectDefs.WeaponChainsaw, null, 0, Sound.Chainsaw),
        new(Weapon.Dynamite, ObjectDefs.WeaponDynamite, ObjectDefs.WeaponDynamite, null, 0, null),
    };

    private static readonly Dictionary<Weapon, WeaponDef> ByKind = new();
    private static readonly Dictionary<string, WeaponDef> ByPickup = new();

    static WeaponDefs()
    {
        foreach (var def in All)
        {
            ByKind[def.Kind] = def;
            if (def.Pickup != null)
                ByPickup[def.Pickup] = def;
        }
    }

    public static WeaponDef Get(Weapon kind) => ByKind[kind];

    // The weapon a pickup object equips, or null for any other object.
    public static WeaponDef ForPickup(string objectType) => ByPickup.GetValueOrDefault(objectType);
}
