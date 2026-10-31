using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    private sealed class AmmoState
    {
        public int magazine;
        public int reserve;
    }

    private Vector3 GetAimPoint(Transform target)
    {
        CharacterController targetController = target.GetComponent<CharacterController>();
        if (targetController != null)
        {
            return target.transform.TransformPoint(targetController.center);
        }

        return target.position + Vector3.up * 0.9f;
    }

    [SerializeField] private List<WeaponDefinition> weapons = new List<WeaponDefinition>();
    [SerializeField] private int currentWeaponIndex = 0;
    [Header("Стартовый набор (если пусто — руки и нож)")]
    [SerializeField] private List<WeaponType> startingWeapons = new List<WeaponType>();
    [SerializeField] private WeaponType startingWeapon = WeaponType.Hands;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField, Min(1f)] private float mosinAimZoom = 2.7f;

    private float nextShotTime;
    private float defaultCameraFieldOfView;
    private int currentAmmo;
    private int reserveAmmo;
    private bool isReloading;
    private bool hasEquippedWeapon;
    private bool hasStarted;
    private bool isAIControlled;
    private WeaponType aiWeaponType = WeaponType.Mosin;
    private Transform aiTarget;
    private bool weaponIsDrawn;
    private readonly Dictionary<WeaponType, AmmoState> ammoByWeapon = new Dictionary<WeaponType, AmmoState>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    public event Action<WeaponDefinition> OnWeaponSwitched;
    public event Action<WeaponDefinition> OnWeaponFired;
    public event Action<bool> OnAimChanged;
    public event Action<bool> OnWeaponDrawChanged;
    public event Action OnReloadStarted;

    public WeaponDefinition CurrentWeapon => weapons.Count > 0 ? weapons[currentWeaponIndex] : null;
    public int CurrentAmmo => currentAmmo;
    public int CurrentReserveAmmo => reserveAmmo;
    public bool IsAiming { get; private set; }
    public bool IsAIControlled => isAIControlled;
    public bool IsWeaponDrawn => weaponIsDrawn;

    private void Start()
    {
        hasStarted = true;
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera != null)
        {
            defaultCameraFieldOfView = playerCamera.fieldOfView;
        }
        ResetLoadout();
    }

    public void ResetLoadout()
    {
        StopAllCoroutines();
        isReloading = false;
        hasEquippedWeapon = false;
        currentWeaponIndex = 0;
        SetAiming(false);
        BuildDefaultWeapons();
        EnsureFirePoint();
        EquipStartingWeapon();
    }

    private void EquipStartingWeapon()
    {
        EquipWeapon(isAIControlled ? aiWeaponType : startingWeapon);
        if (!hasEquippedWeapon && weapons.Count > 0)
        {
            EquipWeapon(weapons[0].type);
        }
    }

    public void ConfigureAIWeapon(WeaponType weaponType)
    {
        isAIControlled = true;
        aiWeaponType = weaponType;
        SetAICombatTarget(null);
        if (hasStarted)
        {
            ResetLoadout();
        }
    }

    public void SetAICombatTarget(Transform target)
    {
        if (!isAIControlled)
        {
            return;
        }

        aiTarget = target;
        bool shouldDrawWeapon = target != null;
        if (weaponIsDrawn != shouldDrawWeapon)
        {
            weaponIsDrawn = shouldDrawWeapon;
            OnWeaponDrawChanged?.Invoke(weaponIsDrawn);
        }
    }

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            SetAiming(false);
            UpdateAimZoom();
            return;
        }

        if (isAIControlled)
        {
            SetAiming(aiTarget != null && CombatSettings.AimingEnabled && CurrentWeapon != null && CurrentWeapon.canAim && !isReloading);
            UpdateAimZoom();
            if (aiTarget != null && !isReloading && Time.time >= nextShotTime)
            {
                Fire();
            }

            return;
        }

        if (weapons.Count == 0)
        {
            return;
        }

        SetAiming(CombatSettings.AimingEnabled &&
                  KeyBindings.GetKey(BindableAction.Aim) &&
                  CurrentWeapon != null &&
                  CurrentWeapon.canAim &&
                  !isReloading);
        UpdateAimZoom();

        if (KeyBindings.GetKeyDown(BindableAction.Slot1))
        {
            EquipWeapon(WeaponType.Mosin);
        }
        else if (KeyBindings.GetKeyDown(BindableAction.Slot2))
        {
            EquipWeapon(WeaponType.PPSH);
        }
        else if (KeyBindings.GetKeyDown(BindableAction.Slot3))
        {
            EquipWeapon(WeaponType.MP40);
        }
        else if (KeyBindings.GetKeyDown(BindableAction.Slot4))
        {
            EquipWeapon(WeaponType.Sturmgewehr);
        }
        else if (KeyBindings.GetKeyDown(BindableAction.Slot5))
        {
            EquipWeapon(WeaponType.Panzerfaust);
        }
        else if (KeyBindings.GetKeyDown(BindableAction.Slot6))
        {
            EquipWeapon(WeaponType.Hands);
        }
        else if (KeyBindings.GetKeyDown(BindableAction.Slot7))
        {
            EquipWeapon(WeaponType.Knife);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f)
        {
            CycleWeapon(1);
        }
        else if (scroll < 0f)
        {
            CycleWeapon(-1);
        }

        if (KeyBindings.GetKeyDown(BindableAction.Reload) &&
            !isReloading &&
            CurrentWeapon != null &&
            CurrentWeapon.behavior != WeaponBehavior.Melee)
        {
            StartCoroutine(ReloadWeapon());
        }

        bool firing = CurrentWeapon != null &&
                      (CurrentWeapon.firingMode == WeaponFiringMode.Automatic
                          ? KeyBindings.GetKey(BindableAction.Fire)
                          : KeyBindings.GetKeyDown(BindableAction.Fire));
        if (firing && !isReloading && Time.time >= nextShotTime)
        {
            Fire();
        }
    }

    public void PickUpWeapon(WeaponType type)
    {
        int index = GetWeaponIndex(type);

        if (index >= 0)
        {
            WeaponDefinition ownedWeapon = weapons[index];
            EquipWeapon(type);
            if (ownedWeapon.behavior == WeaponBehavior.Melee)
            {
                return;
            }

            AmmoState ownedAmmo = ammoByWeapon[type];
            ownedAmmo.reserve = Mathf.Min(
                ownedWeapon.reserveAmmo,
                ownedAmmo.reserve + ownedWeapon.magazineSize);
            reserveAmmo = ownedAmmo.reserve;
            if (currentAmmo <= 0)
            {
                StartCoroutine(ReloadWeapon());
            }
            return;
        }

        WeaponDefinition newWeapon = WeaponCatalog.Get(type);
        if (newWeapon == null)
        {
            return;
        }

        weapons.Add(newWeapon);
        ammoByWeapon[type] = new AmmoState
        {
            magazine = newWeapon.magazineSize,
            reserve = newWeapon.reserveAmmo
        };
        EquipWeapon(type);
    }

    public WeaponInventorySaveData CaptureSaveData()
    {
        SaveCurrentAmmo();
        WeaponInventorySaveData saveData = new WeaponInventorySaveData();
        if (CurrentWeapon != null)
        {
            saveData.equippedWeapon = CurrentWeapon.type;
        }

        foreach (WeaponDefinition weapon in weapons)
        {
            if (ammoByWeapon.TryGetValue(weapon.type, out AmmoState ammo))
            {
                saveData.weapons.Add(new WeaponAmmoSaveData
                {
                    type = weapon.type,
                    magazineAmmo = ammo.magazine,
                    reserveAmmo = ammo.reserve
                });
            }
        }

        return saveData;
    }

    public void RestoreSaveData(WeaponInventorySaveData saveData)
    {
        if (saveData == null || saveData.weapons == null)
        {
            Debug.LogError("Cannot restore weapon inventory because save data is missing.", this);
            return;
        }

        weapons.Clear();
        ammoByWeapon.Clear();
        hasEquippedWeapon = false;
        foreach (WeaponAmmoSaveData savedWeapon in saveData.weapons)
        {
            if (savedWeapon == null)
            {
                continue;
            }

            WeaponDefinition definition = WeaponCatalog.Get(savedWeapon.type);
            if (definition == null)
            {
                continue;
            }

            weapons.Add(definition);
            ammoByWeapon[definition.type] = new AmmoState
            {
                magazine = Mathf.Clamp(savedWeapon.magazineAmmo, 0, definition.magazineSize),
                reserve = Mathf.Clamp(savedWeapon.reserveAmmo, 0, definition.reserveAmmo)
            };
        }

        if (weapons.Count == 0)
        {
            Debug.LogWarning("Save contained no recognized weapons; the default loadout was restored.", this);
            BuildDefaultWeapons();
            EnsureFirePoint();
            EquipStartingWeapon();
            return;
        }

        EquipWeapon(saveData.equippedWeapon);
        if (!hasEquippedWeapon)
        {
            EquipWeapon(weapons[0].type);
        }
    }

    public void EquipWeapon(WeaponType type)
    {
        int index = GetWeaponIndex(type);
        if (index >= 0)
        {
            if (IsAiming)
            {
                SetAiming(false);
            }

            if (hasEquippedWeapon)
            {
                SaveCurrentAmmo();
            }

            currentWeaponIndex = index;
            if (!ammoByWeapon.TryGetValue(type, out AmmoState ammoState))
            {
                ammoState = new AmmoState
                {
                    magazine = CurrentWeapon.magazineSize,
                    reserve = CurrentWeapon.reserveAmmo
                };
                ammoByWeapon[type] = ammoState;
            }

            currentAmmo = ammoState.magazine;
            reserveAmmo = ammoState.reserve;
            hasEquippedWeapon = true;
            OnWeaponSwitched?.Invoke(CurrentWeapon);
        }
    }

    private void BuildDefaultWeapons()
    {
        weapons.Clear();
        ammoByWeapon.Clear();
        if (isAIControlled)
        {
            WeaponDefinition weapon = WeaponCatalog.Get(aiWeaponType);
            if (weapon != null)
            {
                weapons.Add(weapon);
            }
        }
        else
        {
            if (startingWeapons.Count > 0)
            {
                foreach (WeaponType type in startingWeapons)
                {
                    WeaponDefinition weapon = WeaponCatalog.Get(type);
                    if (weapon != null)
                    {
                        weapons.Add(weapon);
                    }
                }
            }

            if (weapons.Count == 0)
            {
                weapons.Add(WeaponCatalog.Get(WeaponType.Hands));
                weapons.Add(WeaponCatalog.Get(WeaponType.Knife));
                weapons.RemoveAll(weapon => weapon == null);
            }
        }

        foreach (WeaponDefinition weapon in weapons)
        {
            ammoByWeapon[weapon.type] = new AmmoState
            {
                magazine = weapon.magazineSize,
                reserve = weapon.reserveAmmo
            };
        }
    }

    private int GetWeaponIndex(WeaponType type)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].type == type)
            {
                return i;
            }
        }

        return -1;
    }

    private void EnsureFirePoint()
    {
        if (firePoint != null)
        {
            return;
        }

        GameObject muzzle = new GameObject("WeaponMuzzle");
        muzzle.transform.SetParent(transform);
        muzzle.transform.localPosition = new Vector3(0.5f, 0.2f, 1f);
        firePoint = muzzle.transform;
    }

    private void SetAiming(bool aiming)
    {
        if (IsAiming == aiming)
        {
            return;
        }

        IsAiming = aiming;
        OnAimChanged?.Invoke(aiming);
    }

    private void UpdateAimZoom()
    {
        if (playerCamera == null)
        {
            return;
        }

        float zoom = CurrentWeapon != null ? CurrentWeapon.aimZoom : mosinAimZoom;
        float targetFieldOfView = IsAiming
            ? defaultCameraFieldOfView / Mathf.Max(zoom, 1f)
            : defaultCameraFieldOfView;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFieldOfView,
            1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
    }

    private void Fire()
    {
        if (CurrentWeapon == null ||
            (CurrentWeapon.behavior != WeaponBehavior.Melee && currentAmmo <= 0))
        {
            return;
        }

        Vector3 origin = isAIControlled
            ? transform.position + Vector3.up * 1.25f
            : playerCamera != null
                ? playerCamera.transform.position
                : transform.position + transform.forward;
        Vector3 direction;
        if (isAIControlled && aiTarget != null && CombatSettings.AimingEnabled)
        {
            direction = (GetAimPoint(aiTarget) - origin).normalized;
        }
        else
        {
            direction = playerCamera != null && !isAIControlled
                ? playerCamera.transform.forward
                : transform.forward;
        }

        direction += UnityEngine.Random.insideUnitSphere * CurrentWeapon.spread;
        direction.Normalize();

        bool usesAmmo = CurrentWeapon.behavior != WeaponBehavior.Melee;
        if (CurrentWeapon.behavior == WeaponBehavior.Melee)
        {
            FireMelee(origin, direction);
        }
        else if (CurrentWeapon.behavior != WeaponBehavior.Hitscan)
        {
            if (CurrentWeapon.behavior == WeaponBehavior.Grenade)
            {
                direction = (direction + Vector3.up * CurrentWeapon.throwArc).normalized;
            }

            WeaponProjectile.Launch(origin + direction * 0.5f, direction, GetComponentInParent<Health>(), CurrentWeapon);
        }
        else
        {
            FireHitscan(origin, direction);
        }

        if (usesAmmo)
        {
            currentAmmo--;
            SaveCurrentAmmo();
        }

        nextShotTime = Time.time + CurrentWeapon.fireRate;
        OnWeaponFired?.Invoke(CurrentWeapon);

        if (usesAmmo && currentAmmo <= 0)
        {
            StartCoroutine(ReloadWeapon());
        }
    }

    private void FireMelee(Vector3 origin, Vector3 direction)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            hitBuffer,
            CurrentWeapon.range,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        Health ownerHealth = GetComponentInParent<Health>();
        int closestHitIndex = -1;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            Health targetHealth = hit.collider.GetComponentInParent<Health>();
            if (targetHealth == ownerHealth || hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            closestHitIndex = i;
        }

        if (closestHitIndex >= 0)
        {
            RaycastHit closestHit = hitBuffer[closestHitIndex];
            Health targetHealth = closestHit.collider.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(CurrentWeapon.damage);
                Debug.DrawLine(origin, closestHit.point, Color.red, 0.15f);
            }
        }
    }

    private void FireHitscan(Vector3 origin, Vector3 direction)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            hitBuffer,
            CurrentWeapon.range,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);
        Health shooterHealth = GetComponentInParent<Health>();
        int closestHitIndex = -1;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            CharacterController hitController = hit.collider as CharacterController;
            EnemyRagdoll hitRagdoll = hitController != null ? hitController.GetComponent<EnemyRagdoll>() : null;
            if ((hitRagdoll != null && hitRagdoll.HasHumanoidHitboxes) ||
                (shooterHealth != null && hit.collider.GetComponentInParent<Health>() == shooterHealth) ||
                hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            closestHitIndex = i;
        }

        if (closestHitIndex >= 0)
        {
            RaycastHit closestHit = hitBuffer[closestHitIndex];
            Debug.DrawLine(origin, closestHit.point, Color.red, 0.15f);
            Health targetHealth = closestHit.collider.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(CurrentWeapon.damage);
            }
        }
        else
        {
            Debug.DrawRay(origin, direction * CurrentWeapon.range, Color.green, 0.15f);
        }

    }

    private void CycleWeapon(int direction)
    {
        if (weapons.Count < 2)
        {
            return;
        }

        int nextIndex = (currentWeaponIndex + direction + weapons.Count) % weapons.Count;
        EquipWeapon(weapons[nextIndex].type);
    }

    private IEnumerator ReloadWeapon()
    {
        if (CurrentWeapon == null || isReloading)
        {
            yield break;
        }

        WeaponDefinition reloadingWeapon = CurrentWeapon;
        isReloading = true;
        OnReloadStarted?.Invoke();
        yield return new WaitForSeconds(reloadingWeapon.reloadTime);

        if (!ammoByWeapon.TryGetValue(reloadingWeapon.type, out AmmoState ammoState))
        {
            isReloading = false;
            yield break;
        }

        int ammoNeeded = reloadingWeapon.magazineSize - ammoState.magazine;
        int ammoToLoad = Mathf.Min(ammoNeeded, ammoState.reserve);

        ammoState.magazine += ammoToLoad;
        ammoState.reserve -= ammoToLoad;
        if (CurrentWeapon != null && CurrentWeapon.type == reloadingWeapon.type)
        {
            currentAmmo = ammoState.magazine;
            reserveAmmo = ammoState.reserve;
        }
        isReloading = false;
    }

    private void SaveCurrentAmmo()
    {
        if (CurrentWeapon == null)
        {
            return;
        }

        ammoByWeapon[CurrentWeapon.type] = new AmmoState
        {
            magazine = currentAmmo,
            reserve = reserveAmmo
        };
    }
}
