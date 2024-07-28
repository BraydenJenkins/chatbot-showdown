using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bootstrapper : MonoBehaviour
{
    public string mainSceneName = "SampleScene";

    void Start()
    {
        // Load the main scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
    }
}
