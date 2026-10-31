using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameMenuManager : MonoBehaviour
{
    private const string MasterVolumePreference = "MasterVolume";
    private const string MainMenuSceneName = "MainMenu";
    private const float AutosaveIntervalSeconds = 180f;

    public static GameMenuManager Instance { get; private set; }

    public bool IsPaused => GameContext.Instance != null && GameContext.Instance.IsPaused;

    private Canvas menuCanvas;
    private GameObject menuPanel;
    private GameObject deathPanel;
    private GameObject settingsPanel;
    private GameObject exitConfirmPanel;
    private GameObject createdEventSystem;
    private Slider volumeSlider;
    private float autosaveTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateMenuUI();
        if (GameContext.Instance != null)
        {
            GameContext.Instance.PlayerDied += ShowDeathScreen;
        }

        // Главное меню — отдельная сцена, поэтому в игровой сцене старую панель не показываем
        if (menuCanvas != null)
        {
            menuCanvas.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsPaused)
        {
            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= AutosaveIntervalSeconds)
            {
                autosaveTimer = 0f;
                SaveSystem.SaveCurrentGame();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (exitConfirmPanel != null && exitConfirmPanel.activeSelf)
            {
                HideExitConfirmation();
                return;
            }

            if (deathPanel != null && deathPanel.activeSelf)
            {
                return;
            }

            if (menuPanel != null && menuPanel.activeSelf)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    private void OnDestroy()
    {
        if (GameContext.Instance != null)
        {
            GameContext.Instance.PlayerDied -= ShowDeathScreen;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PauseGame()
    {
        SetPaused(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem();
        if (menuCanvas != null)
        {
            menuCanvas.enabled = true;
        }
        HideExitConfirmation();
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        SetPaused(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (menuCanvas != null)
        {
            menuCanvas.enabled = false;
        }
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }
        HideExitConfirmation();
    }

    private void ShowDeathScreen(Health health)
    {
        SetPaused(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem();
        menuCanvas.enabled = true;
        menuPanel.SetActive(false);
        HideExitConfirmation();
        deathPanel.SetActive(true);
    }

    public void RestartCurrentScene()
    {
        if (GameContext.Instance != null)
        {
            GameContext.Instance.PrepareForGameplayScene();
        }
        ResumeGame();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Главное меню живёт в отдельной сцене: выходим в неё, а не показываем старую панель поверх игра
    public void ReturnToMainMenu()
    {
        SetPaused(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        DestroyCreatedEventSystem();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    // EventSystem создаём только когда действительно открывается меню паузы,
    // чтобы не плодить дубликаты с EventSystem сцены главного меню
    private void EnsureEventSystem()
    {
        if (createdEventSystem != null || FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        createdEventSystem = new GameObject("EventSystem");
        createdEventSystem.AddComponent<EventSystem>();
        createdEventSystem.AddComponent<StandaloneInputModule>();
    }

    private void DestroyCreatedEventSystem()
    {
        if (createdEventSystem == null)
        {
            return;
        }

        createdEventSystem.SetActive(false);
        Destroy(createdEventSystem);
        createdEventSystem = null;
    }

    public void ShowExitConfirmation()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(true);
        }
    }

    public void HideExitConfirmation()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(false);
        }
    }

    // «Отмена» — возвращаемся в меню паузы
    public void CancelExit()
    {
        HideExitConfirmation();
    }

    // «Всё равно выйти» — выходим без сохранения
    public void ExitWithoutSaving()
    {
        ReturnToMainMenu();
    }

    // «Сохранить и выйти»
    public void SaveAndExit()
    {
        SaveSystem.SaveCurrentGame();
        ReturnToMainMenu();
    }

    public void SaveGame()
    {
        SaveSystem.SaveCurrentGame();
    }

    private void SetPaused(bool paused)
    {
        if (GameContext.Instance != null)
        {
            GameContext.Instance.SetPaused(paused);
            return;
        }

        Time.timeScale = paused ? 0f : 1f;
    }

    public void ToggleSettings()
    {
        if (settingsPanel == null)
        {
            return;
        }

        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void QuitToMenu()
    {
        ShowExitConfirmation();
    }

    private void CreateMenuUI()
    {
        GameObject canvasObject = new GameObject("PauseCanvas");
        canvasObject.transform.SetParent(transform, false);

        menuCanvas = canvasObject.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.enabled = true;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        deathPanel = new GameObject("DeathPanel");
        deathPanel.transform.SetParent(canvasObject.transform, false);
        RectTransform deathRect = deathPanel.AddComponent<RectTransform>();
        deathRect.anchorMin = new Vector2(0.5f, 0.5f);
        deathRect.anchorMax = new Vector2(0.5f, 0.5f);
        deathRect.sizeDelta = new Vector2(560f, 340f);
        Image deathImage = deathPanel.AddComponent<Image>();
        deathImage.color = new Color(0.12f, 0.04f, 0.04f, 0.97f);
        CreateText(deathPanel.transform, "ВЫ ПОГИБЛИ", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -55f), Color.white);
        CreateButton(deathPanel.transform, "Заново", new Vector2(0.5f, 0.58f), new Vector2(300f, 60f), RestartCurrentScene);
        CreateButton(deathPanel.transform, "В меню", new Vector2(0.5f, 0.34f), new Vector2(300f, 60f), ShowExitConfirmation);
        deathPanel.SetActive(false);

        menuPanel = new GameObject("MenuPanel");
        menuPanel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = menuPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 500f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = menuPanel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        CreateText(menuPanel.transform, "ПАУЗА", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -55f), Color.white);

        CreateButton(menuPanel.transform, "Продолжить", new Vector2(0.5f, 0.7f), new Vector2(250f, 60f), ResumeGame);
        CreateButton(menuPanel.transform, "Сохраниться", new Vector2(0.5f, 0.58f), new Vector2(250f, 60f), SaveGame);
        CreateButton(menuPanel.transform, "Настройки", new Vector2(0.5f, 0.45f), new Vector2(250f, 60f), ToggleSettings);
        CreateButton(menuPanel.transform, "Выход в меню", new Vector2(0.5f, 0.3f), new Vector2(250f, 60f), QuitToMenu);

        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(menuPanel.transform, false);
        settingsPanel.SetActive(false);

        RectTransform settingsRect = settingsPanel.AddComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(0f, 0f);
        settingsRect.anchorMax = new Vector2(1f, 1f);
        settingsRect.offsetMin = new Vector2(20f, 20f);
        settingsRect.offsetMax = new Vector2(-20f, -20f);

        Image settingsImage = settingsPanel.AddComponent<Image>();
        settingsImage.color = new Color(0.14f, 0.14f, 0.18f, 0.95f);

        CreateText(settingsPanel.transform, "Настройки", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -60f), Color.white);
        CreateText(settingsPanel.transform, "Громкость", 18, TextAnchor.MiddleLeft, new Vector2(0.15f, 0.72f), new Vector2(0f, 0f), Color.white);

        GameObject sliderObject = new GameObject("VolumeSlider");
        sliderObject.transform.SetParent(settingsPanel.transform, false);

        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.15f, 0.58f);
        sliderRect.anchorMax = new Vector2(0.85f, 0.68f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        volumeSlider = sliderObject.AddComponent<Slider>();
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePreference, 0.7f));
        AudioListener.volume = volumeSlider.value;
        volumeSlider.onValueChanged.AddListener(SetMasterVolume);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fillArea.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(10f, 6f);
        fillRect.offsetMax = new Vector2(-10f, -6f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillTransform = fill.AddComponent<RectTransform>();
        fillTransform.anchorMin = new Vector2(0f, 0f);
        fillTransform.anchorMax = new Vector2(1f, 1f);
        fillTransform.offsetMin = Vector2.zero;
        fillTransform.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.7f, 1f, 1f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(sliderObject.transform, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 20f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        volumeSlider.fillRect = fillTransform;
        volumeSlider.targetGraphic = handleImage;
        volumeSlider.handleRect = handleRect;

        CreateButton(settingsPanel.transform, "Назад", new Vector2(0.5f, 0.18f), new Vector2(200f, 50f), ToggleSettings);

        // Предупреждение о потере несохранённого прогресса
        exitConfirmPanel = new GameObject("ExitConfirmPanel");
        exitConfirmPanel.transform.SetParent(canvasObject.transform, false);

        RectTransform confirmRect = exitConfirmPanel.AddComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.5f, 0.5f);
        confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRect.sizeDelta = new Vector2(780f, 360f);
        confirmRect.anchoredPosition = Vector2.zero;

        Image confirmImage = exitConfirmPanel.AddComponent<Image>();
        confirmImage.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

        CreateText(exitConfirmPanel.transform, "ВЫЙТИ В ГЛАВНОЕ МЕНЮ?", 30, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -55f), Color.white);

        GameObject warningObject = CreateText(exitConfirmPanel.transform,
            "Весь несохранённый прогресс будет утерян.", 20, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Color(0.95f, 0.7f, 0.6f, 1f));
        warningObject.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 80f);

        Button cancelButton = CreateButton(exitConfirmPanel.transform, "Отмена",
            new Vector2(0.5f, 0f), new Vector2(210f, 60f), CancelExit);
        cancelButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-240f, 70f);

        Button exitButton = CreateButton(exitConfirmPanel.transform, "Всё равно выйти",
            new Vector2(0.5f, 0f), new Vector2(210f, 60f), ExitWithoutSaving);
        exitButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 70f);

        // Кнопка сохранения выделена белым фоном, остальные — обычные
        Button saveExitButton = CreateButton(exitConfirmPanel.transform, "Сохранить и выйти",
            new Vector2(0.5f, 0f), new Vector2(210f, 60f), SaveAndExit);
        saveExitButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(240f, 70f);
        saveExitButton.GetComponent<Image>().color = Color.white;
        Text saveExitLabel = saveExitButton.GetComponentInChildren<Text>();
        if (saveExitLabel != null)
        {
            saveExitLabel.color = new Color(0.10f, 0.13f, 0.16f, 1f);
        }

        exitConfirmPanel.SetActive(false);
    }

    private void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(MasterVolumePreference, volume);
        PlayerPrefs.Save();
    }

    private Button CreateButton(Transform parent, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(text + "Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = anchor;
        buttonRect.anchorMax = anchor;
        buttonRect.sizeDelta = size;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.45f, 0.8f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 20;
        label.color = Color.white;
        label.raycastTarget = false;
        return button;
    }

    private GameObject CreateText(Transform parent, string text, int fontSize, TextAnchor alignment, Vector2 anchor, Vector2 position, Color color)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(500f, 60f);
        rect.anchoredPosition = position;

        Text uiText = textObj.AddComponent<Text>();
        uiText.text = text;
        uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = color;
        uiText.raycastTarget = false;
        return textObj;
    }
}
