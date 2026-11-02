using UnityEngine;
using UnityEngine.AI;

// Общие настройки NavMeshAgent для персонажей проекта.
// Корень персонажа поднят на 1.8 м над землёй (модель смещена вниз),
// поэтому агенту обязателен baseOffset: иначе модель уйдёт под землю.
public static class NavMeshCharacterUtility
{
    public const float DefaultFootOffset = 1.8f;

    // Пробует поставить агента на запечённый NavMesh.
    // false — под ногами нет навигации, вызывающий должен остаться на CharacterController.
    public static bool TryActivate(
        GameObject owner,
        NavMeshAgent agent,
        float speed,
        float radius,
        float stoppingDistance)
    {
        if (owner == null || agent == null)
        {
            return false;
        }

        // Важно: сначала проверяем навигацию, и только потом включаем агента.
        // Включение агента вне NavMesh телепортирует трансформ к ближайшему
        // полигону (часто под землю) — персонаж потом проваливается.
        // Для выборки берём ноги на типовом смещении: у персонажей
        // CharacterController настроен по-разному, а модель у всех висит
        // на одном и том же -1.8 от корня.
        Vector3 originalPosition = owner.transform.position;
        Vector3 feet = originalPosition - Vector3.up * DefaultFootOffset;

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(feet, out hit, 1.2f, NavMesh.AllAreas))
        {
            // Корень мог «прилипнуть» к полигону: только что добавленный
            // агент при включении тянет объект на NavMesh. Тогда ищем
            // навигацию прямо под корнем и восстановим высоту модели.
            if (!NavMesh.SamplePosition(originalPosition, out hit, 3f, NavMesh.AllAreas))
            {
                if (agent.enabled)
                {
                    agent.enabled = false;
                }
                return false;
            }
        }

        // baseOffset — это высота корня над полигоном. Берём её из того,
        // как персонаж стоял в сцене, чтобы модель не «утонула» и не
        // повисла в воздухе после перехода на агента.
        float footOffset = originalPosition.y - hit.position.y;
        if (footOffset < 1f || footOffset > 2.6f)
        {
            footOffset = DefaultFootOffset;
        }

        // Агент считает свою позицию на полигоне как transform - baseOffset.
        // Корень должен стоять ровно над найденной точкой полигона: иначе
        // агент решает, что висит вне NavMesh, и падает сквозь мир.
        agent.enabled = false;
        agent.agentTypeID = 0;
        agent.radius = radius;
        agent.height = 2f;
        agent.speed = speed;
        agent.acceleration = 12f;
        agent.angularSpeed = 480f;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(25, 75);
        agent.updatePosition = true;
        agent.updateRotation = false;
        agent.baseOffset = footOffset;

        // Warp не подходит: он ставит трансформ ровно в точку полигона и
        // теряет baseOffset, после чего агент считает себя под землёй.
        owner.transform.position = hit.position + Vector3.up * footOffset;
        agent.enabled = true;

        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            owner.transform.position = originalPosition;
            return false;
        }

        return true;
    }

    // Точки патруля и трансформы персонажей стоят на высоте корня, а не на
    // земле. SetDestination по такой точке агент может проигнорировать —
    // поэтому проецируем её на NavMesh.
    public static bool TryGetNavPoint(Vector3 worldPosition, float maxDistance, out Vector3 result)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(worldPosition, out hit, maxDistance, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = worldPosition;
        return false;
    }

    public static void Stop(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    public static void Resume(NavMeshAgent agent)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = false;
    }
}
