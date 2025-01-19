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
    // Name of your "character selection" scene
    // so we know which spawn point to use
    public string selectionSceneName = "CharacterSelectionScene";
    // The name of the spawn point object in the character selection scene
    public string selectionSceneSpawn = "SelectSpawn";
    // The name of the spawn point object in normal gameplay scenes
    public string defaultSpawn = "PlayerSpawn";

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
                modelIndex -= 1; // Convert 1-based NFC value to 0-based index
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
            // If currentPlayer got destroyed or doesn't exist yet, spawn a new one
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
            // Move the new player to the right spawn point
            PositionPlayerAtSpawn(SceneManager.GetActiveScene().name);
        }
    }

    private void PositionPlayerAtSpawn(string sceneName)
    {
        // Decide which spawn name to look for based on the scene
        string spawnName = defaultSpawn;
        if (sceneName == selectionSceneName)
        {
            spawnName = selectionSceneSpawn;
        }

        // Attempt to find an object with that name in the scene
        GameObject spawnPoint = GameObject.Find(spawnName);
        if (spawnPoint)
        {
            currentPlayer.transform.position = spawnPoint.transform.position;
            currentPlayer.transform.rotation = spawnPoint.transform.rotation;
        }
        else
        {
            Debug.LogWarning($"No spawn point named '{spawnName}' found in scene '{sceneName}'.");
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
