using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Анимация кнопки меню: плавно белеет и увеличивается при наведении,
// резко уменьшается и сереет при нажатии
[RequireComponent(typeof(Image))]
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Color hoverColor = Color.white;
    public Color pressedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    public Color normalTextColor = new Color(0.95f, 0.93f, 0.86f, 1f);
    public Color hoverTextColor = new Color(0.10f, 0.13f, 0.16f, 1f);
    public float hoverScale = 1.5f;
    public float pressedScale = 0.9f;
    public float hoverDuration = 0.16f;
    public float pressDuration = 0.04f;

    private Image image;
    private TMP_Text label;
    private Color normalColor;
    private Vector3 normalScale;
    private bool hovered;
    private bool pressed;

    private void Awake()
    {
        image = GetComponent<Image>();
        label = GetComponentInChildren<TMP_Text>(true);
        normalColor = image.color;
        normalScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
            normalColor = image.color;
        }

        if (normalScale == Vector3.zero)
        {
            normalScale = Vector3.one;
        }

        hovered = false;
        pressed = false;
        image.color = normalColor;
        transform.localScale = normalScale;

        if (label != null)
        {
            label.color = normalTextColor;
        }
    }

    private void Update()
    {
        if (image == null)
        {
            return;
        }

        bool isPressed = pressed;
        bool isHovered = hovered || isPressed;

        Color targetColor = isPressed ? pressedColor : (isHovered ? hoverColor : normalColor);
        Vector3 targetScale = isPressed
            ? normalScale * pressedScale
            : (isHovered ? normalScale * hoverScale : normalScale);

        // unscaledDeltaTime — меню работает и при Time.timeScale = 0
        float colorFactor = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(isPressed ? pressDuration : hoverDuration, 0.0001f));
        image.color = Color.Lerp(image.color, targetColor, colorFactor);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, colorFactor);

        // На белом фоне светлый текст пропадает — перекрашиваем подпись в тёмный
        if (label != null)
        {
            Color targetText = isHovered ? hoverTextColor : normalTextColor;
            label.color = Color.Lerp(label.color, targetText, colorFactor);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }

    // Базовая (неподсвеченная) окраска — например, для активной вкладки настроек
    public void SetBaseColor(Color color)
    {
        normalColor = color;
        if (!hovered && !pressed && image != null)
        {
            image.color = color;
        }
    }
}
