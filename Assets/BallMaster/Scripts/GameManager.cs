using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
public class GameManager : MonoBehaviour
{
    
    public void PlayGame()
    {
        SceneManager.LoadSceneAsync("Map_Selection");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
