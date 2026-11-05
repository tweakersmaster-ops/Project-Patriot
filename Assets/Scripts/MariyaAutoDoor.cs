using UnityEngine;
using UnityEngine.AI;

// Автоматическая дверь здания мерии: открывается, когда игрок подходит.
// Полотно вращается вокруг петли (края), а NavMeshObstacle с carving
// перекрывает проход только пока дверь закрыта.
[RequireComponent(typeof(MeshCollider))]
public class MariyaAutoDoor : MonoBehaviour
{
    [Header("Открывание")]
    public float openDistance = 6f;
    public float openAngle = 90f;
    public float rotateSpeed = 160f;

    [Header("Петля")]
    // true — петля на левом (западном) краю, полотно уходит вправо.
    public bool hingeOnLeft = true;

    private Transform pivot;
    private Transform player;
    private NavMeshObstacle obstacle;
    private Vector3 closedCenter;
    private float signedAngle;
    private float currentAngle;
    private float nextNpcCheckTime;
    private bool npcNearby;

    private void Awake()
    {
        MeshCollider doorCollider = GetComponent<MeshCollider>();
        Bounds bounds = doorCollider != null ? doorCollider.bounds : new Bounds(transform.position, Vector3.one);
        closedCenter = bounds.center;

        Vector3 hingePosition = new Vector3(
            hingeOnLeft ? bounds.min.x : bounds.max.x,
            bounds.center.y,
            bounds.center.z);

        GameObject pivotObject = new GameObject(name + "_Hinge");
        pivot = pivotObject.transform;
        pivot.SetParent(transform.parent, false);
        pivot.position = hingePosition;
        pivot.rotation = Quaternion.identity;

        // Компенсируем масштаб родителя (у mariya он 0.512), чтобы петля
        // вращала дверь как в мировых координатах.
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        pivot.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / parentScale.z);

        transform.SetParent(pivot, true);

        signedAngle = hingeOnLeft ? openAngle : -openAngle;

        // Препятствие для навигации: пока дверь закрыта, проём перекрыт.
        obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            obstacle.center = meshFilter.sharedMesh.bounds.center;
            obstacle.size = meshFilter.sharedMesh.bounds.size;
        }
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
    }

    private void Update()
    {
        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            return;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
            else
            {
                return;
            }
        }

        Vector3 offset = player.position - closedCenter;
        offset.y = 0f;

        // Двери мерии открывают и боты: союзники и охрана.
        if (Time.time >= nextNpcCheckTime)
        {
            nextNpcCheckTime = Time.time + 0.2f;
            npcNearby = HasNpcBeside();
        }

        bool shouldOpen = offset.magnitude <= openDistance || npcNearby;

        float targetAngle = shouldOpen ? signedAngle : 0f;
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
        pivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);

        // Как только полотно пошло, проём для NavMesh свободен.
        if (obstacle != null)
        {
            obstacle.enabled = Mathf.Abs(currentAngle) < 1f;
        }
    }

    // Живой бот (team/enemy) в зоне двери.
    private bool HasNpcBeside()
    {
        Collider[] hits = Physics.OverlapSphere(
            closedCenter,
            openDistance + 0.5f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            Health health = hits[i].GetComponentInParent<Health>();
            if (health == null || health.IsDead)
            {
                continue;
            }

            if (health.CompareTag("team") || health.CompareTag("enemy"))
            {
                return true;
            }
        }

        return false;
    }
}
