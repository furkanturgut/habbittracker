using UnityEngine;
using UnityEngine.SceneManagement;

// UI butonlarinin Inspector'dan baglanabilmesi icin ince sarmalayici.
public class SceneLoader : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
