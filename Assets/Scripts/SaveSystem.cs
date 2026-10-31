using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveSystem
{
    private const string SaveDirectoryName = "save";
    private const string SaveFileName = "save.json";

    private static GameSaveData pendingSave;
    private static bool createSaveAfterSceneLoad;
    private static readonly Dictionary<string, EntitySaveData> deadEntities =
        new Dictionary<string, EntitySaveData>();

    static SaveSystem()
    {
        Health.AnyEntityDied += TrackEntityDeath;
    }

    public static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, SaveDirectoryName);
    public static string SaveFilePath => Path.Combine(SaveDirectoryPath, SaveFileName);
    public static bool HasSave => File.Exists(SaveFilePath);

    public static void PrepareNewGame()
    {
        pendingSave = null;
        createSaveAfterSceneLoad = true;
    }

    public static void BeginScene()
    {
        deadEntities.Clear();
    }

    public static bool LoadGame()
    {
        if (!HasSave)
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            GameSaveData save = JsonUtility.FromJson<GameSaveData>(json);
            if (save == null || save.player == null || string.IsNullOrEmpty(save.scenePath))
            {
                Debug.LogError("Save file is invalid or missing required player/scene data: " + SaveFilePath);
                return false;
            }
            if (save.version != 1)
            {
                Debug.LogError("Save file version is not supported: " + save.version);
                return false;
            }
            if (save.entities == null)
            {
                save.entities = new List<EntitySaveData>();
            }

            int sceneIndex = SceneUtility.GetBuildIndexByScenePath(save.scenePath);
            if (sceneIndex < 0)
            {
                Debug.LogError("Saved scene is not enabled in Build Settings: " + save.scenePath);
                return false;
            }

            pendingSave = save;
            createSaveAfterSceneLoad = false;
            if (GameContext.Instance != null)
            {
                GameContext.Instance.PrepareForGameplayScene();
            }

            SceneManager.LoadScene(sceneIndex);
            return true;
        }
        catch (IOException exception)
        {
            Debug.LogError("Could not read save file '" + SaveFilePath + "': " + exception);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogError("Access to save file was denied '" + SaveFilePath + "': " + exception);
            return false;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Save file path or data is invalid '" + SaveFilePath + "': " + exception);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError("Could not load saved scene or data '" + SaveFilePath + "': " + exception);
            return false;
        }
    }

    public static bool SaveCurrentGame()
    {
        GameContext context = GameContext.Instance;
        if (context != null && (context.Player == null || context.PlayerHealth == null))
        {
            SceneBootstrap bootstrap = UnityEngine.Object.FindObjectOfType<SceneBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.TryRegisterPlayer(context);
            }

            if (context.Player == null || context.PlayerHealth == null)
            {
                PlayerController player = UnityEngine.Object.FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    if (player.GetComponent<Health>() == null)
                    {
                        player.gameObject.AddComponent<Health>();
                    }

                    context.RegisterPlayer(player);
                }
            }
        }

        if (context == null || context.Player == null || context.PlayerHealth == null)
        {
            Debug.LogError("Cannot save: no active PlayerController with Health was found and registered in the current scene.");
            return false;
        }

        try
        {
            GameSaveData save = CaptureGame(context);
            Directory.CreateDirectory(SaveDirectoryPath);
            File.WriteAllText(SaveFilePath, JsonUtility.ToJson(save, true));
            Debug.Log("Game saved to: " + SaveFilePath);
            return true;
        }
        catch (IOException exception)
        {
            Debug.LogError("Could not write save file '" + SaveFilePath + "': " + exception);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogError("Access to save directory was denied '" + SaveDirectoryPath + "': " + exception);
            return false;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Save file path or data is invalid '" + SaveFilePath + "': " + exception);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError("Could not capture game state for saving: " + exception);
            return false;
        }
    }

    public static void RestorePendingSave(PlayerController player)
    {
        if (createSaveAfterSceneLoad)
        {
            createSaveAfterSceneLoad = false;
            SaveCurrentGame();
            return;
        }

        if (pendingSave == null)
        {
            return;
        }

        GameSaveData save = pendingSave;
        pendingSave = null;
        if (player == null || save.player == null)
        {
            Debug.LogError("Cannot restore the save because the scene player is missing.");
            return;
        }

        player.ResetForNewGame();
        player.transform.SetPositionAndRotation(save.player.position, save.player.rotation);
        player.RestoreCameraPitch(save.player.cameraPitch);

        WeaponController playerWeapons = player.GetComponent<WeaponController>();
        if (playerWeapons != null)
        {
            playerWeapons.RestoreSaveData(save.player.weapons);
        }

        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth != null)
        {
            playerHealth.RestoreForSave(save.player.health);
        }

        Dictionary<string, Health> entitiesByPath = new Dictionary<string, Health>();
        foreach (Health entityHealth in UnityEngine.Object.FindObjectsOfType<Health>())
        {
            if (entityHealth != playerHealth)
            {
                entitiesByPath[GetHierarchyPath(entityHealth.transform)] = entityHealth;
            }
        }

        foreach (EntitySaveData entitySave in save.entities)
        {
            if (entitySave == null || !entitiesByPath.TryGetValue(entitySave.hierarchyPath, out Health entityHealth))
            {
                Debug.LogWarning("Could not find saved entity in the loaded scene: " +
                                 (entitySave != null ? entitySave.hierarchyPath : "<null>"));
                continue;
            }

            entityHealth.transform.SetPositionAndRotation(entitySave.position, entitySave.rotation);
            WeaponController entityWeapons = entityHealth.GetComponent<WeaponController>();
            if (entityWeapons != null)
            {
                entityWeapons.RestoreSaveData(entitySave.weapons);
            }

            EnemyAI enemyAI = entityHealth.GetComponent<EnemyAI>();
            if (enemyAI != null && entitySave.health > 0f)
            {
                enemyAI.RefreshNavigation();
            }

            entityHealth.RestoreForSave(entitySave.health);
        }
    }

    private static GameSaveData CaptureGame(GameContext context)
    {
        PlayerController player = context.Player;
        GameSaveData save = new GameSaveData
        {
            scenePath = SceneManager.GetActiveScene().path,
            player = new PlayerSaveData
            {
                position = player.transform.position,
                rotation = player.transform.rotation,
                cameraPitch = player.CameraPitch,
                health = context.PlayerHealth.CurrentHealth
            }
        };

        WeaponController playerWeapons = player.GetComponent<WeaponController>();
        if (playerWeapons != null)
        {
            save.player.weapons = playerWeapons.CaptureSaveData();
        }

        foreach (Health entityHealth in UnityEngine.Object.FindObjectsOfType<Health>())
        {
            if (entityHealth == context.PlayerHealth)
            {
                continue;
            }

            EntitySaveData entitySave = new EntitySaveData
            {
                hierarchyPath = GetHierarchyPath(entityHealth.transform),
                position = entityHealth.transform.position,
                rotation = entityHealth.transform.rotation,
                health = entityHealth.CurrentHealth
            };
            WeaponController entityWeapons = entityHealth.GetComponent<WeaponController>();
            if (entityWeapons != null)
            {
                entitySave.weapons = entityWeapons.CaptureSaveData();
            }

            save.entities.Add(entitySave);
        }

        foreach (KeyValuePair<string, EntitySaveData> deadEntity in deadEntities)
        {
            if (!ContainsEntityPath(save.entities, deadEntity.Key))
            {
                save.entities.Add(deadEntity.Value);
            }
        }

        return save;
    }

    private static void TrackEntityDeath(Health health)
    {
        if (health == null || (GameContext.Instance != null && health == GameContext.Instance.PlayerHealth))
        {
            return;
        }

        EntitySaveData deadEntity = new EntitySaveData
        {
            hierarchyPath = GetHierarchyPath(health.transform),
            position = health.transform.position,
            rotation = health.transform.rotation,
            health = 0f
        };
        WeaponController weapons = health.GetComponent<WeaponController>();
        if (weapons != null)
        {
            deadEntity.weapons = weapons.CaptureSaveData();
        }

        deadEntities[deadEntity.hierarchyPath] = deadEntity;
    }

    private static bool ContainsEntityPath(List<EntitySaveData> entities, string hierarchyPath)
    {
        foreach (EntitySaveData entity in entities)
        {
            if (entity.hierarchyPath == hierarchyPath)
            {
                return true;
            }
        }

        return false;
    }

    // Составной путь с индексами соседей отличает одинаково названные объекты сцены.
    private static string GetHierarchyPath(Transform target)
    {
        string path = target.name + "[" + target.GetSiblingIndex() + "]";
        Transform parent = target.parent;
        while (parent != null)
        {
            path = parent.name + "[" + parent.GetSiblingIndex() + "]/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
