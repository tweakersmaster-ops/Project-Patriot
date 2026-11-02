using UnityEngine;
using UnityEngine.AI;

// Гражданский пленный: выбирается из коровника и тайком бежит к лесу.
public class CivilianFlee : MonoBehaviour
{
    [Header("Куда бежать")]
    public Transform forestExit;
    public string forestExitName = "ForestExit";
    public Transform firstWaypoint; // Сначала дойти до выхода из коровника

    [Header("Настройки")]
    public float runSpeed = 3.5f;
    public float despawnDistance = 3f;
    public float rotationSpeed = 8f;
    public float waypointTolerance = 1.5f;
    public float maxLifetime = 45f; // Страховка: если застрял — исчезнуть

    private NavMeshAgent agent;
    private CharacterController controller;
    private bool useAgent;
    private bool arrived;
    private bool reachedFirstWaypoint;
    private Transform lastAgentTarget;
    private float verticalVelocity;
    private float gravity = -12f;
    private float lifeTimer;
    private readonly RaycastHit[] avoidHits = new RaycastHit[8];
    private float avoidProbeDistance = 1.8f;
    private const float AvoidProbeRadius = 0.3f;

    private void Start()
    {
        if (forestExit == null)
        {
            GameObject exitObject = GameObject.Find(forestExitName);
            if (exitObject != null)
            {
                forestExit = exitObject.transform;
            }
        }

        // Движение: агент по NavMesh, если он есть и стоит на навигации,
        // иначе CharacterController (лес, куда ведёт побег, вне запечённой зоны).
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        useAgent = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        if (useAgent)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }

            agent.speed = runSpeed;
            agent.updateRotation = false;
            Transform startTarget = CurrentTarget();
            if (startTarget == null || !agent.SetDestination(startTarget.position))
            {
                SwitchToController();
            }
            else
            {
                lastAgentTarget = startTarget;
            }
        }
        else if (controller != null)
        {
            EnableController();
        }

        CharacterAnimatorDriver.AttachIfPresent(gameObject);
    }

    private void Update()
    {
        if (arrived || forestExit == null)
        {
            return;
        }

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetime)
        {
            Disappear();
            return;
        }

        if (!reachedFirstWaypoint && firstWaypoint != null &&
            HorizontalDistance(firstWaypoint.position) <= waypointTolerance)
        {
            reachedFirstWaypoint = true;
        }

        Transform target = CurrentTarget();
        if (target == null)
        {
            return;
        }

        if (useAgent)
        {
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                SwitchToController();
                return;
            }

            if (lastAgentTarget != target)
            {
                if (!agent.SetDestination(target.position))
                {
                    SwitchToController();
                    return;
                }
                lastAgentTarget = target;
            }

            // Цель вне запечённой зоны (лес). Агент доводит до ближайшей
            // доступной точки (partial path), и только дойдя туда —
            // переключаемся на CharacterController с обходом препятствий.
            if (!agent.pathPending && agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
            {
                SwitchToController();
                return;
            }

            if (!agent.pathPending && agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathPartial &&
                agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.6f))
            {
                SwitchToController();
                return;
            }

            Vector3 agentVelocity = agent.desiredVelocity;
            agentVelocity.y = 0f;
            if (agentVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(agentVelocity.normalized),
                    rotationSpeed * Time.deltaTime);
            }

            if (target == forestExit &&
                !agent.pathPending &&
                agent.remainingDistance <= despawnDistance)
            {
                Disappear();
            }

            return;
        }

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        if (target == forestExit && offset.magnitude <= despawnDistance)
        {
            Disappear();
            return;
        }

        // Обход стен и препятствий: если прямо мешает геометрия,
        // скользим вдоль неё или ищем свободное направление по бокам.
        Vector3 desiredDirection = offset.normalized;
        Vector3 move = GetAvoidanceDirection(desiredDirection) * runSpeed;
        if (controller != null && controller.enabled)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 motion = move * Time.deltaTime;
            motion.y = verticalVelocity * Time.deltaTime;
            controller.Move(motion);
        }
        else
        {
            transform.position += move * Time.deltaTime;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(offset.normalized),
            rotationSpeed * Time.deltaTime);
    }

    // Обход препятствий без NavMesh: если впереди стена — скользим вдоль
    // неё, а если скользить некуда — ищем свободное направление веером.
    private Vector3 GetAvoidanceDirection(Vector3 desired)
    {
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        RaycastHit blockingHit;
        if (!IsPathBlocked(origin, desired, out blockingHit))
        {
            return desired;
        }

        // Скольжение вдоль плоскости стены.
        Vector3 alongWall = desired - blockingHit.normal * Vector3.Dot(desired, blockingHit.normal);
        alongWall.y = 0f;
        if (alongWall.sqrMagnitude > 0.01f)
        {
            alongWall.Normalize();
            RaycastHit slideHit;
            if (!IsPathBlocked(origin, alongWall, out slideHit))
            {
                return alongWall;
            }
        }

        // Веер направлений: ±45°, ±90°, ±135°.
        for (int step = 1; step <= 3; step++)
        {
            float angle = step * 45f;
            for (int side = 0; side < 2; side++)
            {
                Vector3 candidate = Quaternion.Euler(0f, (side == 0 ? angle : -angle), 0f) * desired;
                RaycastHit fanHit;
                if (!IsPathBlocked(origin, candidate, out fanHit))
                {
                    return candidate;
                }
            }
        }

        return desired;
    }

    private bool IsPathBlocked(Vector3 origin, Vector3 direction, out RaycastHit blockingHit)
    {
        blockingHit = default(RaycastHit);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            AvoidProbeRadius,
            direction,
            avoidHits,
            avoidProbeDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool blocked = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = avoidHits[i];
            if (hit.collider == null || hit.distance <= 0.01f)
            {
                continue;
            }

            // Другие персонажи не считаются стеной.
            if (hit.collider.GetComponentInParent<Health>() != null)
            {
                continue;
            }

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                blockingHit = hit;
                blocked = true;
            }
        }

        return blocked;
    }

    private Transform CurrentTarget()
    {
        if (!reachedFirstWaypoint && firstWaypoint != null)
        {
            return firstWaypoint;
        }

        return forestExit;
    }

    private float HorizontalDistance(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private void EnableController()
    {
        if (controller == null)
        {
            return;
        }

        controller.enabled = true;
        controller.center = new Vector3(0f, -0.9f, 0f);
        controller.height = 1.8f;
        controller.radius = 0.32f;
        controller.stepOffset = 0.4f;
    }

    // Агент не доведёт до леса — дальше идём CharacterController'ом.
    private void SwitchToController()
    {
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.enabled = false;
        }

        useAgent = false;
        EnableController();
    }

    private void Disappear()
    {
        arrived = true;
        Destroy(gameObject);
    }
}
