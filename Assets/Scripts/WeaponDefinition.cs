using UnityEngine;

public enum WeaponType
{
    Mosin,
    PPSH,
    MP40,
    Sturmgewehr,
    Panzerfaust,
    WaltherP38,
    LugerP08,
    MauserC96,
    WaltherPP,
    MP38,
    MP28,
    Kar98k,
    Gewehr41,
    Gewehr43,
    FG42,
    MG34,
    MG42,
    MG13,
    Panzerschreck,
    PzB39,
    Stielhandgranate24,
    Eierhandgranate39,
    TT33,
    Nagant1895,
    PPS43,
    PPD40,
    MosinSniper,
    SVT40,
    AVT40,
    AVS36,
    DP27,
    Maxim1910,
    SG43,
    DShK,
    DT29,
    PTRD41,
    PTRS41,
    F1,
    RGD33,
    RPG43
}

public enum WeaponFaction
{
    German,
    Soviet
}

public enum WeaponCategory
{
    Pistol,
    Revolver,
    SubmachineGun,
    Rifle,
    MachineGun,
    Launcher,
    Grenade
}

public enum WeaponFiringMode
{
    SemiAutomatic,
    Automatic,
    BoltAction,
    SingleShot,
    Thrown
}

public enum WeaponBehavior
{
    Hitscan,
    Rocket,
    Grenade
}

[System.Serializable]
public class WeaponDefinition
{
    public WeaponType type;
    public WeaponFaction faction;
    public WeaponCategory category;
    public WeaponFiringMode firingMode;
    public WeaponBehavior behavior;
    public string displayName;
    public float damage = 20f;
    public float range = 100f;
    public float fireRate = 0.12f;
    public int magazineSize = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 1.5f;
    public float spread = 0.02f;
    public Color muzzleColor = Color.red;
    public Vector3 localPosition = new Vector3(0.45f, -0.35f, 0.7f);
    public bool canAim;
    public float aimZoom = 2.2f;
    public float projectileSpeed = 22f;
    public float blastRadius = 5f;
    public float throwArc = 0.12f;
}
