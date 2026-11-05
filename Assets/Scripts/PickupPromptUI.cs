using UnityEngine;
using UnityEngine.UI;

// Подсказка по центру снизу: «Нажмите G, чтобы подобрать».
// Создаёт собственный Canvas при первом обращении, чтобы не трогать HUD.
public class PickupPromptUI : MonoBehaviour
{
    private static PickupPromptUI instance;

    private Canvas canvas;
    private Text promptText;
    private int showFrame = -1;

    public static void Show(string message)
    {
        EnsureInstance();
        if (instance == null)
        {
            return;
        }

        instance.showFrame = Time.frameCount;
        instance.promptText.text = message;
        instance.promptText.enabled = true;
    }

    public static void Hide()
    {
        if (instance != null && instance.promptText != null)
        {
            instance.promptText.enabled = false;
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        PickupPromptUI existing = FindObjectOfType<PickupPromptUI>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("PickupPromptUI");
        instance = host.AddComponent<PickupPromptUI>();
    }

    private void Awake()
    {
        instance = this;
        BuildUi();
    }

    private void LateUpdate()
    {
        // Подсказку обновляют сами пикапы каждый кадр, пока игрок в зоне.
        // Если кадр прошёл без обновления — прячем.
        if (promptText != null && promptText.enabled && Time.frameCount - showFrame > 1)
        {
            promptText.enabled = false;
        }
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("PickupPromptCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("PickupPromptText");
        textObject.transform.SetParent(canvasObject.transform, false);
        promptText = textObject.AddComponent<Text>();
        promptText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        promptText.fontSize = 26;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = new Color(1f, 0.92f, 0.6f, 1f);
        promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = promptText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 170f);
        rect.sizeDelta = new Vector2(700f, 50f);

        promptText.enabled = false;
    }
}
