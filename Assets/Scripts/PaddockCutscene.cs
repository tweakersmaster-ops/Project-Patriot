using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Катсцена при открытии питомника: один назначенный союзник выходит из
// ворот, говорит фразу через SubtitleManager, после чего остальные солдаты
// освобождаются и идут за игроком.
public class PaddockCutscene : MonoBehaviour
{
    [Header("Ссылки")]
    public PaddockDoor door;
    public GameObject speaker;              // Конкретный солдат, а не случайный
    public GameObject[] soldiers;           // Остальные союзники (изначально выключены)
    public Transform exitPoint;             // Точка снаружи ворот

    [Header("Реплика")]
    [TextArea(2, 4)]
    public string[] phrases = new string[]
    {
        "Спасибо, боец. Мы пойдем освободить мерию, если хочешь — двигай с нами"
    };

    [Header("Отряд после катсцены")]
    public Transform paddockCenter;         // Центр питомника (пусто — сама дверь)
    public Transform mariyaPoint;           // Точка у мерии, куда идёт отряд

    [Header("Настройки")]
    public float walkSpeed = 2.2f;
    public float arriveDistance = 1.2f;
    public float walkTimeout = 15f;
    public float speakDelay = 0.4f;
    public float subtitleTimeout = 30f;
    public float playerDistance = 2.2f;

    private bool started;
    private PlayerController playerController;
    private bool playerWasEnabled = true;

    private void Awake()
    {
        if (door != null)
        {
            door.Opened += HandleDoorOpened;
        }
    }

    private void Start()
    {
        // До открытия двери назначенный солдат стоит и не идёт за игроком.
        // AllyCombatAI/TeamFollowPlayer добавляет AllySoldierAI в OnEnable,
        // поэтому выключаем их здесь, а не в редакторе.
        SetBehaviourEnabled(speaker, false);

        // Если сцена загрузилась с уже открытой дверью — сцену всё равно играем.
        if (door != null && door.IsOpen)
        {
            HandleDoorOpened();
        }
    }

    private void OnDestroy()
    {
        if (door != null)
        {
            door.Opened -= HandleDoorOpened;
        }
    }

    private void HandleDoorOpened()
    {
        if (started)
        {
            return;
        }

        started = true;
        HoldAll(true);
        StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        if (speaker == null)
        {
            ReleaseAll();
            yield break;
        }

        // Союзники не должны стрелять и идти за игроком во время сцены.
        SetBehaviourEnabled(speaker, false);

        // Игрок на время катсцены не ходит.
        FreezePlayer();

        yield return StartCoroutine(WalkSpeakerOutside());

        // Ставим игрока ровно перед говорящим союзником.
        PlacePlayerInFrontOfSpeaker();
        FacePlayer(speaker.transform);

        if (SubtitleManager.Instance != null)
        {
            SubtitleManager.Instance.ShowSubtitles(phrases);
            float deadline = Time.time + subtitleTimeout;
            while (SubtitleManager.Instance.IsShowing && Time.time < deadline)
            {
                yield return null;
            }

            yield return new WaitForSeconds(speakDelay);
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }

        SetBehaviourEnabled(speaker, true);
        ReleaseAll();
        StartSquad();
        UnfreezePlayer();
    }

    private IEnumerator WalkSpeakerOutside()
    {
        if (exitPoint == null)
        {
            yield break;
        }

        NavMeshAgent agent = speaker.GetComponent<NavMeshAgent>();
        bool useAgent = agent != null && agent.enabled && agent.isOnNavMesh;
        if (useAgent)
        {
            NavMeshCharacterUtility.Resume(agent);
            agent.speed = walkSpeed;
            Vector3 destination;
            if (NavMeshCharacterUtility.TryGetNavPoint(exitPoint.position, 2.5f, out destination))
            {
                agent.SetDestination(destination);
            }
            else
            {
                agent.SetDestination(exitPoint.position);
            }
        }

        float deadline = Time.time + walkTimeout;
        while (Time.time < deadline)
        {
            if (useAgent && (!agent.enabled || !agent.isOnNavMesh))
            {
                useAgent = false;
            }

            if (useAgent)
            {
                if (!agent.pathPending &&
                    agent.remainingDistance <= agent.stoppingDistance + 0.3f)
                {
                    break;
                }

                Vector3 velocity = agent.desiredVelocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 0.04f)
                {
                    speaker.transform.rotation = Quaternion.Slerp(
                        speaker.transform.rotation,
                        Quaternion.LookRotation(velocity.normalized),
                        8f * Time.deltaTime);
                }
            }
            else
            {
                Vector3 offset = exitPoint.position - speaker.transform.position;
                if (offset.magnitude <= arriveDistance)
                {
                    break;
                }

                Vector3 step = offset.normalized * walkSpeed * Time.deltaTime;
                speaker.transform.position += step;
                Vector3 flat = offset;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                {
                    speaker.transform.rotation = Quaternion.Slerp(
                        speaker.transform.rotation,
                        Quaternion.LookRotation(flat.normalized),
                        8f * Time.deltaTime);
                }
            }

            yield return null;
        }

        if (useAgent)
        {
            NavMeshCharacterUtility.Stop(agent);
        }
    }

    private PlayerController FindPlayer()
    {
        if (GameContext.Instance != null && GameContext.Instance.Player != null)
        {
            return GameContext.Instance.Player;
        }

        return FindObjectOfType<PlayerController>();
    }

    private void FreezePlayer()
    {
        playerController = FindPlayer();
        if (playerController == null)
        {
            return;
        }

        playerWasEnabled = playerController.enabled;
        playerController.enabled = false;
    }

    private void UnfreezePlayer()
    {
        if (playerController != null)
        {
            playerController.enabled = playerWasEnabled;
            playerController = null;
        }
    }

    // Телепорт игрока на 2 м перед говорящим, лицом к нему.
    private void PlacePlayerInFrontOfSpeaker()
    {
        PlayerController player = playerController != null ? playerController : FindPlayer();
        if (player == null || speaker == null)
        {
            return;
        }

        playerController = player;

        Vector3 away = Vector3.zero;
        if (exitPoint != null && door != null)
        {
            away = exitPoint.position - door.transform.position;
        }

        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = speaker.transform.forward;
            away.y = 0f;
        }

        Vector3 position = speaker.transform.position + away.normalized * playerDistance;

        // Ставим на землю.
        RaycastHit groundHit;
        if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out groundHit, 12f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            position.y = groundHit.point.y + 0.12f;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        bool hadController = controller != null && controller.enabled;
        if (hadController)
        {
            controller.enabled = false;
        }

        player.transform.position = position;

        if (hadController)
        {
            controller.enabled = true;
        }

        Vector3 look = speaker.transform.position - position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
        {
            player.transform.rotation = Quaternion.LookRotation(look.normalized);
        }

        player.RestoreCameraPitch(0f);
    }

    // После катсцены говорящий становится командиром отряда.
    private void StartSquad()
    {
        if (speaker == null)
        {
            return;
        }

        TeamFollowPlayer leaderFollow = speaker.GetComponent<TeamFollowPlayer>();
        if (leaderFollow != null)
        {
            // Командир ходит сам (AllySquadAI), а не за кем-то.
            leaderFollow.enabled = false;
        }

        AllySquadAI squad = speaker.GetComponent<AllySquadAI>();
        if (squad == null)
        {
            squad = speaker.AddComponent<AllySquadAI>();
        }

        squad.paddockCenter = paddockCenter != null
            ? paddockCenter
            : (door != null ? door.transform : null);
        squad.mariyaPoint = mariyaPoint;
        squad.enabled = true;

        if (soldiers == null)
        {
            return;
        }

        for (int i = 0; i < soldiers.Length; i++)
        {
            GameObject soldier = soldiers[i];
            if (soldier == null)
            {
                continue;
            }

            TeamFollowPlayer follow = soldier.GetComponent<TeamFollowPlayer>();
            if (follow == null)
            {
                continue;
            }

            follow.leader = speaker.transform;
            follow.minDistance = 4f;
            follow.maxDistance = 6f;
            follow.followSpeed = 4.2f;
            follow.enabled = true;
        }
    }

    private void FacePlayer(Transform subject)
    {
        if (subject == null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        Vector3 direction = player.transform.position - subject.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            subject.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    // Все союзники на время сцены замирают.
    private void HoldAll(bool hold)
    {
        if (soldiers == null)
        {
            return;
        }

        for (int i = 0; i < soldiers.Length; i++)
        {
            if (soldiers[i] != null)
            {
                SetBehaviourEnabled(soldiers[i], false);
            }
        }
    }

    private void ReleaseAll()
    {
        if (soldiers == null)
        {
            return;
        }

        for (int i = 0; i < soldiers.Length; i++)
        {
            if (soldiers[i] == null)
            {
                continue;
            }

            soldiers[i].SetActive(true);
            SetBehaviourEnabled(soldiers[i], true);
        }
    }

    private static void SetBehaviourEnabled(GameObject soldier, bool enabled)
    {
        if (soldier == null)
        {
            return;
        }

        AllyCombatAI combat = soldier.GetComponent<AllyCombatAI>();
        if (combat != null)
        {
            combat.enabled = enabled;
        }

        TeamFollowPlayer follow = soldier.GetComponent<TeamFollowPlayer>();
        if (follow != null)
        {
            follow.enabled = enabled;
        }

        AllyWeaponSearch search = soldier.GetComponent<AllyWeaponSearch>();
        if (search != null)
        {
            search.enabled = enabled;
        }
    }
}
