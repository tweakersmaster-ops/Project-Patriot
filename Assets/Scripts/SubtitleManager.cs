using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SubtitleManager : MonoBehaviour
{
    public static SubtitleManager Instance;

    [Header("UI Компоненты")]
    public CanvasGroup subtitleCanvasGroup;
    public TextMeshProUGUI subtitleText;

    [Header("Настройки")]
    public float fadeSpeed = 2f;
    public float timePerWord = 0.3f;
    public float extraWaitTime = 1.5f;

    private readonly Queue<string> subtitlesQueue = new Queue<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (subtitleCanvasGroup == null || subtitleText == null)
        {
            Debug.LogError("SubtitleManager requires a CanvasGroup and a TextMeshProUGUI reference.", this);
            enabled = false;
            return;
        }

        subtitleCanvasGroup.alpha = 0f;
        subtitleCanvasGroup.interactable = false;
        subtitleCanvasGroup.blocksRaycasts = false;
    }

    public void ShowSubtitles(string[] phrases)
    {
        if (subtitleCanvasGroup == null || subtitleText == null)
        {
            Debug.LogError("Cannot show subtitles because the UI references are missing.", this);
            return;
        }

        if (phrases == null || phrases.Length == 0)
        {
            return;
        }

        StopAllCoroutines();
        subtitlesQueue.Clear();

        subtitleCanvasGroup.alpha = 0f;
        subtitleCanvasGroup.interactable = false;
        subtitleCanvasGroup.blocksRaycasts = false;

        foreach (string phrase in phrases)
        {
            if (!string.IsNullOrWhiteSpace(phrase))
            {
                subtitlesQueue.Enqueue(phrase);
            }
        }

        if (subtitlesQueue.Count > 0)
        {
            StartCoroutine(PlayQueueRoutine());
        }
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (subtitlesQueue.Count > 0)
        {
            string currentPhrase = subtitlesQueue.Dequeue();
            subtitleText.text = currentPhrase;

            yield return StartCoroutine(FadeCanvas(1f));

            float waitTime = CountWords(currentPhrase) * Mathf.Max(0f, timePerWord) +
                             Mathf.Max(0f, extraWaitTime);
            yield return new WaitForSeconds(waitTime);

            yield return StartCoroutine(FadeCanvas(0f));
            yield return new WaitForSeconds(0.5f);
        }

    }

    private IEnumerator FadeCanvas(float targetAlpha)
    {
        subtitleCanvasGroup.interactable = false;
        subtitleCanvasGroup.blocksRaycasts = false;
        while (Mathf.Abs(subtitleCanvasGroup.alpha - targetAlpha) > 0.01f)
        {
            subtitleCanvasGroup.alpha = Mathf.MoveTowards(
                subtitleCanvasGroup.alpha,
                targetAlpha,
                Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
            yield return null;
        }

        subtitleCanvasGroup.alpha = targetAlpha;
    }

    private static int CountWords(string text)
    {
        int wordCount = 0;
        bool insideWord = false;
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                insideWord = false;
            }
            else if (!insideWord)
            {
                insideWord = true;
                wordCount++;
            }
        }

        return wordCount;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}