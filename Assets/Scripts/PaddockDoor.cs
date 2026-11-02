using UnityEngine;
using UnityEngine.AI;

// Дверь питомника: открывается по E, если у игрока есть ключ.
// При открытии освобождает пленных (активирует их объекты).
public class PaddockDoor : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform pivot;                       // Петля двери (вращается)
    public Transform secondPivot;                 // Вторая створка (вращается в другую сторону)
    public GameObject[] prisonersToActivate;      // Пленные, которых нужно включить

    [Header("Настройки")]
    public KeyCode interactKey = KeyCode.E;
    public float interactDistance = 3f;
    public float openAngle = 100f;
    public float openSpeed = 120f;
    public bool requiresKey = true;
    public bool autoOpenWithKey = true;           // Открывается сама, если у игрока есть ключ и он подошёл

    private bool isOpen;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion closedRotation2;
    private Quaternion openRotation2;

    public bool IsOpen => isOpen;

    private void Start()
    {
        if (pivot == null)
        {
            pivot = transform;
        }

        closedRotation = pivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        if (secondPivot != null)
        {
            closedRotation2 = secondPivot.localRotation;
            openRotation2 = closedRotation2 * Quaternion.Euler(0f, -openAngle, 0f);
        }
    }

    private void Update()
    {
        if (isOpen)
        {
            pivot.localRotation = Quaternion.RotateTowards(
                pivot.localRotation,
                openRotation,
                openSpeed * Time.deltaTime);

            if (secondPivot != null)
            {
                secondPivot.localRotation = Quaternion.RotateTowards(
                    secondPivot.localRotation,
                    openRotation2,
                    openSpeed * Time.deltaTime);
            }

            return;
        }

        if (GameContext.Instance != null && GameContext.Instance.IsPaused)
        {
            return;
        }

        PlayerController player = GameContext.Instance != null ? GameContext.Instance.Player : null;
        if (player == null)
        {
            // Фолбэк: контекст игры мог ещё не зарегистрировать игрока.
            player = FindObjectOfType<PlayerController>();
        }

        if (player == null)
        {
            return;
        }

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude > interactDistance)
        {
            return;
        }

        if (requiresKey && !PlayerInventory.hasPaddockKey)
        {
            return;
        }

        // С ключом дверь открывается сама, когда игрок подходит,
        // E остаётся как альтернативный вариант.
        if (autoOpenWithKey || Input.GetKeyDown(interactKey))
        {
            Open();
        }
    }

    private static void DisableNavObstacles(Transform root)
    {
        if (root == null)
        {
            return;
        }

        NavMeshObstacle[] obstacles = root.GetComponentsInChildren<NavMeshObstacle>(true);
        for (int i = 0; i < obstacles.Length; i++)
        {
            obstacles[i].enabled = false;
        }
    }

    private void Open()
    {
        isOpen = true;

        // Закрытые створки перекрывают NavMesh (NavMeshObstacle с carving).
        // Как только дверь открывается, убираем препятствия, чтобы союзники
        // и пленные могли пройти через проём.
        DisableNavObstacles(pivot);
        DisableNavObstacles(secondPivot);

        if (prisonersToActivate != null)
        {
            for (int i = 0; i < prisonersToActivate.Length; i++)
            {
                if (prisonersToActivate[i] != null)
                {
                    prisonersToActivate[i].SetActive(true);
                }
            }
        }
    }
}
