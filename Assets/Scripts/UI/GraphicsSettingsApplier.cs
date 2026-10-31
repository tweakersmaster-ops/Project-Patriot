using UnityEngine;
using UnityEngine.SceneManagement;

// Применяет графические настройки ко всем сценам: качество, VSync, растительность террейна
public static class GraphicsSettingsApplier
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyQualityAndVSync();
        ApplyTerrainSettings();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyQualityAndVSync();
        ApplyTerrainSettings();
    }

    public static void ApplyQualityAndVSync()
    {
        int quality = Mathf.Clamp(PlayerPrefs.GetInt(SettingsMenu.QualityKey, 3), 0, SettingsMenu.QualityNames.Length - 1);
        QualitySettings.SetQualityLevel(quality, true);
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(SettingsMenu.VSyncKey, 1) == 1 ? 1 : 0;
    }

    public static void ApplyTerrainSettings()
    {
        float vegetation = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsMenu.VegetationKey, 0.7f));
        float grass = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsMenu.GrassKey, 0.8f));

        Terrain[] terrains = Object.FindObjectsOfType<Terrain>();
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null) continue;

            terrain.detailObjectDistance = Mathf.Lerp(30f, 250f, vegetation);
            terrain.treeDistance = Mathf.Lerp(500f, 5000f, vegetation);
            terrain.detailObjectDensity = grass;
        }
    }
}
