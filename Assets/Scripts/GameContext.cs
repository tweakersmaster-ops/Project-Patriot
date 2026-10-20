using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameContext : MonoBehaviour
{
    public static GameContext Instance { get; private set; }

    public bool IsPaused { get; private set; } = true;
    public PlayerController Player { get; private set; }
    public Health PlayerHealth { get; private set; }

    public event Action<Health> PlayerDied;
    private bool gameplayStartRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreatePersistentContext()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject contextObject = new GameObject(nameof(GameContext));
        contextObject.AddComponent<GameContext>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (GetComponent<GameMenuManager>() == null)
        {
            gameObject.AddComponent<GameMenuManager>();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnregisterPlayer();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    public void PrepareForGameplayScene()
    {
        gameplayStartRequested = true;
    }

    public bool ConsumeGameplayStartRequest()
    {
        bool startGameplay = gameplayStartRequested;
        gameplayStartRequested = false;
        return startGameplay;
    }

    public void RegisterPlayer(PlayerController player)
    {
        if (player == null)
        {
            Debug.LogError("Cannot register a null player with GameContext.", this);
            return;
        }

        Health health = player.GetComponent<Health>();
        if (health == null)
        {
            Debug.LogError("The scene player requires a Health component.", player);
            return;
        }

        UnregisterPlayer();
        Player = player;
        PlayerHealth = health;
        PlayerHealth.OnDied += HandlePlayerDied;
    }

    private void UnregisterPlayer()
    {
        if (PlayerHealth != null)
        {
            PlayerHealth.OnDied -= HandlePlayerDied;
        }

        Player = null;
        PlayerHealth = null;
    }

    private void HandlePlayerDied(Health health)
    {
        SetPaused(true);
        PlayerDied?.Invoke(health);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnregisterPlayer();
    }
}
