using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class TeamFollowPlayer : MonoBehaviour
{
    private static readonly List<TeamFollowPlayer> ActiveFollowers = new List<TeamFollowPlayer>();

    public float followSpeed = 3.5f;
    public float followDistance = 2f;
    public float spacing = 1.25f;
    public float rotationSpeed = 8f;
    public float targetSearchRadius = 15f;
    public float targetSearchInterval = 0.35f;
    public float gravity = -9.81f;

    private float verticalVelocity;

    private CharacterController controller;
    private NavMeshAgent agent;
    private bool useAgent;
    private PlayerController player;
    private AllyCombatAI allyCombatAI;
    private EnemyAI teamAI;
    private EnemyAI currentEnemy;
    private float nextTargetSearchTime;

    private void OnEnable()
    {
        if (!ActiveFollowers.Contains(this))
        {
            ActiveFollowers.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveFollowers.Remove(this);
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();

        // Союзник идёт по запечённому NavMesh, если под ним есть навигация.
        // AllySoldierAI уже поднял агента; здесь только проверяем состояние.
        useAgent = agent != null && agent.enabled && agent.isOnNavMesh;
        if (useAgent)
        {
            agent.updateRotation = false;
            agent.speed = followSpeed;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        CharacterAnimatorDriver.AttachIfPresent(gameObject);
        allyCombatAI = GetComponent<AllyCombatAI>();
        teamAI = GetComponent<EnemyAI>();
        GroundedEntity groundedEntity = GetComponent<GroundedEntity>();
        if (groundedEntity != null)
        {
            groundedEntity.enabled = false;
        }

        if (teamAI != null)
        {
            teamAI.detectionRange = targetSearchRadius;
            teamAI.target = null;
        }
    }

    private void Update()
    {
        if (!useAgent && (controller == null || !controller.enabled))
        {
            return;
        }

        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            if (useAgent)
            {
                NavMeshCharacterUtility.Stop(agent);
            }
            return;
        }

        if (teamAI == null)
        {
            teamAI = GetComponent<EnemyAI>();
        }

        if (allyCombatAI == null)
        {
            allyCombatAI = GetComponent<AllyCombatAI>();
        }

        if (allyCombatAI != null && allyCombatAI.CurrentTarget != null)
        {
            // В бою союзник стоит и стреляет, поворотом занимается AllyCombatAI.
            if (useAgent)
            {
                NavMeshCharacterUtility.Stop(agent);
            }
            return;
        }

        if (teamAI != null && allyCombatAI == null)
        {
            teamAI.detectionRange = targetSearchRadius;
            if (currentEnemy == null || IsOutOfRange(currentEnemy))
            {
                currentEnemy = null;
            }

            if (currentEnemy == null && Time.time >= nextTargetSearchTime)
            {
                currentEnemy = FindNearestEnemy();
                nextTargetSearchTime = Time.time + targetSearchInterval;
            }

            teamAI.target = currentEnemy != null ? currentEnemy.transform : null;
            if (currentEnemy != null)
            {
                if (useAgent)
                {
                    NavMeshCharacterUtility.Stop(agent);
                }
                return;
            }
        }

        GameContext context = GameContext.Instance;
        player = context != null ? context.Player : null;

        if (useAgent)
        {
            FollowPlayerOnNavMesh();
            return;
        }

        Vector3 horizontalMovement = Vector3.zero;
        if (player != null)
        {
            Vector3 targetPosition = GetFormationPosition();
            Vector3 offset = targetPosition - transform.position;
            offset.y = 0f;

            if (offset.magnitude > 0.25f)
            {
                horizontalMovement = offset.normalized * followSpeed;
                Quaternion targetRotation = Quaternion.LookRotation(horizontalMovement);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }

        // Гравитация нужна, чтобы CharacterController реально касался земли
        // (isGrounded) и аниматор не показывал падение.
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 movement = horizontalMovement * Time.deltaTime;
        movement.y = verticalVelocity * Time.deltaTime;
        controller.Move(movement);
    }

    // Движение по запечённому NavMesh: агент сам обходит стены и мебель.
    private void FollowPlayerOnNavMesh()
    {
        if (player == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            NavMeshCharacterUtility.Stop(agent);
            return;
        }

        // Точка строя считается от корня игрока (он выше земли на 1.8 м),
        // поэтому проецируем её на навигацию.
        Vector3 targetPosition;
        if (!NavMeshCharacterUtility.TryGetNavPoint(GetFormationPosition(), 3f, out targetPosition))
        {
            NavMeshCharacterUtility.Stop(agent);
            return;
        }

        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;

        if (offset.magnitude <= 0.6f)
        {
            NavMeshCharacterUtility.Stop(agent);
            return;
        }

        NavMeshCharacterUtility.Resume(agent);
        agent.speed = followSpeed;
        if ((agent.destination - targetPosition).sqrMagnitude > 0.36f)
        {
            agent.SetDestination(targetPosition);
        }

        Vector3 velocity = agent.desiredVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.04f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(velocity.normalized),
                Time.deltaTime * rotationSpeed);
        }
    }

    private Vector3 GetFormationPosition()
    {
        int slot = ActiveFollowers.IndexOf(this);
        if (slot < 0)
        {
            slot = 0;
        }

        int row = slot / 2;
        float side = slot % 2 == 0 ? -spacing * 0.5f : spacing * 0.5f;
        float behind = followDistance + row * spacing;
        return player.transform.position - player.transform.forward * behind + player.transform.right * side;
    }

    private EnemyAI FindNearestEnemy()
    {
        EnemyAI nearestEnemy = null;
        float nearestDistanceSquared = targetSearchRadius * targetSearchRadius;
        IReadOnlyList<EnemyAI> activeEnemies = EnemyAI.ActiveEnemyList;
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyAI candidate = activeEnemies[i];
            if (candidate == null || candidate == teamAI || candidate.GetComponent<TeamFollowPlayer>() != null)
            {
                continue;
            }

            Health health = candidate.GetComponent<Health>();
            if (health != null && health.IsDead)
            {
                continue;
            }

            float distanceSquared = (transform.position - candidate.transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestEnemy = candidate;
            }
        }

        return nearestEnemy;
    }

    private bool IsOutOfRange(EnemyAI enemy)
    {
        if (enemy == null)
        {
            return true;
        }

        Health health = enemy.GetComponent<Health>();
        return (health != null && health.IsDead) ||
               Vector3.Distance(transform.position, enemy.transform.position) > targetSearchRadius;
    }
}