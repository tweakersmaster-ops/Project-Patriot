using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Настройки: три вкладки — Графика, Управление, Звук.
// Сохраняются в PlayerPrefs "settings.*" (переназначение клавиш — отдельно, в KeyBindings)
public class SettingsMenu : MonoBehaviour
{
    public const string VolumeKey = "settings.volume";
    public const string FullscreenKey = "settings.fullscreen";
    public const string VSyncKey = "settings.vsync";
    public const string QualityKey = "settings.quality";
    public const string VegetationKey = "settings.vegetation";
    public const string GrassKey = "settings.grass";

    public static readonly string[] QualityNames =
    {
        "Очень низкое", "Низкое", "Среднее", "Высокое", "Очень высокое", "Ультра"
    };

    [Header("Вкладки")]
    public Button graphicsTab;
    public Button controlsTab;
    public Button soundTab;
    public GameObject graphicsContent;
    public GameObject controlsContent;
    public GameObject soundContent;
    public Color tabActiveColor = new Color(0.28f, 0.32f, 0.36f, 0.95f);
    public Color tabInactiveColor = new Color(0.10f, 0.13f, 0.16f, 0.92f);

    [Header("Графика")]
    public Button qualityButton;
    public TMP_Text qualityButtonLabel;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;
    public Slider vegetationSlider;
    public TMP_Text vegetationValue;
    public Slider grassSlider;
    public TMP_Text grassValue;

    [Header("Управление")]
    public KeyBindButton[] keyButtons;
    public Button resetBindingsButton;

    [Header("Звук")]
    public Slider volumeSlider;

    private bool wired;
    private int currentTab;

    private void OnEnable()
    {
        if (!wired)
        {
            Wire();
        }

        RefreshUI();
        SelectTab(currentTab);
    }

    private void Wire()
    {
        wired = true;

        if (graphicsTab != null) graphicsTab.onClick.AddListener(SelectGraphicsTab);
        if (controlsTab != null) controlsTab.onClick.AddListener(SelectControlsTab);
        if (soundTab != null) soundTab.onClick.AddListener(SelectSoundTab);
        if (qualityButton != null) qualityButton.onClick.AddListener(CycleQuality);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (vsyncToggle != null) vsyncToggle.onValueChanged.AddListener(SetVSync);
        if (vegetationSlider != null) vegetationSlider.onValueChanged.AddListener(SetVegetation);
        if (grassSlider != null) grassSlider.onValueChanged.AddListener(SetGrass);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(SetVolume);
        if (resetBindingsButton != null) resetBindingsButton.onClick.AddListener(ResetBindings);
    }

    // Применить сохранённые настройки до открытия панели
    public static void ApplySavedSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        ApplyQuality(PlayerPrefs.GetInt(QualityKey, 3));
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey, 1) == 1 ? 1 : 0;
        GraphicsSettingsApplier.ApplyTerrainSettings();

        // Полноэкранный режим в редакторе не трогаем
        if (!Application.isEditor)
        {
            Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        }
    }

    private static void ApplyQuality(int index)
    {
        index = Mathf.Clamp(index, 0, QualityNames.Length - 1);
        QualitySettings.SetQualityLevel(index, true);
    }

    // --- вкладки ---
    public void SelectGraphicsTab() { SelectTab(0); }
    public void SelectControlsTab() { SelectTab(1); }
    public void SelectSoundTab() { SelectTab(2); }

    private void SelectTab(int index)
    {
        currentTab = Mathf.Clamp(index, 0, 2);
        if (graphicsContent != null) graphicsContent.SetActive(currentTab == 0);
        if (controlsContent != null) controlsContent.SetActive(currentTab == 1);
        if (soundContent != null) soundContent.SetActive(currentTab == 2);

        SetTabColor(graphicsTab, currentTab == 0);
        SetTabColor(controlsTab, currentTab == 1);
        SetTabColor(soundTab, currentTab == 2);

        // Кнопка сброса клавиш живёт на общей панели — показываем только на вкладке управления
        if (resetBindingsButton != null)
        {
            resetBindingsButton.gameObject.SetActive(currentTab == 1);
        }
    }

    private void SetTabColor(Button tab, bool active)
    {
        if (tab == null) return;
        Image image = tab.GetComponent<Image>();
        if (image == null) return;

        Color color = active ? tabActiveColor : tabInactiveColor;
        MenuButtonHover hover = tab.GetComponent<MenuButtonHover>();
        if (hover != null)
        {
            hover.SetBaseColor(color);
        }
        else
        {
            image.color = color;
        }
    }

    // --- графика ---
    private void CycleQuality()
    {
        int index = (PlayerPrefs.GetInt(QualityKey, 3) + 1) % QualityNames.Length;
        PlayerPrefs.SetInt(QualityKey, index);
        PlayerPrefs.Save();
        ApplyQuality(index);
        RefreshQualityLabel(index);
    }

    private void RefreshQualityLabel(int index)
    {
        if (qualityButtonLabel != null)
        {
            qualityButtonLabel.text = QualityNames[Mathf.Clamp(index, 0, QualityNames.Length - 1)];
        }
    }

    public void SetFullscreen(bool value)
    {
        if (!Application.isEditor)
        {
            Screen.fullScreen = value;
        }

        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetVSync(bool value)
    {
        QualitySettings.vSyncCount = value ? 1 : 0;
        PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetVegetation(float value)
    {
        PlayerPrefs.SetFloat(VegetationKey, value);
        PlayerPrefs.Save();
        GraphicsSettingsApplier.ApplyTerrainSettings();
        RefreshValueLabel(vegetationValue, value);
    }

    public void SetGrass(float value)
    {
        PlayerPrefs.SetFloat(GrassKey, value);
        PlayerPrefs.Save();
        GraphicsSettingsApplier.ApplyTerrainSettings();
        RefreshValueLabel(grassValue, value);
    }

    // --- звук (пока заглушка) ---
    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
    }

    // --- управление ---
    public void ResetBindings()
    {
        KeyBindings.ResetAll();
        RefreshKeyButtons();
    }

    private void RefreshKeyButtons()
    {
        if (keyButtons == null) return;
        for (int i = 0; i < keyButtons.Length; i++)
        {
            if (keyButtons[i] != null)
            {
                keyButtons[i].RefreshLabel();
            }
        }
    }

    private void RefreshUI()
    {
        int quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 3), 0, QualityNames.Length - 1);
        RefreshQualityLabel(quality);

        if (fullscreenToggle != null)
        {
            bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(VSyncKey, 1) == 1);
        }

        if (volumeSlider != null)
        {
            float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            volumeSlider.SetValueWithoutNotify(volume);
        }

        if (vegetationSlider != null)
        {
            float vegetation = PlayerPrefs.GetFloat(VegetationKey, 0.7f);
            vegetationSlider.SetValueWithoutNotify(vegetation);
            RefreshValueLabel(vegetationValue, vegetation);
        }

        if (grassSlider != null)
        {
            float grass = PlayerPrefs.GetFloat(GrassKey, 0.8f);
            grassSlider.SetValueWithoutNotify(grass);
            RefreshValueLabel(grassValue, grass);
        }

        RefreshKeyButtons();
    }

    private void RefreshValueLabel(TMP_Text label, float value)
    {
        if (label != null)
        {
            label.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
