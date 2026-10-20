using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    private Canvas canvas;
    private Image healthFill;
    private Image staminaFill;
    private Text healthText;
    private Text staminaText;
    private Text weaponText;
    private Text ammoText;
    private Text objectiveText;
    private Image[] crosshairLines;

    private Health playerHealth;
    private PlayerController playerController;
    private WeaponController weaponController;

    private void Start()
    {
        BuildHUD();

        playerHealth = GetComponentInParent<Health>(true);
        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<Health>();
        }

        playerController = GetComponentInParent<PlayerController>(true);
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        weaponController = GetComponentInParent<WeaponController>(true);
        if (weaponController == null)
        {
            weaponController = FindObjectOfType<WeaponController>();
        }
    }

    private void Update()
    {
        if (canvas != null && GameContext.Instance != null)
        {
            canvas.enabled = !GameContext.Instance.IsPaused;
        }

        if (playerHealth != null && healthFill != null)
        {
            float healthFraction = playerHealth.maxHealth > 0f
                ? Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.maxHealth)
                : 0f;
            healthFill.fillAmount = healthFraction;
            healthFill.color = GetHealthColor(healthFraction);
        }

        if (playerHealth != null && healthText != null)
        {
            healthText.text = "HP: " + Mathf.RoundToInt(playerHealth.CurrentHealth) + " / " + Mathf.RoundToInt(playerHealth.maxHealth);
        }

        if (playerController != null)
        {
            float staminaFraction = playerController.StaminaNormalized;
            if (staminaFill != null)
            {
                staminaFill.fillAmount = staminaFraction;
                staminaFill.color = Color.Lerp(
                    new Color(0.85f, 0.22f, 0.12f, 1f),
                    new Color(0.95f, 0.78f, 0.2f, 1f),
                    staminaFraction);
            }

            if (staminaText != null)
            {
                staminaText.text = "STAMINA: " +
                                   Mathf.CeilToInt(playerController.CurrentStamina) + " / " +
                                   Mathf.CeilToInt(playerController.MaxStamina);
            }
        }

        if (weaponController != null)
        {
            if (weaponText != null)
            {
                weaponText.text = weaponController.CurrentWeapon != null ? weaponController.CurrentWeapon.displayName : "No weapon";
            }

            if (ammoText != null)
            {
                ammoText.text = weaponController.CurrentWeapon != null
                    ? weaponController.CurrentAmmo + " / " + weaponController.CurrentReserveAmmo
                    : "0 / 0";
            }
        }
    }

    private void BuildHUD()
    {
        GameObject canvasObject = new GameObject("PlayerHUD");
        canvasObject.transform.SetParent(transform);
        canvasObject.transform.localPosition = Vector3.zero;

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject healthPanel = new GameObject("HealthPanel");
        healthPanel.transform.SetParent(canvasObject.transform, false);
        RectTransform healthRect = healthPanel.AddComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0f, 1f);
        healthRect.anchorMax = new Vector2(0f, 1f);
        healthRect.pivot = new Vector2(0f, 1f);
        healthRect.anchoredPosition = new Vector2(40f, -40f);
        healthRect.sizeDelta = new Vector2(300f, 28f);

        Image healthBackground = healthPanel.AddComponent<Image>();
        healthBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        healthBackground.raycastTarget = false;

        GameObject healthFillObject = new GameObject("HealthFill");
        healthFillObject.transform.SetParent(healthPanel.transform, false);
        RectTransform fillRect = healthFillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);

        healthFill = healthFillObject.AddComponent<Image>();
        Texture2D whiteTexture = Texture2D.whiteTexture;
        healthFill.sprite = Sprite.Create(
            whiteTexture,
            new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
            new Vector2(0.5f, 0.5f));
        healthFill.color = new Color(0.2f, 0.8f, 0.35f, 1f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.raycastTarget = false;

        healthText = CreateText(canvasObject.transform, "HP: 100 / 100", new Vector2(0f, 1f), 18, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 1f));
        healthText.rectTransform.anchoredPosition = new Vector2(40f, -80f);

        GameObject staminaPanel = new GameObject("StaminaPanel");
        staminaPanel.transform.SetParent(canvasObject.transform, false);
        RectTransform staminaRect = staminaPanel.AddComponent<RectTransform>();
        staminaRect.anchorMin = new Vector2(0f, 1f);
        staminaRect.anchorMax = new Vector2(0f, 1f);
        staminaRect.pivot = new Vector2(0f, 1f);
        staminaRect.anchoredPosition = new Vector2(40f, -112f);
        staminaRect.sizeDelta = new Vector2(300f, 18f);
        Image staminaBackground = staminaPanel.AddComponent<Image>();
        staminaBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        staminaBackground.raycastTarget = false;

        GameObject staminaFillObject = new GameObject("StaminaFill");
        staminaFillObject.transform.SetParent(staminaPanel.transform, false);
        RectTransform staminaFillRect = staminaFillObject.AddComponent<RectTransform>();
        staminaFillRect.anchorMin = Vector2.zero;
        staminaFillRect.anchorMax = Vector2.one;
        staminaFillRect.offsetMin = new Vector2(3f, 3f);
        staminaFillRect.offsetMax = new Vector2(-3f, -3f);
        staminaFill = staminaFillObject.AddComponent<Image>();
        staminaFill.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f));
        staminaFill.type = Image.Type.Filled;
        staminaFill.fillMethod = Image.FillMethod.Horizontal;
        staminaFill.raycastTarget = false;

        staminaText = CreateText(canvasObject.transform, "STAMINA: 6 / 6", new Vector2(0f, 1f), 14, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 1f));
        staminaText.rectTransform.anchoredPosition = new Vector2(40f, -134f);

        weaponText = CreateText(canvasObject.transform, "StG 44", new Vector2(1f, 0f), 22, TextAnchor.UpperRight, new Color(1f, 0.9f, 0.3f, 1f));
        weaponText.rectTransform.anchoredPosition = new Vector2(-40f, 40f);

        ammoText = CreateText(canvasObject.transform, "30 / 120", new Vector2(1f, 0f), 20, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 1f));
        ammoText.rectTransform.anchoredPosition = new Vector2(-40f, 70f);

        objectiveText = CreateText(canvasObject.transform, "Objective: break through the enemy line", new Vector2(0.5f, 1f), 20, TextAnchor.UpperCenter, new Color(0.9f, 0.9f, 0.9f, 1f));
        objectiveText.rectTransform.anchoredPosition = new Vector2(0f, -30f);

        CreateCrosshair(canvasObject.transform);
    }

    private Color GetHealthColor(float healthFraction)
    {
        Color red = new Color(0.9f, 0.12f, 0.1f, 1f);
        Color orange = new Color(1f, 0.55f, 0.05f, 1f);
        Color green = new Color(0.2f, 0.8f, 0.35f, 1f);

        return healthFraction < 0.5f
            ? Color.Lerp(red, orange, healthFraction * 2f)
            : Color.Lerp(orange, green, (healthFraction - 0.5f) * 2f);
    }

    private Text CreateText(Transform parent, string message, Vector2 anchorMin, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject("HUDText");
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMin;
        rect.pivot = anchorMin;

        Text text = textObject.AddComponent<Text>();
        text.text = message;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        return text;
    }

    private void CreateCrosshair(Transform parent)
    {
        crosshairLines = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject line = new GameObject("CrosshairLine" + i);
            line.transform.SetParent(parent, false);
            RectTransform rect = line.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(i % 2 == 0 ? 20f : 3f, i % 2 == 0 ? 3f : 20f);
            rect.anchoredPosition = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = line.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.85f);
            image.raycastTarget = false;
            crosshairLines[i] = image;
        }

        crosshairLines[0].rectTransform.anchoredPosition = new Vector2(-12f, 0f);
        crosshairLines[1].rectTransform.anchoredPosition = new Vector2(12f, 0f);
        crosshairLines[2].rectTransform.anchoredPosition = new Vector2(0f, -12f);
        crosshairLines[3].rectTransform.anchoredPosition = new Vector2(0f, 12f);
    }
}
