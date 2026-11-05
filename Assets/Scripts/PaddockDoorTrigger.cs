using UnityEngine;

// Триггер на открытие двери питомника. Работает как StoryTrigger,
// но срабатывает не по входу в зону, а по событию открытия PaddockDoor:
// показывает титры, закрывает старый квест и выдаёт новый.
public class PaddockDoorTrigger : MonoBehaviour
{
    [Header("Ссылки")]
    public PaddockDoor door;

    [Header("Титры")]
    [TextArea(3, 5)]
    public string[] phrases;

    [Header("Квесты")]
    public string questToComplete;
    public string questToAdd;

    private bool hasTriggered;

    private void Start()
    {
        if (door == null)
        {
            door = GetComponent<PaddockDoor>();
        }

        if (door == null)
        {
            Debug.LogWarning("PaddockDoorTrigger не нашёл PaddockDoor.", this);
            return;
        }

        door.Opened += HandleDoorOpened;

        // Если дверь уже открыли до создания триггера — догоняем событие.
        if (door.IsOpen)
        {
            HandleDoorOpened();
        }
    }

    private void OnDestroy()
    {
        if (door != null)
        {
            door.Opened -= HandleDoorOpened;
        }
    }

    private void HandleDoorOpened()
    {
        if (hasTriggered)
        {
            return;
        }

        hasTriggered = true;

        if (phrases != null && phrases.Length > 0 && SubtitleManager.Instance != null)
        {
            SubtitleManager.Instance.ShowSubtitles(phrases);
        }

        QuestManager questManager = QuestManager.Instance;
        if (questManager == null)
        {
            Debug.LogWarning("PaddockDoorTrigger: нет QuestManager на сцене.", this);
            return;
        }

        if (!string.IsNullOrWhiteSpace(questToComplete))
        {
            questManager.CompleteQuest(questToComplete);
        }

        if (!string.IsNullOrWhiteSpace(questToAdd))
        {
            questManager.AddQuest(questToAdd);
        }
    }
}
