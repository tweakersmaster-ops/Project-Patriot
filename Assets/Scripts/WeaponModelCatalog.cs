using System.Collections.Generic;
using UnityEngine;

// Реестр моделей оружия: сопоставляет тип оружия (WeaponType) с готовым префабом.
// Ассет лежит в Resources/WeaponModelCatalog, поэтому ссылки на сцену не нужны.
[CreateAssetMenu(menuName = "Slutsk1941/Каталог моделей оружия", fileName = "WeaponModelCatalog")]
public class WeaponModelCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public WeaponType type;
        public GameObject prefab;

        [Header("Только для игрока (вид от первого лица)")]
        [Tooltip("Дополнительный сдвиг модели в руках игрока.")]
        public Vector3 playerPositionOffset;
        [Tooltip("Дополнительный поворот модели в руках игрока.")]
        public Vector3 playerEulerOffset;
        [Tooltip("Дополнительный масштаб модели в руках игрока.")]
        public float playerScale = 1f;
    }

    public Entry[] entries;

    private static WeaponModelCatalog instance;
    private static readonly Dictionary<WeaponType, Entry> lookup = new Dictionary<WeaponType, Entry>();

    public static WeaponModelCatalog Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<WeaponModelCatalog>("WeaponModelCatalog");
                lookup.Clear();
                if (instance != null && instance.entries != null)
                {
                    for (int i = 0; i < instance.entries.Length; i++)
                    {
                        Entry entry = instance.entries[i];
                        if (entry != null && entry.prefab != null)
                        {
                            lookup[entry.type] = entry;
                        }
                    }
                }
            }

            return instance;
        }
    }

    // Возвращает запись для типа или null, если модели нет (нужен процедурный fallback).
    public static Entry Find(WeaponType type)
    {
        if (Instance == null)
        {
            return null;
        }

        Entry entry;
        return lookup.TryGetValue(type, out entry) ? entry : null;
    }
}
