using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Экран загрузки: асинхронная загрузка сцены + прогресс-бар 0..1
public class LoadingScreen : MonoBehaviour
{
    [Header("Прогресс")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text progressText;

    [Header("Настройки")]
    [Tooltip("Минимальное время показа экрана, сек")]
    [SerializeField] private float minShowTime = 0.5f;

    private bool isLoading;

    public bool IsLoading { get { return isLoading; } }

    // Вызывается кнопками меню
    public void LoadScene(string sceneName)
    {
        if (isLoading) return;

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[LoadingScreen] Сцена не найдена в Build Settings: \"" + sceneName + "\"");
            return;
        }

        isLoading = true;
        gameObject.SetActive(true);
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        SetProgress(0f);
        yield return null;

        // Максимальный приоритет фоновой загрузки — сцена грузится заметно быстрее
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.High;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Application.backgroundLoadingPriority = previousPriority;
            isLoading = false;
            yield break;
        }

        // Ждём загрузки, не активируя сцену, чтобы успеть дорисовать полоску
        op.allowSceneActivation = false;
        float startTime = Time.realtimeSinceStartup;

        while (op.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(op.progress / 0.9f) * 0.95f);
            yield return null;
        }

        while (Time.realtimeSinceStartup - startTime < minShowTime)
            yield return null;

        SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.15f);

        Application.backgroundLoadingPriority = previousPriority;
        op.allowSceneActivation = true;
    }

    private void SetProgress(float value)
    {
        if (progressBar != null) progressBar.value = value;
        if (progressText != null)
            progressText.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
