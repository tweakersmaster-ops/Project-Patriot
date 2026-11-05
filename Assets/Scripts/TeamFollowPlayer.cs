using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Союзник идёт за командиром отряда (leader) и держится в 4–6 м.
// Если командир не задан — старое поведение: идти за игроком.
[RequireComponent(typeof(CharacterController))]
public class TeamFollowPlayer : MonoBehaviour
{
    private static readonly List<TeamFollowPlayer> ActiveFollowers = new List<TeamFollowPlayer>();

    [Header("Кого слушаемся")]
    public Transform leader;              // Командир отряда; пусто — игрок

    [Header("Дистанция")]
    public float minDistance = 4f;        // Ближе не подходить
    public float maxDistance = 6f;        // Дальше — догонять
    public float followSpeed = 4.2f;
    public float rotationSpeed = 8f;

    [Header("Бой")]
    public float targetSearchRadius = 15f;
    public float targetSearchInterval = 0.35f;

    public float gravity = -9.81f;

    private float verticalVelocity;
    private float currentSpeed;

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

        Transform followTarget = leader;
        if (followTarget == null)
        {
            GameContext context = GameContext.Instance;
            player = context != null ? context.Player : null;
            followTarget = player != null ? player.transform : null;
        }

        if (followTarget == null)
        {
            StopMoving();
            return;
        }

        Vector3 toLeader = followTarget.position - transform.position;
        toLeader.y = 0f;
        float distance = toLeader.magnitude;

        // Точка строя в 5 м за командиром держит дистанцию 4–6 м.
        // Отстали больше чем на maxDistance — догоняем быстрее командира.
        currentSpeed = distance > maxDistance
            ? followSpeed * 1.6f
            : followSpeed;

        Vector3 targetPosition = GetFormationPosition(followTarget);

        if (useAgent)
        {
            FollowOnNavMesh(targetPosition, followTarget);
            return;
        }

        MoveOnController(targetPosition);
    }

    // Точка строя: за командиром (~5 м), чуть в сторону, чтобы не слипались.
    private Vector3 GetFormationPosition(Transform followTarget)
    {
        int slot = ActiveFollowers.IndexOf(this);
        if (slot < 0)
        {
            slot = 0;
        }

        float side = (slot - (ActiveFollowers.Count - 1) * 0.5f) * 1.2f;
        // Целимся ближе к ближней границе: пока идём, отстаём на ~1 м.
        float behind = minDistance + (maxDistance - minDistance) * 0.25f;
        return followTarget.position - followTarget.forward * behind + followTarget.right * side;
    }

    private void FollowOnNavMesh(Vector3 targetPosition, Transform followTarget)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            StopMoving();
            return;
        }

        Vector3 destination;
        if (!NavMeshCharacterUtility.TryGetNavPoint(targetPosition, 3f, out destination))
        {
            // Точка строя ушла в стену — идём хотя бы к командиру,
            // иначе отстающий застревает, а командир его ждёт вечно.
            if (followTarget == null ||
                !NavMeshCharacterUtility.TryGetNavPoint(followTarget.position, 4f, out destination))
            {
                StopMoving();
                return;
            }
        }

        Vector3 offset = destination - transform.position;
        offset.y = 0f;
        if (offset.magnitude <= 0.75f)
        {
            StopMoving();
            return;
        }

        NavMeshCharacterUtility.Resume(agent);
        agent.speed = currentSpeed;
        if ((agent.destination - destination).sqrMagnitude > 0.36f)
        {
            agent.SetDestination(destination);
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

    private void MoveOnController(Vector3 targetPosition)
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;

        Vector3 horizontalMovement = Vector3.zero;
        if (offset.magnitude > 0.25f)
        {
            horizontalMovement = offset.normalized * currentSpeed;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(horizontalMovement),
                Time.deltaTime * rotationSpeed);
        }

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

    private void StopMoving()
    {
        if (useAgent && agent != null)
        {
            NavMeshCharacterUtility.Stop(agent);
        }
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
