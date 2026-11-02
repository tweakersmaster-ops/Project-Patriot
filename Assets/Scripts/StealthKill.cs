using UnityEngine;

// Тихое убийство: игрок стоит за спиной немца ближе 2 метров и нажимает E.
// Вешается на игрока.
public class StealthKill : MonoBehaviour
{
    [Header("Условия убийства")]
    public float killDistance = 2f;      // Максимальная дистанция
    public float behindDot = -0.35f;     // Насколько строго "за спиной" (dot < этого)
    public KeyCode killKey = KeyCode.E;

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            return;
        }

        if (!Input.GetKeyDown(killKey))
        {
            return;
        }

        EnemyAI victim = FindVictim();
        if (victim == null)
        {
            return;
        }

        Health health = victim.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(99999f);
        }
    }

    private EnemyAI FindVictim()
    {
        EnemyAI best = null;
        float bestDistance = killDistance;

        var enemies = EnemyAI.ActiveEnemyList;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            if (enemy.CompareTag("team") || enemy.GetComponent<TeamFollowPlayer>() != null)
            {
                continue;
            }

            Health health = enemy.GetComponent<Health>();
            if (health != null && health.IsDead)
            {
                continue;
            }

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            float distance = toEnemy.magnitude;
            if (distance > bestDistance || distance < 0.01f)
            {
                continue;
            }

            // Игрок должен быть сзади: вектор от врага к игроку смотрит против взгляда врага
            Vector3 enemyForward = enemy.transform.forward;
            enemyForward.y = 0f;
            Vector3 toPlayer = (transform.position - enemy.transform.position).normalized;
            float dot = Vector3.Dot(enemyForward.normalized, toPlayer);
            if (dot > behindDot)
            {
                continue;
            }

            best = enemy;
            bestDistance = distance;
        }

        return best;
    }
}
