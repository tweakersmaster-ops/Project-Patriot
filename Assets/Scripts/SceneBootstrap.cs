using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public sealed class SceneBootstrap : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject playerPrefab;

    private void Start()
    {
        GameContext context = GameContext.Instance;
        if (context == null)
        {
            Debug.LogError("GameContext was not initialized before scene bootstrap.", this);
            return;
        }

        bool startGameplay = context.ConsumeGameplayStartRequest();
        SaveSystem.BeginScene();
        if (!TryRegisterPlayer(context))
        {
            context.SetPaused(true);
            if (GameMenuManager.Instance != null)
            {
                GameMenuManager.Instance.ReturnToMainMenu();
            }
            return;
        }

        context.SetPaused(true);
        StartCoroutine(FinishSceneInitialization(context, startGameplay));
    }

    public bool TryRegisterPlayer(GameContext context)
    {
        if (context == null)
        {
            return false;
        }

        GameObject spawnedPlayerObject = null;
        if (player == null)
        {
            player = FindObjectOfType<PlayerController>(true);
        }

        if (player == null)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("No PlayerController was found and no player prefab is assigned.", this);
                return false;
            }

            spawnedPlayerObject = Instantiate(
                playerPrefab,
                GetDefaultSpawnPosition(),
                Quaternion.identity);
            player = spawnedPlayerObject.GetComponent<PlayerController>();
            if (player == null)
            {
                Debug.LogError("The assigned player prefab does not have a PlayerController.", spawnedPlayerObject);
                Destroy(spawnedPlayerObject);
                return false;
            }
        }

        if (!EnsurePlayerComponents(player))
        {
            if (spawnedPlayerObject != null)
            {
                Destroy(spawnedPlayerObject);
                player = null;
            }

            return false;
        }

        context.RegisterPlayer(player);
        foreach (EnemyAI enemy in FindObjectsOfType<EnemyAI>())
        {
            enemy.target = enemy.GetComponent<TeamFollowPlayer>() == null ? player.transform : null;
        }

        return context.Player == player;
    }

    private IEnumerator FinishSceneInitialization(GameContext context, bool startGameplay)
    {
        // Даём контроллерам завершить Start до восстановления инвентаря и состояния сущностей.
        yield return null;
        SaveSystem.RestorePendingSave(player);

        if (startGameplay)
        {
            context.SetPaused(false);
        }
        else
        {
            context.SetPaused(true);
            if (GameMenuManager.Instance != null)
            {
                GameMenuManager.Instance.ReturnToMainMenu();
            }
        }
    }

    private bool EnsurePlayerComponents(PlayerController scenePlayer)
    {
        Camera playerCamera = scenePlayer.GetComponentInChildren<Camera>(true);
        if (playerCamera == null)
        {
            Debug.LogError("The player prefab must contain its own camera.", scenePlayer);
            return false;
        }

        if (scenePlayer.GetComponent<CharacterController>() == null)
        {
            scenePlayer.gameObject.AddComponent<CharacterController>();
        }

        if (scenePlayer.GetComponent<Health>() == null)
        {
            scenePlayer.gameObject.AddComponent<Health>();
        }
        scenePlayer.GetComponent<Health>().destroyOnDeath = false;

        if (scenePlayer.GetComponent<WeaponController>() == null)
        {
            scenePlayer.gameObject.AddComponent<WeaponController>();
        }

        if (scenePlayer.GetComponentInChildren<Renderer>(true) == null)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Player Body";
            capsule.transform.SetParent(scenePlayer.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
            capsule.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Destroy(capsule.GetComponent<Collider>());
        }

        if (playerCamera.GetComponent<PlayerHUD>() == null)
        {
            playerCamera.gameObject.AddComponent<PlayerHUD>();
        }

        if (playerCamera.GetComponent<WeaponView>() == null)
        {
            playerCamera.gameObject.AddComponent<WeaponView>();
        }

        return true;
    }

    private static Vector3 GetDefaultSpawnPosition()
    {
        string scenePath = SceneManager.GetActiveScene().path;
        if (scenePath == "Assets/Scenes/maps/Polock/Polock.unity")
        {
            return new Vector3(491.17f, 2f, 484.85f);
        }

        return new Vector3(0f, 1f, 0f);
    }
}
