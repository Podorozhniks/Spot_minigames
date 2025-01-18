using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene loading

public class SceneSwitcher : MonoBehaviour
{
    public string sceneName; // Name of the scene to load

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Ensure the player is tagged "Player"
        {
            SceneManager.LoadScene(sceneName); // Load the designated scene
        }
    }
}