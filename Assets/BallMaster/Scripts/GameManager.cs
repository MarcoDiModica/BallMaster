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

    public void LoadMap_1()
    {
        SceneManager.LoadSceneAsync("Map_1");
    }
    public void LoadMap_2()
    {
        SceneManager.LoadSceneAsync("Map_2");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
