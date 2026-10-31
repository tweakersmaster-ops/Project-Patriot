using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public Transform questContainer;
    public GameObject questTextPrefab;

    private readonly Dictionary<string, TextMeshProUGUI> activeQuests =
        new Dictionary<string, TextMeshProUGUI>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddQuest(string questText)
    {
        if (string.IsNullOrWhiteSpace(questText))
        {
            Debug.LogWarning("Cannot add a quest with empty text.", this);
            return;
        }

        if (questContainer == null)
        {
            Debug.LogError("QuestManager requires a quest container.", this);
            return;
        }

        if (activeQuests.ContainsKey(questText))
        {
            return;
        }

        GameObject newQuestObj = questTextPrefab != null
            ? Instantiate(questTextPrefab, questContainer)
            : CreateQuestTextObject();
        TextMeshProUGUI textComp = newQuestObj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (textComp == null)
        {
            Debug.LogError("Quest text prefab must contain a TextMeshProUGUI component.", newQuestObj);
            Destroy(newQuestObj);
            return;
        }

        textComp.text = "• " + questText;
        activeQuests.Add(questText, textComp);
    }

    public void CompleteQuest(string questText)
    {
        if (string.IsNullOrWhiteSpace(questText))
        {
            Debug.LogWarning("Cannot complete a quest with empty text.", this);
            return;
        }

        if (activeQuests.TryGetValue(questText, out TextMeshProUGUI textComp))
        {
            textComp.text = "<s><color=#888888>" + textComp.text + "</color></s>";
            activeQuests.Remove(questText);
        }
        else
        {
            Debug.LogWarning("Cannot complete a quest that is not active: " + questText, this);
        }
    }

    private GameObject CreateQuestTextObject()
    {
        GameObject questObject = new GameObject(
            "QuestText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        questObject.transform.SetParent(questContainer, false);

        TextMeshProUGUI text = questObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 22f;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        LayoutElement layoutElement = questObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 32f;
        layoutElement.preferredHeight = 32f;

        RectTransform containerRect = questContainer as RectTransform;
        if (containerRect != null)
        {
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(12f, 12f);
            containerRect.offsetMax = new Vector2(-12f, -12f);
        }

        VerticalLayoutGroup layout = questContainer.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = questContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return questObject;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}