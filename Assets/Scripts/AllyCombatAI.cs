using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WeaponController))]
public sealed class AllyCombatAI : MonoBehaviour
{
    [SerializeField] private string enemyTag = "enemy";
    [SerializeField, Min(1f)] private float searchRadius = 20f;
    [SerializeField, Min(0.05f)] private float searchInterval = 0.25f;
    [SerializeField, Min(0f)] private float rotationSpeed = 8f;
    [SerializeField] private WeaponType startingWeapon = WeaponType.Mosin;
    [SerializeField] private Transform weaponViewAnchor;

    private WeaponController weaponController;
    private Transform currentTarget;
    private Transform weaponHand;
    private float nextSearchTime;

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

        if (currentTarget != null && !HasLineOfSight(currentTarget))
        {
            // За стеной цель не видна — отпускаем её, чтобы союзник шёл за
            // игроком, а не стрелял в стену из коровника.
            currentTarget = null;
            nextSearchTime = Time.time + searchInterval;
        }

        if (currentTarget == null || IsInvalidTarget(currentTarget))
        {
            currentTarget = null;
        }

        if (currentTarget == null && Time.time >= nextSearchTime)
        {
            currentTarget = FindNearestEnemy();
            nextSearchTime = Time.time + searchInterval;
        }

        weaponController.SetAICombatTarget(currentTarget);
        if (currentTarget == null || !CombatSettings.AimingEnabled)
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

    private Transform FindNearestEnemy()
    {
        Transform nearest = null;
        float nearestDistanceSquared = searchRadius * searchRadius;
        IReadOnlyList<EnemyAI> activeEnemies = EnemyAI.ActiveEnemyList;
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyAI enemyAI = activeEnemies[i];
            if (enemyAI == null)
            {
                continue;
            }

            Transform enemy = enemyAI.transform;
            if (enemy == transform || enemy.IsChildOf(transform) ||
                !enemy.CompareTag(enemyTag) || enemy.CompareTag("team"))
            {
                continue;
            }

            Health health = enemy.GetComponentInParent<Health>();
            if (health != null && health.IsDead)
            {
                continue;
            }

            float distanceSquared = (enemy.transform.position - transform.position).sqrMagnitude;
            // Берём только тех, кого реально видно: иначе союзник замирает
            // внутри коровника, «целясь» в немца за стеной.
            if (distanceSquared < nearestDistanceSquared && HasLineOfSight(enemy.transform))
            {
                nearest = enemy.transform;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    // Проверка прямой видимости: стены и прочая геометрия мешают стрельбе.
    private bool HasLineOfSight(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * -0.4f;
        Vector3 aim = target.position + Vector3.up * -0.4f;
        RaycastHit hit;
        if (!Physics.Linecast(origin, aim, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // Другой персонаж на линии стрельбе не мешает: WeaponController
        // пропускает своих и бьёт дальнюю цель.
        return hit.collider.GetComponentInParent<Health>() != null;
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
            weaponViewAnchor.rotation = transform.rotation;
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
