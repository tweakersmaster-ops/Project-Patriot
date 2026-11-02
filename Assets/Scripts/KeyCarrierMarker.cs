using UnityEngine;

// Жёлтый кружок над врагом, из которого выпадает ключ.
// Кружок всегда повёрнут к камере, покачивается и исчезает после смерти владельца.
public class KeyCarrierMarker : MonoBehaviour
{
    [Header("Настройки метки")]
    public Vector3 localOffset = new Vector3(0f, 2.7f, 0f); // Высота над головой
    public float radius = 0.17f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2.5f;
    public Color color = new Color(1f, 0.86f, 0.1f);

    private Transform marker;
    private Health health;
    private Vector3 baseLocalPosition;
    private static Material markerMaterial;

    private void Start()
    {
        health = GetComponentInParent<Health>();
        if (health != null)
        {
            health.OnDied += HandleOwnerDied;
        }

        CreateMarker();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleOwnerDied;
        }
    }

    private void CreateMarker()
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "KeyMarker";
        Collider discCollider = disc.GetComponent<Collider>();
        if (discCollider != null)
        {
            Destroy(discCollider);
        }

        marker = disc.transform;
        marker.SetParent(transform, false);
        marker.localPosition = localOffset;
        // Цилиндр сплющен по Y — получается плоский кружок.
        marker.localScale = new Vector3(radius * 2f, 0.012f, radius * 2f);
        baseLocalPosition = localOffset;

        Renderer discRenderer = disc.GetComponent<Renderer>();
        if (discRenderer != null)
        {
            discRenderer.sharedMaterial = GetMarkerMaterial();
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            discRenderer.receiveShadows = false;
        }
    }

    private static Material GetMarkerMaterial()
    {
        if (markerMaterial != null)
        {
            return markerMaterial;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        markerMaterial = new Material(shader);
        markerMaterial.color = new Color(1f, 0.86f, 0.1f, 1f);
        markerMaterial.EnableKeyword("_EMISSION");
        markerMaterial.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.05f) * 1.6f);
        return markerMaterial;
    }

    private void Update()
    {
        if (marker == null)
        {
            return;
        }

        // Покачивание.
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        marker.localPosition = baseLocalPosition + Vector3.up * bob;

        // Кружок всегда смотрит в камеру.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = marker.position - cam.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                marker.rotation = Quaternion.FromToRotation(Vector3.up, -toCamera.normalized);
            }
        }
    }

    private void HandleOwnerDied(Health deadHealth)
    {
        if (marker != null)
        {
            Destroy(marker.gameObject);
        }
    }
}
