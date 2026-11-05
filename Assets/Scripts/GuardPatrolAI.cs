using UnityEngine;
using UnityEngine.AI;

// Патрульный охранник. Если под ногами есть запечённый NavMesh и все точки
// патруля до него достижимы — ходит через NavMeshAgent (обходит препятствия),
// иначе остаётся на CharacterController (тесные помещения с мебелью, где
// агент физически не пролезает).
public class GuardPatrolAI : MonoBehaviour
{
    [Header("Патруль")]
    public Transform[] waypoints;
    public float waitTime = 15f;
    public float moveSpeed = 1.7f;

    [Header("Бой")]
    public float detectionRange = 22f;
    public float viewAngle = 60f;      // Угол обзора (полный конус, градусы)
    public float attackRange = 9f;
    public float fireRate = 1.2f;
    public float damage = 10f;
    public float turnSpeed = 6f;

    [Header("Гравитация")]
    public float gravity = -12f;

    [Header("Навигация")]
    // При загрузке сцены NavMeshAgent иногда не успевает встать на полигон
    // в кадре Start. Включаем повторные попытки для новых врагов (Мария).
    public bool retryAgentActivation;

    private CharacterController controller;
    private NavMeshAgent agent;
    private Health health;
    private Transform player;
    private Transform target;
    private int waypointIndex;
    private int agentTargetWaypoint = -1;
    private float waitTimer;
    private float nextShotTime;
    private float nextTargetSearchTime;
    private float nextRepathTime;
    private float verticalVelocity;
    private bool useAgent;
    private float nextAgentRetryTime;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();

        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        agent = GetComponent<NavMeshAgent>();
        Vector3 startPosition = transform.position;
        useAgent = agent != null &&
                   NavMeshCharacterUtility.TryActivate(gameObject, agent, moveSpeed, 0.4f, 0.35f) &&
                   AreWaypointsNavigable();

        if (useAgent)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }
        }
        else
        {
            if (agent != null)
            {
                agent.enabled = false;
            }

            // Агент при включении мог сдвинуть трансформ на полигон —
            // возвращаем охранника туда, где он стоял в сцене.
            transform.position = startPosition;

            if (controller != null)
            {
                controller.enabled = true;
                controller.center = new Vector3(0f, -0.9f, 0f);
                controller.height = 1.8f;
                controller.radius = 0.32f;
                controller.stepOffset = 0.4f;
            }
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }

    }

    // При загрузке сцены NavMeshAgent не всегда регистрируется в кадре Start.
    // Пока флаг включён, раз в полсекунды пробуем включить агента снова.
    private void RetryAgentActivationTick()
    {
        if (useAgent || agent == null || Time.time < nextAgentRetryTime)
        {
            return;
        }

        nextAgentRetryTime = Time.time + 0.5f;

        Vector3 feet = transform.position - Vector3.up * NavMeshCharacterUtility.DefaultFootOffset;
        NavMeshHit navHit;
        if (!NavMesh.SamplePosition(feet, out navHit, 1.2f, NavMesh.AllAreas))
        {
            return;
        }

        if (!NavMeshCharacterUtility.TryActivate(gameObject, agent, moveSpeed, 0.4f, 0.35f))
        {
            return;
        }

        if (!AreWaypointsNavigable())
        {
            agent.enabled = false;
            return;
        }

        useAgent = true;
        if (controller != null)
        {
            controller.enabled = false;
        }
    }

    // Точки патруля должны быть достижимы агентом, иначе он упрётся в
    // кровать/перегородку и застрянет — тогда честнее идти на контроллере.
    private bool AreWaypointsNavigable()
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return false;
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            return true;
        }

        // Ноги стоят на полигоне: transform - baseOffset.
        Vector3 from = transform.position - Vector3.up * agent.baseOffset;
        NavMeshHit fromHit;
        if (!NavMesh.SamplePosition(from, out fromHit, 1f, NavMesh.AllAreas))
        {
            return false;
        }

        from = fromHit.position;
        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
            {
                continue;
            }

            // Пустышки патруля стоят на высоте корня персонажа, поэтому
            // ищем навигацию под точкой, на уровне ног.
            Vector3 waypointPosition;
            if (!NavMeshCharacterUtility.TryGetNavPoint(waypoints[i].position, 2.5f, out waypointPosition))
            {
                return false;
            }

            if (!NavMesh.CalculatePath(from, waypointPosition, NavMesh.AllAreas, path))
            {
                return false;
            }

            if (path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }
        }

        return true;
    }

    private void Update()
    {
        if (!useAgent && retryAgentActivation)
        {
            RetryAgentActivationTick();
        }

        if (health != null && health.IsDead)
        {
            if (useAgent)
            {
                NavMeshCharacterUtility.Stop(agent);
            }
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

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        // Цель — игрок или освобождённый союзник, но только если её видно.
        if (target != null)
        {
            Health targetHealth = target.GetComponentInParent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                target = null;
            }
            else if (HorizontalDistance(target.position) > detectionRange + 3f)
            {
                target = null;
            }
        }

        if (target == null && Time.time >= nextTargetSearchTime)
        {
            target = FindBestTarget();
            nextTargetSearchTime = Time.time + 0.3f;
        }

        float distance = target != null ? HorizontalDistance(target.position) : float.MaxValue;

        if (target != null && distance <= detectionRange)
        {
            if (distance <= attackRange)
            {
                Attack();
            }
            else
            {
                Chase();
            }
        }
        else
        {
            Patrol();
        }
    }

    private Transform FindBestTarget()
    {
        Transform best = null;
        float bestDistance = detectionRange;

        if (player != null && IsValidTarget(player) && CanSee(player))
        {
            best = player;
            bestDistance = HorizontalDistance(player.position);
        }

        // Союзники игрока (освобождённые пленные) тоже враждебны охране.
        GameObject[] allies = GameObject.FindGameObjectsWithTag("team");
        for (int i = 0; i < allies.Length; i++)
        {
            Transform candidate = allies[i] != null ? allies[i].transform : null;
            if (candidate == null || !IsValidTarget(candidate))
            {
                continue;
            }

            float candidateDistance = HorizontalDistance(candidate.position);
            if (candidateDistance > bestDistance || !CanSee(candidate))
            {
                continue;
            }

            best = candidate;
            bestDistance = candidateDistance;
        }

        return best;
    }

    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        Health candidateHealth = candidate.GetComponentInParent<Health>();
        return candidateHealth == null || !candidateHealth.IsDead;
    }

    private bool CanSee(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        // Сначала проверяем конус обзора — сзади охрана игрока не видит.
        Vector3 toCandidate = candidate.position - transform.position;
        toCandidate.y = 0f;
        if (toCandidate.sqrMagnitude > 0.01f &&
            Vector3.Angle(transform.forward, toCandidate) > viewAngle * 0.5f)
        {
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * -0.5f;
        Vector3 aim = candidate.position + Vector3.up * -0.4f;
        RaycastHit hit;
        if (!Physics.Linecast(origin, aim, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // Другой персонаж на линии — не стена, сквозь него смотрим.
        return hit.collider.GetComponentInParent<Health>() != null;
    }

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            if (useAgent)
            {
                NavMeshCharacterUtility.Stop(agent);
            }
            else
            {
                Move(Vector3.zero);
            }
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            if (useAgent)
            {
                NavMeshCharacterUtility.Stop(agent);
            }
            else
            {
                Move(Vector3.zero);
            }
            return;
        }

        Transform waypoint = waypoints[waypointIndex];
        if (waypoint == null)
        {
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
            return;
        }

        if (useAgent)
        {
            if (agentTargetWaypoint != waypointIndex)
            {
                agentTargetWaypoint = waypointIndex;
                NavMeshCharacterUtility.Resume(agent);
                agent.speed = moveSpeed;

                Vector3 destination;
                if (NavMeshCharacterUtility.TryGetNavPoint(waypoint.position, 2.5f, out destination))
                {
                    agent.SetDestination(destination);
                }
                else
                {
                    agent.SetDestination(waypoint.position);
                }
            }

            bool arrived = !agent.pathPending &&
                           agent.remainingDistance <= agent.stoppingDistance + 0.25f &&
                           HorizontalDistance(waypoint.position) <= 1f;
            if (arrived)
            {
                NavMeshCharacterUtility.Stop(agent);
                waitTimer = waitTime;
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                agentTargetWaypoint = -1;
                return;
            }

            FaceMovementDirection();
            return;
        }

        Vector3 offset = waypoint.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude <= 0.5f)
        {
            waitTimer = waitTime;
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
            Move(Vector3.zero);
            return;
        }

        Move(offset.normalized * moveSpeed);
    }

    private void Chase()
    {
        if (target == null)
        {
            return;
        }

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (useAgent)
        {
            agentTargetWaypoint = -1;
            if (distance > 2.5f)
            {
                NavMeshCharacterUtility.Resume(agent);
                agent.speed = moveSpeed * 1.9f;
                if (Time.time >= nextRepathTime)
                {
                    Vector3 destination;
                    if (NavMeshCharacterUtility.TryGetNavPoint(target.position, 3f, out destination))
                    {
                        agent.SetDestination(destination);
                    }
                    nextRepathTime = Time.time + 0.25f;
                }
                FaceMovementDirection();
            }
            else
            {
                NavMeshCharacterUtility.Stop(agent);
            }
            return;
        }

        if (distance > 2.5f)
        {
            Move(offset.normalized * (moveSpeed * 1.9f));
        }
        else
        {
            Move(Vector3.zero);
        }
    }

    private void Attack()
    {
        if (target == null)
        {
            return;
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction.normalized),
                turnSpeed * Time.deltaTime);
        }

        if (useAgent)
        {
            agentTargetWaypoint = -1;
            NavMeshCharacterUtility.Stop(agent);
        }
        else
        {
            Move(Vector3.zero);
        }

        if (Time.time >= nextShotTime)
        {
            Shoot();
            nextShotTime = Time.time + fireRate;
        }
    }

    // В режиме агента поворот делаем сами: updateRotation выключен, чтобы
    // не конфликтовать с ручным разворотом на цель при стрельбе.
    private void FaceMovementDirection()
    {
        if (agent == null)
        {
            return;
        }

        Vector3 velocity = agent.desiredVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude <= 0.04f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(velocity.normalized),
            turnSpeed * Time.deltaTime);
    }

    private void Shoot()
    {
        if (target == null)
        {
            return;
        }

        // Стреляем от уровня груди (модель смещена вниз на 1.8 м).
        Vector3 origin = transform.position + Vector3.up * -0.6f;
        Vector3 aim = (target.position + Vector3.up * -0.3f - origin).normalized;
        aim += Random.insideUnitSphere * 0.03f;

        RaycastHit hit;
        if (Physics.Raycast(origin, aim, out hit, detectionRange))
        {
            Health hitHealth = hit.collider.GetComponentInParent<Health>();
            // Своих не бьём: охрана имеет тег enemy, союзники — team.
            if (hitHealth != null && !hitHealth.IsDead && !hitHealth.CompareTag(gameObject.tag))
            {
                hitHealth.TakeDamage(damage);
            }
        }

        Debug.DrawRay(origin, aim * detectionRange, Color.red, 0.4f);
    }

    private void Move(Vector3 horizontalVelocity)
    {
        if (controller == null || !controller.enabled)
        {
            transform.position += horizontalVelocity * Time.deltaTime;
            return;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 motion = horizontalVelocity * Time.deltaTime;
        motion.y = verticalVelocity * Time.deltaTime;
        controller.Move(motion);

        if (horizontalVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(horizontalVelocity.normalized),
                turnSpeed * Time.deltaTime);
        }
    }

    private float HorizontalDistance(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }
}
