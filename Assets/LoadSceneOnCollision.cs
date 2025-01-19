using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnCollision : MonoBehaviour
{
    [SerializeField]
    private string sceneName = "SceneToLoad";

    private void OnCollisionEnter(Collision collision)
    {
        SceneManager.LoadScene(sceneName);
    }
}

