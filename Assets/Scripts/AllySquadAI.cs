using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Командир отряда освобождённых: сам ищет живых немцев вокруг питомника и
// базы, идёт к ним и зачищает. Когда живых врагов рядом не осталось —
// ведёт отряд к мерии. Остальные союзники идут за ним (TeamFollowPlayer
// с leader), держа дистанцию 4–6 м.
public class AllySquadAI : MonoBehaviour
{
    [Header("Зоны поиска")]
    public Transform paddockCenter;
    public Transform mariyaPoint;
    public float paddockRadius = 16f;
    public float mariyaRadius = 14f;
    public float paddockClearRadius = 45f;
    [Tooltip("Радиус поиска врагов вокруг командира")]
    public float huntRadius = 70f;

    [Header("Движение")]
    public float moveSpeed = 4.2f;
    public float arriveDistance = 2f;
    [Tooltip("Сколько секунд стоять на месте, прежде чем считать цель недостижимой")]
    public float stuckTimeout = 5f;
    [Tooltip("На сколько секунд забыть недостижимого врага")]
    public float ignoreDuration = 12f;
    [Tooltip("Ближе этого к врагу командир не подходит")]
    public float huntStopDistance = 7f;

    private enum Phase
    {
        Paddock,
        Mariya
    }

    private Phase phase = Phase.Paddock;
    private Vector3[] waypoints;
    private int waypointIndex;
    private NavMeshAgent agent;
    private Health health;
    private float nextCheckTime;
    private float stuckTimer;
    private Vector3 lastPosition;
    private float leftBehindTimer;

    private Transform huntTarget;
    private Transform ignoredEnemy;
    private float ignoreUntil;
    private float nextHuntSearchTime;
    private Vector3 huntDestination;
    private bool hasHuntDestination;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        lastPosition = transform.position;
        BuildPaddockRoute();
    }

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            Stop();
            return;
        }

        if (health != null && health.IsDead)
        {
            Stop();
            return;
        }

        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + 0.5f;
            CheckPhase();
        }

        // Пока кто-то из отряда стреляет — командир стоит и воюет.
        if (IsAnySquadMemberEngaged())
        {
            Stop();
            return;
        }

        // Отряд растянулся — ждём, чтобы все держались в 4–6 м.
        // Если кто-то застрял надолго, командир не стоит вечно.
        if (IsSquadLeftBehind())
        {
            leftBehindTimer += Time.deltaTime;
            if (leftBehindTimer < 8f)
            {
                Stop();
                return;
            }
        }
        else
        {
            leftBehindTimer = 0f;
        }

        // Сначала идём к ближайшему живому врагу, только потом — по маршруту.
        if (Time.time >= nextHuntSearchTime)
        {
            nextHuntSearchTime = Time.time + 0.3f;
            hasHuntDestination = TryFindEnemy(transform.position, huntRadius, out huntDestination);
        }

        if (hasHuntDestination)
        {
            // Враг рядом и его видно — дальше подходить не нужно, стреляет
            // боевой ИИ. Если враг за стеной (дальше по NavMesh нужно
            // обойти), продолжаем идти, иначе отряд замирает у забора.
            if (huntTarget != null &&
                HorizontalDistance(huntTarget.position) <= huntStopDistance &&
                HasLineOfSight(huntTarget))
            {
                Stop();
                return;
            }

            MoveTo(huntDestination, true);
            return;
        }

        MoveAlongRoute();
    }

    // Питомник считается зачищенным, когда рядом не осталось живых немцев,
    // до которых вообще можно дойти.
    private void CheckPhase()
    {
        if (phase != Phase.Paddock)
        {
            return;
        }

        if (paddockCenter == null ||
            !HasHuntableEnemyNear(paddockCenter.position, paddockClearRadius))
        {
            phase = Phase.Mariya;
            BuildMariyaRoute();
        }
    }

    // Живой враг рядом, до которого командир реально может дойти по NavMesh.
    // Немец за глухим забором не должен держать отряд на месте навсегда.
    private bool HasHuntableEnemyNear(Vector3 center, float radius)
    {
        float radiusSquared = radius * radius;
        Health[] allHealth = FindObjectsOfType<Health>();
        for (int i = 0; i < allHealth.Length; i++)
        {
            Health candidate = allHealth[i];
            if (!IsLivingEnemy(candidate) || IsIgnored(candidate.transform))
            {
                continue;
            }

            if ((candidate.transform.position - center).sqrMagnitude > radiusSquared)
            {
                continue;
            }

            Vector3 navPoint;
            if (!NavMeshCharacterUtility.TryGetNavPoint(candidate.transform.position, 3f, out navPoint))
            {
                continue;
            }

            if (HasCompletePath(navPoint))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCompletePath(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return true;
        }

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path))
        {
            return false;
        }

        return path.status == NavMeshPathStatus.PathComplete;
    }

    private float HorizontalDistance(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // Видно ли врага с груди командира (та же логика, что у AllyCombatAI).
    private bool HasLineOfSight(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * -0.6f;
        Vector3 aimPoint = target.position;
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider == null)
        {
            targetCollider = target.GetComponentInChildren<Collider>();
        }

        if (targetCollider != null)
        {
            aimPoint = targetCollider.bounds.center;
        }

        Vector3 direction = aimPoint - origin;
        float distance = direction.magnitude;
        if (distance < 0.01f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction / distance,
            distance + 0.3f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        RaycastHit closest = default(RaycastHit);
        bool hasClosest = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null ||
                hits[i].collider.transform.IsChildOf(transform))
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
            return true;
        }

        return closest.collider.transform == target ||
               closest.collider.transform.IsChildOf(target);
    }

    // Ищем по Health, а не по EnemyAI: у охраны EnemyAI выключен
    // (GuardPatrolAI), но это всё ещё живые немцы.
    private bool TryFindEnemy(Vector3 origin, float radius, out Vector3 destination)
    {
        destination = Vector3.zero;
        float bestSquared = radius * radius;
        Transform bestTransform = null;

        Health[] allHealth = FindObjectsOfType<Health>();
        for (int i = 0; i < allHealth.Length; i++)
        {
            Health candidate = allHealth[i];
            if (!IsLivingEnemy(candidate) || IsIgnored(candidate.transform))
            {
                continue;
            }

            Vector3 position = candidate.transform.position;
            float distanceSquared = (position - origin).sqrMagnitude;
            if (distanceSquared > bestSquared)
            {
                continue;
            }

            Vector3 navPoint;
            if (!NavMeshCharacterUtility.TryGetNavPoint(position, 3f, out navPoint))
            {
                continue;
            }

            bestSquared = distanceSquared;
            destination = navPoint;
            bestTransform = candidate.transform;
        }

        huntTarget = bestTransform;
        return bestTransform != null;
    }

    private static bool IsLivingEnemy(Health candidate)
    {
        if (candidate == null || candidate.IsDead)
        {
            return false;
        }

        if (!candidate.CompareTag("enemy") || candidate.CompareTag("team"))
        {
            return false;
        }

        // Без коллайдера немца невозможно убить — не охотимся за пустышкой.
        return candidate.GetComponent<Collider>() != null ||
               candidate.GetComponentInChildren<Collider>() != null;
    }

    private bool IsIgnored(Transform enemy)
    {
        if (ignoredEnemy == null || Time.time >= ignoreUntil)
        {
            return false;
        }

        return enemy == ignoredEnemy;
    }

    private void IgnoreCurrentTarget()
    {
        if (huntTarget != null)
        {
            ignoredEnemy = huntTarget;
            ignoreUntil = Time.time + ignoreDuration;
        }

        huntTarget = null;
    }

    private static bool IsAnySquadMemberEngaged()
    {
        TeamFollowPlayer[] followers = FindObjectsOfType<TeamFollowPlayer>();
        for (int i = 0; i < followers.Length; i++)
        {
            AllyCombatAI combat = followers[i] != null
                ? followers[i].GetComponent<AllyCombatAI>()
                : null;
            if (combat != null && combat.enabled && combat.CurrentTarget != null)
            {
                return true;
            }
        }

        return false;
    }

    // Подчинённый отстал больше чем на 9 м — командир ждёт.
    private bool IsSquadLeftBehind()
    {
        TeamFollowPlayer[] followers = FindObjectsOfType<TeamFollowPlayer>();
        for (int i = 0; i < followers.Length; i++)
        {
            TeamFollowPlayer follower = followers[i];
            if (follower == null || !follower.enabled || follower.leader != transform)
            {
                continue;
            }

            Vector3 offset = follower.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > 9f)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildPaddockRoute()
    {
        if (paddockCenter == null)
        {
            phase = Phase.Mariya;
            BuildMariyaRoute();
            return;
        }

        Vector3[] ring = BuildRing(paddockCenter.position, paddockRadius, 8);
        System.Collections.Generic.List<Vector3> route = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < ring.Length; i++)
        {
            if (ring[i] != Vector3.zero)
            {
                route.Add(ring[i]);
            }
        }

        waypoints = route.ToArray();
        waypointIndex = 0;
        ApplyAgentSettings();
    }

    private void BuildMariyaRoute()
    {
        if (mariyaPoint == null)
        {
            waypoints = new Vector3[0];
            return;
        }

        // Около мерии: кольцо снаружи и точки внутри здания (этажи).
        Vector3[] ring = BuildRing(mariyaPoint.position, mariyaRadius, 8);
        Vector3[] inside = new Vector3[3];
        inside[0] = mariyaPoint.position + new Vector3(0f, 0f, 2f);
        inside[1] = mariyaPoint.position + new Vector3(-6f, 0f, -4f);
        inside[2] = mariyaPoint.position + new Vector3(6f, 0f, -4f);

        System.Collections.Generic.List<Vector3> route = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < ring.Length; i++)
        {
            if (ring[i] != Vector3.zero)
            {
                route.Add(ring[i]);
            }
        }

        for (int i = 0; i < inside.Length; i++)
        {
            Vector3 projected;
            if (NavMeshCharacterUtility.TryGetNavPoint(inside[i], 4f, out projected))
            {
                route.Add(projected);
            }
        }

        waypoints = route.ToArray();
        waypointIndex = 0;
        ApplyAgentSettings();
    }

    // Кольцо точек вокруг центра, только те, что лежат на NavMesh.
    private static Vector3[] BuildRing(Vector3 center, float radius, int count)
    {
        Vector3[] result = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 point = center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            Vector3 projected;
            if (NavMeshCharacterUtility.TryGetNavPoint(point, 4f, out projected))
            {
                result[i] = projected;
            }
        }

        return result;
    }

    private void ApplyAgentSettings()
    {
        if (agent != null && agent.enabled)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = arriveDistance * 0.5f;
            agent.updateRotation = false;
            agent.autoBraking = true;
        }
    }

    private void MoveAlongRoute()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Stop();
            return;
        }

        MoveTo(waypoints[waypointIndex], false);
    }

    // Движение к точке. hunting=true — застряли у врага, помечаем его
    // недостижимым; иначе просто пропускаем точку маршрута.
    private void MoveTo(Vector3 destination, bool hunting)
    {
        bool arrived = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            NavMeshCharacterUtility.Resume(agent);
            if ((agent.destination - destination).sqrMagnitude > 1f)
            {
                agent.SetDestination(destination);
            }

            arrived = !agent.pathPending && agent.hasPath &&
                      agent.remainingDistance <= arriveDistance + 0.3f;
            if (!arrived)
            {
                FaceMovement(agent.desiredVelocity);
            }
        }
        else
        {
            Vector3 offset = destination - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= arriveDistance)
            {
                arrived = true;
            }
            else
            {
                transform.position += offset.normalized * moveSpeed * Time.deltaTime;
                FaceMovement(offset);
            }
        }

        if (arrived)
        {
            stuckTimer = 0f;
            if (!hunting)
            {
                huntTarget = null;
                NextWaypoint();
                return;
            }

            // Дошли до ближайшей точки NavMesh, но враг всё ещё далеко —
            // значит, он за забором/стеной. Не кружим вокруг него.
            if (huntTarget != null &&
                HorizontalDistance(huntTarget.position) > huntStopDistance + 2f)
            {
                IgnoreCurrentTarget();
            }

            huntTarget = null;
            return;
        }

        // Застрял (путь обрезан, дверь, забор) — меняем цель.
        if ((transform.position - lastPosition).sqrMagnitude < 0.04f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > stuckTimeout)
            {
                stuckTimer = 0f;
                if (hunting)
                {
                    IgnoreCurrentTarget();
                }
                else
                {
                    NextWaypoint();
                }
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
    }

    private void FaceMovement(Vector3 velocity)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude <= 0.04f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(velocity.normalized),
            Time.deltaTime * 8f);
    }

    private void NextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return;
        }

        waypointIndex = (waypointIndex + 1) % waypoints.Length;
    }

    private void Stop()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            NavMeshCharacterUtility.Stop(agent);
        }
    }
}
