using UnityEngine;

// Выкидывает предметы после смерти владельца (Enemy_WithKey: ключ + ППШ).
public class LootDropper : MonoBehaviour
{
    public GameObject[] lootPrefabs;
    public Vector3 spawnOffset = new Vector3(0f, 0.15f, 0f);

    private Health health;
    private bool dropped;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.OnDied += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied(Health deadHealth)
    {
        if (dropped)
        {
            return;
        }

        dropped = true;
        if (lootPrefabs == null)
        {
            return;
        }

        Vector3 origin = transform.position + spawnOffset;
        for (int i = 0; i < lootPrefabs.Length; i++)
        {
            GameObject prefab = lootPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            Vector3 offset = new Vector3((i % 3) * 0.45f - 0.45f, 0f, (i / 3) * 0.45f);
            Instantiate(prefab, origin + offset, Quaternion.identity);
        }
    }
}
