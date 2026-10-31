using UnityEngine;

public class GameStart : MonoBehaviour
{
    private void Start()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogError("GameStart requires an active QuestManager.", this);
            return;
        }

        QuestManager.Instance.AddQuest("подслушайте диалог немцев");
    }
}