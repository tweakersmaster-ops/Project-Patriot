using System.Collections.Generic;
using UnityEngine;

// Срабатывает, когда в указанных группах не осталось живых врагов.
// Подписывается на Health.AnyEntityDied и один раз проверяет, зачищена ли зона.
public class AreaClearTrigger : MonoBehaviour
{
    [Header("Группы врагов")]
    public Transform[] enemyGroups;

    [Header("Титры")]
    [TextArea(3, 5)]
    public string[] phrases;

    [Header("Квесты")]
    public string questToComplete;
    public string questToAdd;

    [Header("Активировать при срабатывании")]
    public GameObject[] objectsToActivate;

    private readonly List<Health> tracked = new List<Health>();
    private bool hasTriggered;

    private void Start()
    {
        CollectEnemies();
        Health.AnyEntityDied += HandleAnyEntityDied;
        CheckCleared();
    }

    private void OnDestroy()
    {
        Health.AnyEntityDied -= HandleAnyEntityDied;
    }

    private void CollectEnemies()
    {
        if (enemyGroups == null)
        {
            return;
        }

        for (int i = 0; i < enemyGroups.Length; i++)
        {
            if (enemyGroups[i] == null)
            {
                continue;
            }

            Health[] found = enemyGroups[i].GetComponentsInChildren<Health>(true);
            for (int j = 0; j < found.Length; j++)
            {
                // У модели тоже есть Health (destroyOnDeath), поэтому считаем
                // только настоящих врагов — тех, у кого есть боевой ИИ.
                if (found[j] != null && found[j].CompareTag("enemy") &&
                    (found[j].GetComponent<EnemyAI>() != null || found[j].GetComponent<GuardPatrolAI>() != null))
                {
                    tracked.Add(found[j]);
                }
            }
        }
    }

    private void HandleAnyEntityDied(Health entity)
    {
        CheckCleared();
    }

    private void CheckCleared()
    {
        if (hasTriggered || tracked.Count == 0)
        {
            return;
        }

        for (int i = 0; i < tracked.Count; i++)
        {
            // Убитый враг уничтожается (destroyOnDeath), поэтому null тоже мёртв.
            if (tracked[i] != null && !tracked[i].IsDead)
            {
                return;
            }
        }

        hasTriggered = true;
        Health.AnyEntityDied -= HandleAnyEntityDied;

        if (phrases != null && phrases.Length > 0 && SubtitleManager.Instance != null)
        {
            SubtitleManager.Instance.ShowSubtitles(phrases);
        }

        QuestManager questManager = QuestManager.Instance;
        if (questManager != null)
        {
            if (!string.IsNullOrWhiteSpace(questToComplete))
            {
                questManager.CompleteQuest(questToComplete);
            }

            if (!string.IsNullOrWhiteSpace(questToAdd))
            {
                questManager.AddQuest(questToAdd);
            }
        }
        else
        {
            Debug.LogWarning("AreaClearTrigger: нет QuestManager на сцене.", this);
        }

        if (objectsToActivate != null)
        {
            for (int i = 0; i < objectsToActivate.Length; i++)
            {
                if (objectsToActivate[i] != null)
                {
                    objectsToActivate[i].SetActive(true);
                }
            }
        }
    }
}
