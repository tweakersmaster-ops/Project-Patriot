using UnityEngine;

public sealed class SceneBootstrap : MonoBehaviour
{
    [SerializeField] private PlayerController player;

    private void Start()
    {
        GameContext context = GameContext.Instance;
        if (context == null)
        {
            Debug.LogError("GameContext was not initialized before scene bootstrap.", this);
            return;
        }

        bool startGameplay = context.ConsumeGameplayStartRequest();
        if (player == null)
        {
            player = FindObjectOfType<PlayerController>();
        }

        if (player == null)
        {
            context.SetPaused(true);
            if (GameMenuManager.Instance != null)
            {
                GameMenuManager.Instance.ShowMainMenu();
            }
            return;
        }

        EnsurePlayerComponents(player);
        context.RegisterPlayer(player);
        foreach (EnemyAI enemy in FindObjectsOfType<EnemyAI>())
        {
            enemy.target = enemy.GetComponent<TeamFollowPlayer>() == null ? player.transform : null;
        }
        if (startGameplay)
        {
            context.SetPaused(false);
        }
        else
        {
            context.SetPaused(true);
            if (GameMenuManager.Instance != null)
            {
                GameMenuManager.Instance.ShowMainMenu();
            }
        }
    }

    private void EnsurePlayerComponents(PlayerController scenePlayer)
    {
        if (scenePlayer.GetComponent<CharacterController>() == null)
        {
            scenePlayer.gameObject.AddComponent<CharacterController>();
        }

        if (scenePlayer.GetComponent<Health>() == null)
        {
            scenePlayer.gameObject.AddComponent<Health>();
        }

        if (scenePlayer.GetComponent<WeaponController>() == null)
        {
            scenePlayer.gameObject.AddComponent<WeaponController>();
        }

        Camera playerCamera = scenePlayer.GetComponentInChildren<Camera>(true);
        if (playerCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(scenePlayer.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            cameraObject.tag = "MainCamera";
            playerCamera = cameraObject.AddComponent<Camera>();
        }

        if (playerCamera.GetComponent<PlayerHUD>() == null)
        {
            playerCamera.gameObject.AddComponent<PlayerHUD>();
        }

        if (playerCamera.GetComponent<WeaponView>() == null)
        {
            playerCamera.gameObject.AddComponent<WeaponView>();
        }
    }
}
