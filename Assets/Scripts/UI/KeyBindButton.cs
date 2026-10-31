using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Кнопка переназначения одной клавиши: клик — ждём нажатия, Esc — отмена
public class KeyBindButton : MonoBehaviour, IPointerClickHandler
{
    public BindableAction action;
    public TMP_Text label;
    public Color textColor = new Color(0.95f, 0.93f, 0.86f, 1f);
    public Color listeningColor = new Color(0.95f, 0.85f, 0.45f, 1f);

    private bool listening;

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void OnEnable()
    {
        RefreshLabel();
    }

    private void Start()
    {
        // TMP инициализирует цвет в Awake/OnEnable — подстраховываемся после запуска
        RefreshLabel();
    }

    public void RefreshLabel()
    {
        if (label != null)
        {
            label.color = textColor;
            label.text = KeyBindings.DisplayName(KeyBindings.Get(action));
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!listening && isActiveAndEnabled)
        {
            StartCoroutine(CaptureKey());
        }
    }

    private IEnumerator CaptureKey()
    {
        listening = true;
        if (label != null)
        {
            label.color = listeningColor;
            label.text = "Нажмите...";
        }

        bool done = false;
        while (!done)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                done = true;
            }
            else
            {
                System.Array keys = System.Enum.GetValues(typeof(KeyCode));
                for (int i = 0; i < keys.Length; i++)
                {
                    KeyCode key = (KeyCode)keys.GetValue(i);
                    if (key == KeyCode.Escape || key == KeyCode.None)
                    {
                        continue;
                    }

                    if (Input.GetKeyDown(key))
                    {
                        KeyBindings.Set(action, key);
                        done = true;
                        break;
                    }
                }
            }

            yield return null;
        }

        listening = false;
        RefreshLabel();
    }
}
