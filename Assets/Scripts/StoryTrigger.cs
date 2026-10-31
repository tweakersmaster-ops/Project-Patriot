using UnityEngine;

public class StoryTrigger : MonoBehaviour
{
    [Header("Титры")]
    [TextArea(3, 5)]
    public string[] phrases;

    [Header("Квесты")]
    public string questToComplete;
    public string questToAdd;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        hasTriggered = true;

        if (phrases != null && phrases.Length > 0)
        {
            if (SubtitleManager.Instance != null)
            {
                SubtitleManager.Instance.ShowSubtitles(phrases);
            }
            else
            {
                Debug.LogWarning("Story trigger has subtitles but no SubtitleManager is active.", this);
            }
        }

        QuestManager questManager = QuestManager.Instance;
        if (questManager == null)
        {
            Debug.LogWarning("Story trigger has quest updates but no QuestManager is active.", this);
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