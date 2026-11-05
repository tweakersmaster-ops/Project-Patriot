using System.Collections.Generic;
using UnityEngine;

// Боевой ИИ союзника: ищет немцев, но стреляет только при чистой линии
// огня. Стена отменяет выстрел, а если на линии свой (тег team) или
// игрок — союзник не стреляет и меняет позицию (отходит за игроком).
[RequireComponent(typeof(WeaponController))]
public sealed class AllyCombatAI : MonoBehaviour
{
    private enum ShotCheck
    {
        Clear,
        WallBlocked,
        FriendlyBlocked
    }

    [SerializeField] private string enemyTag = "enemy";
    [SerializeField, Min(1f)] private float searchRadius = 20f;
    [SerializeField, Min(0.05f)] private float searchInterval = 0.25f;
    [SerializeField, Min(0f)] private float rotationSpeed = 8f;
    [SerializeField] private WeaponType startingWeapon = WeaponType.Mosin;
    [SerializeField] private Transform weaponViewAnchor;

    [Header("Перепозиционирование")]
    [SerializeField, Min(0.1f)] private float repositionTime = 1.8f;

    private WeaponController weaponController;
    private Transform currentTarget;
    private Transform weaponHand;
    private float nextSearchTime;
    private float repositionUntil;

    public Transform CurrentTarget => currentTarget;

    public float SearchRadius
    {
        get => searchRadius;
        set => searchRadius = Mathf.Max(1f, value);
    }

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
        weaponController.ConfigureAIWeapon(startingWeapon);
        EnsureWeaponView();
        CharacterAnimatorDriver.AttachIfPresent(gameObject);
    }

    private void OnDisable()
    {
        currentTarget = null;
        if (weaponController != null)
        {
            weaponController.SetAICombatTarget(null);
        }
    }

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            weaponController.SetAICombatTarget(null);
            return;
        }

        KeepWeaponVisible();

        bool repositioning = Time.time < repositionUntil;

        if (currentTarget != null)
        {
            ShotCheck check = CheckShot(currentTarget);
            if (check != ShotCheck.Clear)
            {
                // Стена или свой на линии — не стреляем.
                currentTarget = null;
                weaponController.SetAICombatTarget(null);

                if (check == ShotCheck.FriendlyBlocked)
                {
                    // Даём TeamFollowPlayer увести союзника на другую точку
                    // строя: пауза в поиске цели.
                    repositionUntil = Time.time + repositionTime;
                }

                nextSearchTime = Time.time + searchInterval;
            }
        }

        if (currentTarget != null && IsInvalidTarget(currentTarget))
        {
            currentTarget = null;
        }

        if (currentTarget == null && !repositioning && Time.time >= nextSearchTime)
        {
            currentTarget = FindNearestEnemy();
            nextSearchTime = Time.time + searchInterval;
        }

        weaponController.SetAICombatTarget(currentTarget);
        if (currentTarget == null)
        {
            // SetAICombatTarget прячет оружие без цели — возвращаем его в руки.
            KeepWeaponVisible();
            return;
        }

        if (!CombatSettings.AimingEnabled)
        {
            return;
        }

        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }

    // Оружие в руках у союзника видно всегда, даже без цели.
    private void KeepWeaponVisible()
    {
        if (weaponController == null)
        {
            return;
        }

        WeaponDefinition weapon = weaponController.CurrentWeapon;
        bool hasFirearm = weapon != null &&
                          weapon.type != WeaponType.Hands &&
                          weapon.type != WeaponType.Knife &&
                          weapon.behavior != WeaponBehavior.Melee;
        weaponController.SetAIWeaponDrawn(hasFirearm);
    }

    // Ищем по Health, а не по EnemyAI.ActiveEnemyList: у охраны EnemyAI
    // выключен (GuardPatrolAI), но это живые немцы, и стрелять по ним нужно.
    private Transform FindNearestEnemy()
    {
        Transform nearest = null;
        float nearestDistanceSquared = searchRadius * searchRadius;
        Health[] allHealth = FindObjectsOfType<Health>();
        for (int i = 0; i < allHealth.Length; i++)
        {
            Health health = allHealth[i];
            if (health == null || health.IsDead)
            {
                continue;
            }

            Transform enemy = health.transform;
            if (enemy == transform || enemy.IsChildOf(transform) ||
                !enemy.CompareTag(enemyTag) || enemy.CompareTag("team"))
            {
                continue;
            }

            float distanceSquared = (enemy.position - transform.position).sqrMagnitude;
            // Берём только тех, кого реально видно и по кому можно стрелять:
            // иначе союзник замирает внутри коровника, «целясь» в стену.
            if (distanceSquared < nearestDistanceSquared && CheckShot(enemy) == ShotCheck.Clear)
            {
                nearest = enemy;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    // Линия выстрела от груди до цели. Ближайшее попадание решает:
    // цель — можно, свой или игрок — нельзя, стена — нельзя.
    private ShotCheck CheckShot(Transform target)
    {
        if (target == null)
        {
            return ShotCheck.WallBlocked;
        }

        Vector3 origin = transform.position + Vector3.up * -0.6f;

        // Целимся в коллайдер цели (примерно центр туловища), а не в корень:
        // у ботов с низким корнем точка «корень − 0.3» уходит под землю,
        // и луч всегда упирался в террейн — союзник не мог выстрелить вплотную.
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider == null)
        {
            targetCollider = target.GetComponentInChildren<Collider>();
        }

        // Без коллайдера цель неубиваема — по ней нельзя стрелять.
        if (targetCollider == null)
        {
            return ShotCheck.WallBlocked;
        }

        Vector3 aimPoint = targetCollider.bounds.center;

        Vector3 direction = aimPoint - origin;
        float distance = direction.magnitude;
        if (distance < 0.01f)
        {
            return ShotCheck.Clear;
        }

        direction /= distance;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            distance + 0.5f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        RaycastHit closest = default(RaycastHit);
        bool hasClosest = false;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            // Собственные коллайдеры стрельбе не мешают.
            if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                closest = hits[i];
                hasClosest = true;
            }
        }

        if (!hasClosest)
        {
            return ShotCheck.Clear;
        }

        Health health = closest.collider.GetComponentInParent<Health>();
        if (health == null)
        {
            // Стена, забор, техника — пуля не пройдёт.
            return ShotCheck.WallBlocked;
        }

        if (IsTargetCollider(closest.collider.transform, target))
        {
            return ShotCheck.Clear;
        }

        if (closest.collider.CompareTag("team") || closest.collider.CompareTag("Player") ||
            closest.collider.GetComponentInParent<PlayerController>() != null)
        {
            return ShotCheck.FriendlyBlocked;
        }

        // Другой персонаж (в том числе чужой немец) физически закрывает линию.
        return ShotCheck.WallBlocked;
    }

    private static bool IsTargetCollider(Transform colliderTransform, Transform target)
    {
        if (colliderTransform == null || target == null)
        {
            return false;
        }

        return colliderTransform == target || colliderTransform.IsChildOf(target);
    }

    private bool IsInvalidTarget(Transform target)
    {
        Health health = target.GetComponentInParent<Health>();
        return !target.gameObject.activeInHierarchy ||
               !target.CompareTag(enemyTag) ||
               (health != null && health.IsDead) ||
               (target.position - transform.position).sqrMagnitude > searchRadius * searchRadius;
    }

    private void LateUpdate()
    {
        // Оружие держим в правой руке: позицию берём с кости кисти,
        // поворот — вперёд персонажа (куда он целится).
        if (weaponViewAnchor != null && weaponHand != null)
        {
            weaponViewAnchor.position = weaponHand.position;
            weaponViewAnchor.rotation = transform.rotation * Quaternion.Euler(-18f, 0f, 0f);
        }
    }

    private void EnsureWeaponView()
    {
        Animator animator = FindHumanoidAnimator();
        if (animator != null)
        {
            weaponHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (weaponViewAnchor == null)
        {
            Transform existingAnchor = transform.Find("WeaponViewAnchor");
            if (existingAnchor != null)
            {
                weaponViewAnchor = existingAnchor;
            }
            else
            {
                GameObject anchor = new GameObject("WeaponViewAnchor");
                weaponViewAnchor = anchor.transform;
                weaponViewAnchor.SetParent(transform, false);
                weaponViewAnchor.localPosition = new Vector3(0f, 1.15f, 0.15f);
            }
        }

        if (weaponHand != null)
        {
            // Крепление к кисти: без IK-констрейнтов оружие «висит в воздухе».
            weaponViewAnchor.SetParent(weaponHand, false);
            weaponViewAnchor.localPosition = new Vector3(0.02f, -0.02f, 0.04f);
            weaponViewAnchor.localRotation = Quaternion.identity;
        }

        if (weaponViewAnchor.GetComponent<WeaponView>() == null)
        {
            weaponViewAnchor.gameObject.AddComponent<WeaponView>();
        }
    }

    private Animator FindHumanoidAnimator()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i].avatar != null && animators[i].avatar.isHuman)
            {
                return animators[i];
            }
        }

        return null;
    }
}
