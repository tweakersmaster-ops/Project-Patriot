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
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = enemy.transform;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    private bool IsInvalidTarget(Transform target)
    {
        Health health = target.GetComponentInParent<Health>();
        return !target.gameObject.activeInHierarchy ||
               !target.CompareTag(enemyTag) ||
               (health != null && health.IsDead) ||
               (target.position - transform.position).sqrMagnitude > searchRadius * searchRadius;
    }

    private void EnsureWeaponView()
    {
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

        if (weaponViewAnchor.GetComponent<WeaponView>() == null)
        {
            weaponViewAnchor.gameObject.AddComponent<WeaponView>();
        }
    }
}
