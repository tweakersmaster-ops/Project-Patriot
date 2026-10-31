using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GameSaveData
{
    public int version = 1;
    public string scenePath;
    public PlayerSaveData player = new PlayerSaveData();
    public List<EntitySaveData> entities = new List<EntitySaveData>();
}

[Serializable]
public sealed class PlayerSaveData
{
    public Vector3 position;
    public Quaternion rotation;
    public float cameraPitch;
    public float health;
    public WeaponInventorySaveData weapons = new WeaponInventorySaveData();
}

[Serializable]
public sealed class EntitySaveData
{
    public string hierarchyPath;
    public Vector3 position;
    public Quaternion rotation;
    public float health;
    public WeaponInventorySaveData weapons = new WeaponInventorySaveData();
}

[Serializable]
public sealed class WeaponInventorySaveData
{
    public WeaponType equippedWeapon;
    public List<WeaponAmmoSaveData> weapons = new List<WeaponAmmoSaveData>();
}

[Serializable]
public sealed class WeaponAmmoSaveData
{
    public WeaponType type;
    public int magazineAmmo;
    public int reserveAmmo;
}
