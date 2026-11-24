using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    [Header("Next Scene")]
    public string mainSceneName = "MainScene";

    [Header("Display")]
    public bool activateDisplay2 = true;
    public float waitAfterDisplayActivate = 0.5f;

    void Start()
    {
        StartCoroutine(BootRoutine());
    }

    IEnumerator BootRoutine()
    {
        Time.timeScale = 1f;

        Debug.Log("BootLoader: Boot started.");
        Debug.Log("BootLoader: Display count = " + Display.displays.Length);

        if (activateDisplay2 && Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
            Debug.Log("BootLoader: Display 2 Activate() called.");
        }
        else
        {
            Debug.LogWarning("BootLoader: Display 2 not activated. Available displays = " + Display.displays.Length);
        }

        yield return null;
        yield return null;

        yield return new WaitForSecondsRealtime(waitAfterDisplayActivate);

        Debug.Log("BootLoader: Loading main scene = " + mainSceneName);

        SceneManager.LoadScene(mainSceneName);
    }
}