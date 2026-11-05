using UnityEngine;
using UnityEngine.SceneManagement;

// Управление главным меню: панели + загрузка сцен
public class MainMenuManager : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject debugPanel;

    [Header("Экраны")]
    [SerializeField] private LoadingScreen loadingScreen;

    [Header("Фон")]
    [SerializeField] private UnityEngine.Video.VideoPlayer backgroundVideo;

    [Header("Сцены (имена как в Build Settings)")]
    [SerializeField] private string firstScene = "Polock";
    [SerializeField] private string bridgeScene = "Most";
    [SerializeField] private string debugScene = "Debug";

    private GameMenuManager legacyMenu;

    private void Start()
    {
        // GameContext создаёт старую систему паузы в любой сцене: в главном меню она
        // перекрывает видео и держит Time.timeScale = 0 (VideoPlayer стоит на месте).
        legacyMenu = FindObjectOfType<GameMenuManager>();
        if (legacyMenu != null)
        {
            legacyMenu.ResumeGame();
            legacyMenu.enabled = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // VideoPlayer стартует в Awake — раньше, чем GameContext обнулит timeScale,
        // поэтому после снятия паузы запускаем воспроизведение заново
        StartBackgroundVideo();

        SettingsMenu.ApplySavedSettings();
        ShowMain();

        // «Продолжить» появляется только если есть сохранение.
        SetupContinueButton();
        CreateVersionLabel();
    }

    // Подпись сборки в правом нижнем углу главного меню.
    private void CreateVersionLabel()
    {
        // Собственный оверлей, чтобы подпись не зависела от того, какой Canvas
        // найден в сцене и какая у него раскладка.
        GameObject canvasObject = new GameObject("VersionLabelCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();

        GameObject labelObject = new GameObject("VersionLabel");
        labelObject.transform.SetParent(canvasObject.transform, false);

        UnityEngine.UI.Text label = labelObject.AddComponent<UnityEngine.UI.Text>();
        label.text = "alpha 0.1 designed by SGK";
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 18;
        label.alignment = TextAnchor.LowerRight;
        label.color = new Color(1f, 1f, 1f, 0.85f);
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(420f, 30f);
        rect.anchoredPosition = new Vector2(-24f, 20f);
    }

    // Кнопка «Продолжить» между «Начать игру» и «Настройки»: остальные
    // кнопки сдвигаются вниз на один шаг. Без сохранения меню не меняется.
    private void SetupContinueButton()
    {
        if (mainMenuPanel == null || !SaveSystem.HasSave)
        {
            return;
        }

        Transform startButton = mainMenuPanel.transform.Find("Button_Start");
        if (startButton == null)
        {
            return;
        }

        RectTransform startRect = startButton.GetComponent<RectTransform>();
        if (startRect == null)
        {
            return;
        }

        // Шаг берём из расстояния до соседней кнопки (по умолчанию 130 px).
        float spacing = 130f;
        Transform settingsButton = mainMenuPanel.transform.Find("Button_Settings");
        if (settingsButton != null && settingsButton.GetComponent<RectTransform>() != null)
        {
            spacing = Mathf.Abs(startRect.anchoredPosition.y -
                                settingsButton.GetComponent<RectTransform>().anchoredPosition.y);
        }

        // Сдвигаем все кнопки ниже «Начать игру» на один шаг вниз.
        UnityEngine.UI.Button[] buttons = mainMenuPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].transform.parent != mainMenuPanel.transform)
            {
                continue;
            }

            RectTransform rect = buttons[i].GetComponent<RectTransform>();
            if (rect != null && rect.anchoredPosition.y < startRect.anchoredPosition.y - 1f)
            {
                rect.anchoredPosition -= new Vector2(0f, spacing);
            }
        }

        GameObject continueObject = Instantiate(startButton.gameObject, startButton.parent);
        continueObject.name = "Button_Continue";
        continueObject.transform.SetSiblingIndex(startButton.GetSiblingIndex() + 1);

        RectTransform continueRect = continueObject.GetComponent<RectTransform>();
        continueRect.anchoredPosition = startRect.anchoredPosition + new Vector2(0f, -spacing);

        UnityEngine.UI.Button continueButton = continueObject.GetComponent<UnityEngine.UI.Button>();
        if (continueButton != null)
        {
            continueButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            continueButton.onClick.AddListener(LoadContinue);
        }

        TMPro.TMP_Text label = continueObject.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (label != null)
        {
            label.text = "Продолжить";
        }
    }

    public void LoadContinue()
    {
        if (!SaveSystem.HasSave)
        {
            StartGame();
            return;
        }

        if (GameContext.Instance != null)
        {
            GameContext.Instance.PrepareForGameplayScene();
        }

        if (legacyMenu != null)
        {
            legacyMenu.enabled = true;
        }

        if (backgroundVideo != null)
        {
            backgroundVideo.Stop();
        }

        // SaveSystem сам загрузит сцену и восстановит состояние.
        SaveSystem.LoadGame();
    }

    private void StartBackgroundVideo()
    {
        if (backgroundVideo == null) return;
        backgroundVideo.playOnAwake = false;
        backgroundVideo.Stop();
        backgroundVideo.Play();
        StartCoroutine(EnsureVideoPlaying());
    }

    // Play() сразу после Stop() может потеряться, пока плеер готовится —
    // дожидаемся готовности и запускаем повторно
    private System.Collections.IEnumerator EnsureVideoPlaying()
    {
        float timeout = 15f;
        while (backgroundVideo != null && timeout > 0f)
        {
            if (backgroundVideo.isPlaying) yield break;
            if (backgroundVideo.isPrepared) backgroundVideo.Play();
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void ShowMain()
    {
        SetPanels(true, false, false);
    }

    public void OpenSettings()
    {
        SetPanels(false, true, false);
    }

    public void OpenDebug()
    {
        SetPanels(false, false, true);
    }

    // --- кнопки ---
    public void StartGame()
    {
        // Новая игра: старое сохранение удаляем, новое создастся после загрузки сцены.
        SaveSystem.DeleteSave();
        SaveSystem.PrepareNewGame();
        Load(firstScene);
    }
    public void LoadPolock() { Load(firstScene); }
    public void LoadBridge() { Load(bridgeScene); }
    public void LoadDebugMap() { Load(debugScene); }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // --- служебное ---
    public void CloseSettings() { ShowMain(); }
    public void CloseDebug() { ShowMain(); }

    private void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[MainMenu] Сцена не найдена в Build Settings: \"" + sceneName + "\"");
            return;
        }

        // Игровая сцена должна стартовать сразу, без старого экрана паузы
        if (GameContext.Instance != null)
            GameContext.Instance.PrepareForGameplayScene();

        // Возвращаем штатную систему паузы (она нужна уже в самой игре)
        if (legacyMenu != null)
            legacyMenu.enabled = true;

        // Останавливаем фоновое видео — иначе оно ест ресурсы во время загрузки
        if (backgroundVideo != null)
            backgroundVideo.Stop();

        if (loadingScreen != null)
            loadingScreen.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    private void SetPanels(bool main, bool settings, bool debug)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(main);
        if (settingsPanel != null) settingsPanel.SetActive(settings);
        if (debugPanel != null) debugPanel.SetActive(debug);
    }
}
