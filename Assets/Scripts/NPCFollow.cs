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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (target == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        if (distanceToPlayer > stopDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
        else
        {
            agent.isStopped = true;
        }
    }
}