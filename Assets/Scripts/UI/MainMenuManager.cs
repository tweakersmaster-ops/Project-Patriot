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
    public void StartGame() { Load(firstScene); }
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
