using UnityEngine;

// Решает, чем отыгрывать смерть персонажа:
//  - средние и высокие настройки графики -> физика рэгдола (EnemyRagdoll);
//  - низкие настройки -> физика выключается, смерть проигрывает Animator
//    (состояние "die" в samodel.controller), труп удаляется через corpseLifetime.
public static class RagdollSettings
{
    // Уровень качества из SettingsMenu.QualityNames, начиная с которого работает физика:
    // 0 — Очень низкое, 1 — Низкое, 2 — Среднее, 3 — Высокое, 4 — Очень высокое, 5 — Ультра
    public const int MinQualityLevelForRagdoll = 2;

    public static int CurrentQualityLevel
    {
        get
        {
            int levels = SettingsMenu.QualityNames.Length;
            return Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, levels - 1);
        }
    }

    // Физика рэгдола разрешена на текущих настройках графики
    public static bool RagdollEnabled
    {
        get { return CurrentQualityLevel >= MinQualityLevelForRagdoll; }
    }

    // Смерть отыгрывает аниматор (обратное к RagdollEnabled)
    public static bool UsesDeathAnimation
    {
        get { return !RagdollEnabled; }
    }
}
