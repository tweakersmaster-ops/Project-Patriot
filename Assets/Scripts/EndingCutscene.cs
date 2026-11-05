using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Финальная катсцена: когда в мерии убит последний немец, перед игроком на
// третьем этаже появляется лейтенант Соколов (тот же солдат, что и в
// катсцене питомника). Короткий диалог, затемнение и переход на карту Гомеля.
public class EndingCutscene : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Группа Mariya_Guards: сцена начнётся, когда в ней не останется живых")]
    public Transform mariyaGuards;
    [Tooltip("Солдат из катсцены питомника (Prisoner_Soldier_1)")]
    public GameObject speaker;
    [Tooltip("Точка на третьем этаже мерии, где встречаются герой и лейтенант")]
    public Transform meetingPoint;

    [Header("Диалог")]
    [TextArea(2, 4)]
    public string[] phrases = new string[]
    {
        "Соколов: Как зовут-то тебя, боец?",
        "Алексей: Алексей Рязько.",
        "Соколов: Меня — лейтенант Соколов. Будешь на благо Родины служить и дальше?",
        "Алексей: Так точно!",
        "Соколов: На другой ответ я и не рассчитывал."
    };

    [Header("Затемнение")]
    [TextArea(2, 4)]
    public string dateText = "Спустя пол года, на фронтовой территории Гомеля";
    public float fadeDuration = 2f;
    public float dateHoldTime = 3.5f;

    [Header("Переход")]
    public string nextScene = "Gomel";

    private readonly List<Health> tracked = new List<Health>();
    private bool started;
    private PlayerController playerController;
    private bool playerWasEnabled = true;
    private float nextCheckTime;

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private TextMeshProUGUI overlayText;

    private void Start()
    {
        CollectGuards();
        Health.AnyEntityDied += HandleAnyEntityDied;
        // На случай, если всех уже убили до включения сцены.
        CheckCleared();
    }

    private void OnDestroy()
    {
        Health.AnyEntityDied -= HandleAnyEntityDied;
    }

    private void Update()
    {
        if (started)
        {
            return;
        }

        // Подстраховка: часть врагов может погибнуть, не вызвав событие
        // (например, уничтожение объекта), поэтому раз в секунду сверяемся.
        if (Time.time < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.time + 1f;
        CheckCleared();
    }

    private void CollectGuards()
    {
        if (mariyaGuards == null)
        {
            return;
        }

        Health[] found = mariyaGuards.GetComponentsInChildren<Health>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].CompareTag("enemy"))
            {
                tracked.Add(found[i]);
            }
        }
    }

    private void HandleAnyEntityDied(Health entity)
    {
        CheckCleared();
    }

    private void CheckCleared()
    {
        if (started || tracked.Count == 0)
        {
            return;
        }

        for (int i = 0; i < tracked.Count; i++)
        {
            if (tracked[i] != null && !tracked[i].IsDead)
            {
                return;
            }
        }

        started = true;
        Health.AnyEntityDied -= HandleAnyEntityDied;
        StartCoroutine(RunEnding());
    }

    private IEnumerator RunEnding()
    {
        FreezePlayer();

        if (speaker != null)
        {
            // Солдат не должен идти за игроком и стрелять во время сцены.
            SetSpeakerActive(false);
            MoveSpeakerToMeetingPoint();
        }

        PlacePlayerInFrontOfSpeaker();
        FaceEachOther();

        if (SubtitleManager.Instance != null && phrases != null && phrases.Length > 0)
        {
            SubtitleManager.Instance.ShowSubtitles(phrases);
            float deadline = Time.time + 120f;
            while (SubtitleManager.Instance.IsShowing && Time.time < deadline)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }

        yield return new WaitForSeconds(0.6f);

        CreateOverlay();
        yield return StartCoroutine(FadeOverlay(1f));

        // Надпись на чёрном экране.
        if (overlayText != null)
        {
            overlayText.text = dateText;
            yield return StartCoroutine(FadeText(1f));
        }

        yield return new WaitForSeconds(dateHoldTime);

        if (!string.IsNullOrEmpty(nextScene) &&
            Application.CanStreamedLevelBeLoaded(nextScene))
        {
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            Debug.LogWarning("EndingCutscene: сцена не добавлена в Build Settings: " + nextScene, this);
        }
    }

    private void FreezePlayer()
    {
        playerController = GameContext.Instance != null && GameContext.Instance.Player != null
            ? GameContext.Instance.Player
            : FindObjectOfType<PlayerController>();

        if (playerController == null)
        {
            return;
        }

        playerWasEnabled = playerController.enabled;
        playerController.enabled = false;
    }

    private void MoveSpeakerToMeetingPoint()
    {
        if (meetingPoint == null)
        {
            return;
        }

        float y = meetingPoint.position.y;

        // Ставим на пол, а не в воздух.
        RaycastHit hit;
        Vector3 rayStart = meetingPoint.position + Vector3.up * 1.5f;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 4f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            y = hit.point.y;
        }

        // Агент каждый кадр пишет свою внутреннюю позицию в transform,
        // поэтому одного Stop() мало — объект «отпрыгивает» назад.
        // На время катсцены агент выключаем совсем.
        UnityEngine.AI.NavMeshAgent agent = speaker.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            NavMeshCharacterUtility.Stop(agent);
            agent.enabled = false;
        }

        CharacterController controller = speaker.GetComponent<CharacterController>();
        bool hadController = controller != null && controller.enabled;
        if (hadController)
        {
            controller.enabled = false;
        }

        Vector3 position = new Vector3(meetingPoint.position.x, y + 0.05f, meetingPoint.position.z);
        speaker.transform.position = position;
        speaker.transform.rotation = meetingPoint.rotation;

        if (hadController)
        {
            controller.enabled = true;
        }
    }

    private void PlacePlayerInFrontOfSpeaker()
    {
        if (meetingPoint == null)
        {
            return;
        }

        PlayerController player = playerController != null
            ? playerController
            : FindObjectOfType<PlayerController>();
        if (player == null)
        {
            return;
        }

        playerController = player;

        Vector3 forward = meetingPoint.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        Vector3 position = meetingPoint.position + forward.normalized * 1.8f;

        // Герой встаёт на тот же пол, что и лейтенант: отдельный рейкаст
        // мог попасть в люк лестницы этажом ниже.
        if (speaker != null)
        {
            position.y = speaker.transform.position.y + 0.1f;
        }
        else
        {
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out hit, 4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y + 0.1f;
            }
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        bool hadController = controller != null && controller.enabled;
        if (hadController)
        {
            controller.enabled = false;
        }

        player.transform.position = position;

        if (hadController)
        {
            controller.enabled = true;
        }

        Vector3 look = meetingPoint.position - position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
        {
            player.transform.rotation = Quaternion.LookRotation(look.normalized);
        }

        player.RestoreCameraPitch(0f);
    }

    private void FaceEachOther()
    {
        if (speaker == null || playerController == null)
        {
            return;
        }

        Vector3 direction = playerController.transform.position - speaker.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            speaker.transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    // Выключаем ИИ солдата, но не сам объект.
    private void SetSpeakerActive(bool active)
    {
        if (speaker == null)
        {
            return;
        }

        AllySquadAI squad = speaker.GetComponent<AllySquadAI>();
        if (squad != null)
        {
            squad.enabled = active;
        }

        TeamFollowPlayer follow = speaker.GetComponent<TeamFollowPlayer>();
        if (follow != null)
        {
            follow.enabled = active;
        }

        AllyCombatAI combat = speaker.GetComponent<AllyCombatAI>();
        if (combat != null)
        {
            combat.enabled = active;
        }

        AllyWeaponSearch search = speaker.GetComponent<AllyWeaponSearch>();
        if (search != null)
        {
            search.enabled = active;
        }
    }

    private void NavMeshAgentOff(GameObject target)
    {
        UnityEngine.AI.NavMeshAgent agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            NavMeshCharacterUtility.Stop(agent);
        }
    }

    // Чёрный экран и текст создаём в рантайме, чтобы не трогать сцену.
    private void CreateOverlay()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("EndingFadeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 999;

        GameObject imageObject = new GameObject("Black", typeof(RectTransform));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(canvasObject.transform, false);
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        Image image = imageObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        overlayGroup = canvasObject.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 1f;
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;

        GameObject textObject = new GameObject("Caption", typeof(RectTransform));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(canvasObject.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(80f, 80f);
        textRect.offsetMax = new Vector2(-80f, -80f);

        overlayText = textObject.AddComponent<TextMeshProUGUI>();
        overlayText.text = string.Empty;
        overlayText.fontSize = 42f;
        overlayText.alignment = TextAlignmentOptions.Center;
        overlayText.color = new Color(1f, 1f, 1f, 0f);
        overlayText.raycastTarget = false;
        overlayText.enableWordWrapping = true;

        // Шрифт берём у субтитров: у него точно есть кириллица.
        if (SubtitleManager.Instance != null && SubtitleManager.Instance.subtitleText != null &&
            SubtitleManager.Instance.subtitleText.font != null)
        {
            overlayText.font = SubtitleManager.Instance.subtitleText.font;
        }

        // Затемнение считается по времени, поэтому альфа холста не нужна:
        // используем альфу картинки.
        overlayGroup.alpha = 1f;
        image.color = new Color(0f, 0f, 0f, 0f);
        overlayImage = image;
    }

    private Image overlayImage;

    private IEnumerator FadeOverlay(float targetAlpha)
    {
        if (overlayImage == null)
        {
            yield break;
        }

        Color color = overlayImage.color;
        float start = color.a;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration));
            color.a = Mathf.Lerp(start, targetAlpha, t);
            overlayImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        overlayImage.color = color;
    }

    private IEnumerator FadeText(float targetAlpha)
    {
        if (overlayText == null)
        {
            yield break;
        }

        Color color = overlayText.color;
        float start = color.a;
        float elapsed = 0f;
        const float duration = 1.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(start, targetAlpha, t);
            overlayText.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        overlayText.color = color;
    }
}
