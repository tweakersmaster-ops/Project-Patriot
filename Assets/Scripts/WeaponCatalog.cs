using UnityEngine;

public static class WeaponCatalog
{
    private static readonly WeaponDefinition[] Definitions =
    {
        Create(WeaponType.Mosin, WeaponFaction.Soviet, WeaponCategory.Rifle, WeaponFiringMode.BoltAction, WeaponBehavior.Hitscan, "Mosin-Nagant", 50f, 180f, 1.2f, 5, 24, 2.2f, 0.04f, true, 22f, 5f, 0.12f, 2.7f),
        Create(WeaponType.PPSH, WeaponFaction.Soviet, WeaponCategory.SubmachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "PPSh-41", 22f, 90f, 0.09f, 50, 150, 1.8f, 0.06f),
        Create(WeaponType.MP40, WeaponFaction.German, WeaponCategory.SubmachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "MP 40", 18f, 70f, 0.08f, 32, 96, 1.5f, 0.05f),
        Create(WeaponType.Sturmgewehr, WeaponFaction.German, WeaponCategory.Rifle, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "StG 44", 26f, 120f, 0.1f, 30, 120, 1.7f, 0.025f, true),
        Create(WeaponType.Panzerfaust, WeaponFaction.German, WeaponCategory.Launcher, WeaponFiringMode.SingleShot, WeaponBehavior.Rocket, "Panzerfaust", 120f, 35f, 1.2f, 1, 3, 2.5f, 0f, false, 24f, 5.5f),
        Create(WeaponType.WaltherP38, WeaponFaction.German, WeaponCategory.Pistol, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "Walther P38", 24f, 45f, 0.24f, 8, 48, 1.4f, 0.035f),
        Create(WeaponType.LugerP08, WeaponFaction.German, WeaponCategory.Pistol, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "Luger P08", 23f, 45f, 0.23f, 8, 40, 1.5f, 0.035f),
        Create(WeaponType.MauserC96, WeaponFaction.German, WeaponCategory.Pistol, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "Mauser C96", 26f, 55f, 0.2f, 10, 50, 1.8f, 0.03f),
        Create(WeaponType.WaltherPP, WeaponFaction.German, WeaponCategory.Pistol, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "Walther PP / PPK", 19f, 35f, 0.22f, 7, 35, 1.3f, 0.045f),
        Create(WeaponType.MP38, WeaponFaction.German, WeaponCategory.SubmachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "MP 38", 18f, 70f, 0.09f, 32, 96, 1.6f, 0.055f),
        Create(WeaponType.MP28, WeaponFaction.German, WeaponCategory.SubmachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "MP 28", 20f, 75f, 0.1f, 32, 96, 1.8f, 0.06f),
        Create(WeaponType.Kar98k, WeaponFaction.German, WeaponCategory.Rifle, WeaponFiringMode.BoltAction, WeaponBehavior.Hitscan, "Karabiner 98k", 52f, 190f, 1.15f, 5, 25, 2.1f, 0.035f, true),
        Create(WeaponType.Gewehr41, WeaponFaction.German, WeaponCategory.Rifle, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "Gewehr 41", 42f, 150f, 0.35f, 10, 50, 2.1f, 0.045f, true),
        Create(WeaponType.Gewehr43, WeaponFaction.German, WeaponCategory.Rifle, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "Gewehr 43 / K43", 40f, 160f, 0.27f, 10, 60, 1.9f, 0.04f, true),
        Create(WeaponType.FG42, WeaponFaction.German, WeaponCategory.Rifle, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "FG 42", 34f, 145f, 0.095f, 20, 100, 2f, 0.045f, true),
        Create(WeaponType.MG34, WeaponFaction.German, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "MG 34", 28f, 150f, 0.075f, 50, 200, 3.4f, 0.055f),
        Create(WeaponType.MG42, WeaponFaction.German, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "MG 42", 27f, 145f, 0.055f, 50, 200, 3.5f, 0.065f),
        Create(WeaponType.MG13, WeaponFaction.German, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "MG 13", 26f, 125f, 0.11f, 25, 100, 2.7f, 0.055f),
        Create(WeaponType.Panzerschreck, WeaponFaction.German, WeaponCategory.Launcher, WeaponFiringMode.SingleShot, WeaponBehavior.Rocket, "Panzerschreck", 145f, 65f, 1.3f, 1, 4, 2.8f, 0.015f, false, 32f, 6f),
        Create(WeaponType.PzB39, WeaponFaction.German, WeaponCategory.Rifle, WeaponFiringMode.SingleShot, WeaponBehavior.Hitscan, "PzB 39", 95f, 180f, 1.3f, 1, 10, 2.4f, 0.035f, true),
        Create(WeaponType.Stielhandgranate24, WeaponFaction.German, WeaponCategory.Grenade, WeaponFiringMode.Thrown, WeaponBehavior.Grenade, "Stielhandgranate 24", 85f, 35f, 1.1f, 1, 3, 1.1f, 0.02f, false, 14f, 6.5f, 0.3f),
        Create(WeaponType.Eierhandgranate39, WeaponFaction.German, WeaponCategory.Grenade, WeaponFiringMode.Thrown, WeaponBehavior.Grenade, "Eierhandgranate 39", 100f, 32f, 1f, 1, 3, 1f, 0.025f, false, 16f, 5.5f, 0.32f),
        Create(WeaponType.TT33, WeaponFaction.Soviet, WeaponCategory.Pistol, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "TT-33", 28f, 55f, 0.21f, 8, 48, 1.4f, 0.03f),
        Create(WeaponType.Nagant1895, WeaponFaction.Soviet, WeaponCategory.Revolver, WeaponFiringMode.SingleShot, WeaponBehavior.Hitscan, "Nagant M1895", 25f, 40f, 0.42f, 7, 35, 2.1f, 0.04f),
        Create(WeaponType.PPS43, WeaponFaction.Soviet, WeaponCategory.SubmachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "PPS-43", 20f, 80f, 0.09f, 35, 140, 1.5f, 0.045f),
        Create(WeaponType.PPD40, WeaponFaction.Soviet, WeaponCategory.SubmachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "PPD-40", 22f, 85f, 0.075f, 71, 142, 2.5f, 0.065f),
        Create(WeaponType.MosinSniper, WeaponFaction.Soviet, WeaponCategory.Rifle, WeaponFiringMode.BoltAction, WeaponBehavior.Hitscan, "Mosin sniper (PU)", 58f, 240f, 1.35f, 5, 20, 2.3f, 0.012f, true, 22f, 5f, 0.12f, 3f),
        Create(WeaponType.SVT40, WeaponFaction.Soviet, WeaponCategory.Rifle, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "SVT-40", 40f, 155f, 0.25f, 10, 60, 1.8f, 0.045f, true),
        Create(WeaponType.AVT40, WeaponFaction.Soviet, WeaponCategory.Rifle, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "AVT-40", 38f, 145f, 0.11f, 10, 60, 1.9f, 0.075f, true),
        Create(WeaponType.AVS36, WeaponFaction.Soviet, WeaponCategory.Rifle, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "AVS-36", 38f, 145f, 0.12f, 15, 60, 2.2f, 0.075f, true),
        Create(WeaponType.DP27, WeaponFaction.Soviet, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "DP-27", 28f, 140f, 0.11f, 47, 141, 3.2f, 0.055f),
        Create(WeaponType.Maxim1910, WeaponFaction.Soviet, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "Maxim M1910/30", 28f, 160f, 0.085f, 100, 300, 4f, 0.05f),
        Create(WeaponType.SG43, WeaponFaction.Soviet, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "SG-43", 28f, 155f, 0.09f, 250, 500, 4.5f, 0.055f),
        Create(WeaponType.DShK, WeaponFaction.Soviet, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "DShK", 45f, 200f, 0.12f, 50, 150, 4f, 0.05f),
        Create(WeaponType.DT29, WeaponFaction.Soviet, WeaponCategory.MachineGun, WeaponFiringMode.Automatic, WeaponBehavior.Hitscan, "DT-29", 27f, 130f, 0.1f, 63, 189, 3f, 0.055f),
        Create(WeaponType.PTRD41, WeaponFaction.Soviet, WeaponCategory.Rifle, WeaponFiringMode.SingleShot, WeaponBehavior.Hitscan, "PTRD-41", 110f, 200f, 1.55f, 1, 10, 2.8f, 0.025f, true),
        Create(WeaponType.PTRS41, WeaponFaction.Soviet, WeaponCategory.Rifle, WeaponFiringMode.SemiAutomatic, WeaponBehavior.Hitscan, "PTRS-41", 100f, 190f, 0.65f, 5, 20, 2.8f, 0.035f, true),
        Create(WeaponType.F1, WeaponFaction.Soviet, WeaponCategory.Grenade, WeaponFiringMode.Thrown, WeaponBehavior.Grenade, "F-1", 120f, 30f, 1.1f, 1, 3, 1.1f, 0.025f, false, 13f, 8f, 0.32f),
        Create(WeaponType.RGD33, WeaponFaction.Soviet, WeaponCategory.Grenade, WeaponFiringMode.Thrown, WeaponBehavior.Grenade, "RGD-33", 95f, 34f, 1.2f, 1, 3, 1.2f, 0.025f, false, 13f, 6f, 0.3f),
        Create(WeaponType.RPG43, WeaponFaction.Soviet, WeaponCategory.Grenade, WeaponFiringMode.Thrown, WeaponBehavior.Grenade, "RPG-43", 160f, 24f, 1.35f, 1, 2, 1.3f, 0.025f, false, 15f, 4.5f, 0.24f)
    };

    public static WeaponDefinition[] GetAll()
    {
        WeaponDefinition[] copy = new WeaponDefinition[Definitions.Length];
        for (int i = 0; i < Definitions.Length; i++)
        {
            copy[i] = Copy(Definitions[i]);
        }

        return copy;
    }

    public static WeaponDefinition Get(WeaponType type)
    {
        for (int i = 0; i < Definitions.Length; i++)
        {
            if (Definitions[i].type == type)
            {
                return Copy(Definitions[i]);
            }
        }

        Debug.LogError("No weapon definition exists for " + type + ".");
        return null;
    }

    private static WeaponDefinition Create(
        WeaponType type,
        WeaponFaction faction,
        WeaponCategory category,
        WeaponFiringMode firingMode,
        WeaponBehavior behavior,
        string displayName,
        float damage,
        float range,
        float fireRate,
        int magazineSize,
        int reserveAmmo,
        float reloadTime,
        float spread,
        bool canAim = false,
        float projectileSpeed = 22f,
        float blastRadius = 5f,
        float throwArc = 0.12f,
        float aimZoom = 2.2f)
    {
        return new WeaponDefinition
        {
            type = type,
            faction = faction,
            category = category,
            firingMode = firingMode,
            behavior = behavior,
            displayName = displayName,
            damage = damage,
            range = range,
            fireRate = fireRate,
            magazineSize = magazineSize,
            reserveAmmo = reserveAmmo,
            reloadTime = reloadTime,
            spread = spread,
            muzzleColor = faction == WeaponFaction.German
                ? new Color(1f, 0.72f, 0.28f)
                : new Color(1f, 0.42f, 0.18f),
            localPosition = new Vector3(0.45f, -0.35f, 0.75f),
            canAim = canAim,
            aimZoom = aimZoom,
            projectileSpeed = projectileSpeed,
            blastRadius = blastRadius,
            throwArc = throwArc
        };
    }

    private static WeaponDefinition Copy(WeaponDefinition source)
    {
        return new WeaponDefinition
        {
            type = source.type,
            faction = source.faction,
            category = source.category,
            firingMode = source.firingMode,
            behavior = source.behavior,
            displayName = source.displayName,
            damage = source.damage,
            range = source.range,
            fireRate = source.fireRate,
            magazineSize = source.magazineSize,
            reserveAmmo = source.reserveAmmo,
            reloadTime = source.reloadTime,
            spread = source.spread,
            muzzleColor = source.muzzleColor,
            localPosition = source.localPosition,
            canAim = source.canAim,
            aimZoom = source.aimZoom,
            projectileSpeed = source.projectileSpeed,
            blastRadius = source.blastRadius,
            throwArc = source.throwArc
        };
    }
}
