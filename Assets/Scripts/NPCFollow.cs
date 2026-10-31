using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCFollow : MonoBehaviour
{
    [Header("Цель преследования")]
    public Transform target; // Сюда перетащим игрока

    [Header("Настройки следования")]
    public float stopDistance = 2.0f; // На каком расстоянии он остановится от игрока

    private NavMeshAgent agent;
    private Vector3 lastDestination;
    private bool hasDestination;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        CharacterAnimatorDriver.AttachIfPresent(gameObject);
    }

    void Update()
    {
        if (target == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        if (distanceToPlayer > stopDistance)
        {
            agent.isStopped = false;
            if (!hasDestination || (lastDestination - target.position).sqrMagnitude > 0.25f || !agent.hasPath)
            {
                hasDestination = agent.SetDestination(target.position);
                lastDestination = target.position;
            }

            Vector3 moveDirection = agent.desiredVelocity;
            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    agent.angularSpeed * Time.deltaTime);
            }
        }
        else
        {
            agent.isStopped = true;
            hasDestination = false;
        }
    }
}