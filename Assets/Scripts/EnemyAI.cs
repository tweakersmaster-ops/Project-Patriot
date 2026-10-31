using UnityEngine;
using UnityEngine.AI;

// Состояния, в которых может находиться немец
public enum EnemyState
{
    Patrolling, // Патрулирует
    Chasing,    // Преследует
    Attacking   // Стреляет
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))] // Убедись, что у немца есть Health
public class EnemyAI : MonoBehaviour
{
    [Header("Настройки патрулирования")]
    public Transform[] waypoints; // Точки патруля (закинь пустышки)
    public float waitTimeAtPoint = 3f; // Сколько стоять на точке

    [Header("Настройки зрения и атаки")]
    public float moveSpeed = 2f;      // Скорость (для совместимости, синхронизируется с agent.speed)
    public float detectionRange = 15f; // Радиус обнаружения игрока
    public float attackRange = 10f;    // Дистанция, с которой начинает стрелять
    public float fireRate = 1.5f;      // Скорострельность
    public float damage = 10f;         // Урон

    [Header("Ссылки")]
    public Transform target; // Игрок (можно оставить пустым, скрипт сам найдет)

    private NavMeshAgent agent;
    private EnemyState currentState;
    
    private int currentWaypointIndex = 0;
    private float waitTimer = 0f;
    private float nextShotTime;
    
    private Health myHealth;
    private CharacterAnimatorDriver animatorDriver; // Ссылка на твой драйвер анимаций

    // Список всех живых врагов (для AllyCombatAI, TeamFollowPlayer и т.д.)
    public static System.Collections.Generic.List<EnemyAI> ActiveEnemyList =
        new System.Collections.Generic.List<EnemyAI>();

    private void Awake()
    {
        ActiveEnemyList.Add(this);
    }

    private void OnDestroy()
    {
        ActiveEnemyList.Remove(this);
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        myHealth = GetComponent<Health>();
        animatorDriver = GetComponent<CharacterAnimatorDriver>();

        // Настройки агента для плавного движения и поворотов
        agent.updateRotation = true; // Пусть агент сам поворачивает модель
        agent.updatePosition = true; // Пусть агент сам двигает модель
        agent.stoppingDistance = attackRange; // Останавливается, когда доходит до дистанции атаки

        // Если игрок не назначен вручную, ищем его на сцене
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }

        // Начинаем с патрулирования
        ChangeState(EnemyState.Patrolling);
        nextShotTime = Time.time;
    }

    private void Update()
    {
        // Если немец мертв, ничего не делаем
        if (myHealth != null && myHealth.IsDead) return;
        if (target == null) return;

        // Считаем дистанцию до игрока
        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        // ЛОГИКА ПЕРЕКЛЮЧЕНИЯ СОСТОЯНИЙ (Машина состояний)
        if (distanceToPlayer <= attackRange)
        {
            if (currentState != EnemyState.Attacking)
                ChangeState(EnemyState.Attacking);
        }
        else if (distanceToPlayer <= detectionRange)
        {
            if (currentState != EnemyState.Chasing)
                ChangeState(EnemyState.Chasing);
        }
        else
        {
            if (currentState != EnemyState.Patrolling)
                ChangeState(EnemyState.Patrolling);
        }

        // ВЫПОЛНЕНИЕ ДЕЙСТВИЙ В ЗАВИСИМОСТИ ОТ СОСТОЯНИЯ
        switch (currentState)
        {
            case EnemyState.Patrolling:
                HandlePatrol();
                break;
            case EnemyState.Chasing:
                HandleChase();
                break;
            case EnemyState.Attacking:
                HandleAttack();
                break;
        }
    }

    private void ChangeState(EnemyState newState)
    {
        currentState = newState;

        if (newState == EnemyState.Patrolling)
        {
            agent.speed = moveSpeed; // Медленная скорость
            agent.isStopped = false;
            waitTimer = 0f;
        }
        else if (newState == EnemyState.Chasing)
        {
            agent.speed = 5f; // Быстрая скорость
            agent.isStopped = false;
        }
        else if (newState == EnemyState.Attacking)
        {
            agent.isStopped = true; // Останавливаемся для стрельбы
        }
    }

    private void HandlePatrol()
    {
        if (waypoints.Length == 0) return;

        // Если стоим на точке и ждем
        if (waitTimer > 0)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        // Если дошли до текущей точки
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            waitTimer = waitTimeAtPoint; // Включаем таймер ожидания
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length; // Идем к следующей
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    private void HandleChase()
    {
        // Постоянно обновляем цель
        agent.SetDestination(target.position);
    }

    private void HandleAttack()
    {
        // Плавно поворачиваемся к игроку
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

        // Стреляем с задержкой
        if (Time.time >= nextShotTime)
        {
            Shoot();
            nextShotTime = Time.time + fireRate;
        }
    }

    private void Shoot()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f; // Стреляем с высоты груди
        Vector3 aimDirection = (target.position + Vector3.up * 1f - origin).normalized;

        // Небольшой разброс для реализма
        aimDirection += Random.insideUnitSphere * 0.05f;

        Ray ray = new Ray(origin, aimDirection);
        if (Physics.Raycast(ray, out RaycastHit hit, detectionRange))
        {
            // Если попали в игрока
            if (hit.collider.CompareTag("Player"))
            {
                Health playerHealth = hit.collider.GetComponentInParent<Health>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                }
            }
        }
        
        // Визуализация выстрела в редакторе (красная линия)
        Debug.DrawRay(origin, aimDirection * detectionRange, Color.red, 0.5f);
    }

    public void StopNavigation()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
    }

    public void RefreshNavigation()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }
    }

    // Рисуем зоны видимости в редакторе, чтобы было удобно настраивать
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}