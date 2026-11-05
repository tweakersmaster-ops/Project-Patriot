using UnityEngine;
using UnityEngine.AI;

// Освобождённый солдат: берёт оружие, идёт за игроком и стреляет по немцам.
// Дополняет существующие AllyCombatAI + TeamFollowPlayer.
[RequireComponent(typeof(CharacterController))]
public class AllySoldierAI : MonoBehaviour
{
    [Header("Настройки")]
    public WeaponType startingWeapon = WeaponType.PPSH;
    public bool disableEnemyAI = true;

    private bool initialized;

    private void OnEnable()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        gameObject.tag = "team";

        if (disableEnemyAI)
        {
            EnemyAI enemyAI = GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                enemyAI.enabled = false;
                // Вражеский ИИ на союзнике только гадит.
                Destroy(enemyAI);
            }
        }

        // Движение: если под ногами запечённый NavMesh — работает агент,
        // иначе CharacterController. CharacterController у ботов по умолчанию
        // выключен, поэтому включаем его только в резервном режиме.
        CharacterController controller = GetComponent<CharacterController>();
        Vector3 spawnPosition = transform.position;
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            // Свежедобавленный агент сразу пытается встать на полигон,
            // ругается и тянет корень вниз (модель уходит под пол).
            // Выключаем и возвращаем персонажа на исходное место.
            agent.enabled = false;
            transform.position = spawnPosition;
        }

        if (NavMeshCharacterUtility.TryActivate(gameObject, agent, 4.5f, 0.4f, 0.6f))
        {
            if (controller != null)
            {
                controller.enabled = false;
            }
        }
        else
        {
            agent.enabled = false;
            if (controller != null)
            {
                controller.enabled = true;
                controller.center = new Vector3(0f, -0.9f, 0f);
                controller.height = 1.8f;
                controller.radius = 0.32f;
                controller.stepOffset = 0.4f;
            }
        }

        WeaponController weapons = GetComponent<WeaponController>();
        if (weapons == null)
        {
            weapons = gameObject.AddComponent<WeaponController>();
        }

        AllyCombatAI combat = GetComponent<AllyCombatAI>();
        if (combat == null)
        {
            combat = gameObject.AddComponent<AllyCombatAI>();
        }

        // Командир подводит отряд к врагу, бойцы должны увидеть его раньше,
        // чем противник окажется вплотную.
        if (combat != null)
        {
            combat.SearchRadius = 30f;
        }

        // AllyCombatAI.Awake ставит оружие по умолчанию — перебиваем после его добавления
        weapons.ConfigureAIWeapon(startingWeapon);

        if (GetComponent<TeamFollowPlayer>() == null)
        {
            gameObject.AddComponent<TeamFollowPlayer>();
        }

        // Без огнестрельного оружия союзник сам ищет выпавшее.
        if (GetComponent<AllyWeaponSearch>() == null)
        {
            gameObject.AddComponent<AllyWeaponSearch>();
        }

        CharacterAnimatorDriver.AttachIfPresent(gameObject);
    }
}
