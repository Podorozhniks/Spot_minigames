using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSelectionManager : MonoBehaviour
{
    public static PlayerSelectionManager Instance;

    [Header("Serial / NFC")]
    public SerialController serialController;

    [Header("Available Player Prefabs")]
    public GameObject[] playerPrefabs;

    [Header("Scene & Spawn Settings")]
    // Name of your "character selection" scene so we know which spawn point to use
    public string selectionSceneName = "CharacterSelectionScene";
    // The name of the spawn point object in the character selection scene
    public string selectionSceneSpawn = "SelectSpawn";

    // The name of the spawn point object in non-listed scenes
    // (this is used as a fallback if the scene isn't in additionalSceneSpawns)
    public string defaultSpawn = "PlayerSpawn";

    // Allows you to specify unique spawn point names for multiple scenes besides the selection scene
    [System.Serializable]
    public class SceneSpawnMapping
    {
        public string sceneName;      // exact name of the scene
        public string spawnPointName; // object in the scene (e.g. "MyLevelSpawn")
    }

    [Header("Additional Scene Spawns")]
    [Tooltip("Add entries here for any scene that should have its own unique spawn point.")]
    public SceneSpawnMapping[] additionalSceneSpawns;

    private GameObject currentPlayer;
    private int currentIndex = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Update()
    {
        if (serialController == null) return;
        string message = serialController.ReadSerialMessage();
        if (!string.IsNullOrEmpty(message))
        {
            if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_CONNECTED)) return;
            if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_DISCONNECTED)) return;

            if (int.TryParse(message, out int modelIndex))
            {
                // Convert 1-based NFC value to 0-based index
                modelIndex -= 1;

                // Check if valid index and different from our current selection
                if (modelIndex >= 0 && modelIndex < playerPrefabs.Length && modelIndex != currentIndex)
                {
                    currentIndex = modelIndex;
                    SpawnOrSwitchPlayer();
                }
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If we already have a selected character index
        if (currentIndex >= 0 && currentIndex < playerPrefabs.Length)
        {
            // If currentPlayer got destroyed or doesn't exist, spawn a new one
            if (currentPlayer == null)
            {
                SpawnOrSwitchPlayer();
            }
            else
            {
                // Otherwise, just position the existing player at the appropriate spawn point
                PositionPlayerAtSpawn(scene.name);
            }
        }
    }

    private void SpawnOrSwitchPlayer()
    {
        // Destroy the old player if it exists
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
            currentPlayer = null;
        }

        // Instantiate the new prefab
        if (currentIndex >= 0 && currentIndex < playerPrefabs.Length)
        {
            currentPlayer = Instantiate(playerPrefabs[currentIndex]);
            DontDestroyOnLoad(currentPlayer);

            // Move the new player to the correct spawn point for the active scene
            PositionPlayerAtSpawn(SceneManager.GetActiveScene().name);
        }
    }

    private void PositionPlayerAtSpawn(string sceneName)
    {
        // 1) Figure out which spawn name to use
        string spawnName = GetSpawnNameForScene(sceneName);

        // 2) Attempt to find an object with that name in the scene
        GameObject spawnPoint = GameObject.Find(spawnName);
        if (spawnPoint)
        {
            currentPlayer.transform.position = spawnPoint.transform.position;
            currentPlayer.transform.rotation = spawnPoint.transform.rotation;
        }
        else
        {
            Debug.LogWarning($"[PlayerSelectionManager] No spawn point named '{spawnName}' found in scene '{sceneName}'.");
        }
    }

    /// <summary>
    /// Returns the appropriate spawn point name for a given scene,
    /// checking the selection scene name, then any additional mappings,
    /// and finally falling back to the defaultSpawn.
    /// </summary>
    private string GetSpawnNameForScene(string sceneName)
    {
        // If it's the selection scene, use selectionSceneSpawn
        if (sceneName == selectionSceneName)
        {
            return selectionSceneSpawn;
        }

        // Otherwise, see if the scene is in our additional mappings
        foreach (var mapping in additionalSceneSpawns)
        {
            if (mapping.sceneName == sceneName)
            {
                return mapping.spawnPointName;
            }
        }

        // If none matched, use the default
        return defaultSpawn;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
