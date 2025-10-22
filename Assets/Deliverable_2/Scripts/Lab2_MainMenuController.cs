using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Lab2_MainMenuController : MonoBehaviour
{
    public Button btnServerTCP;
    public Button btnClientTCP;
    public Button btnServerUDP;
    public Button btnClientUDP;
    
    void Start()
    {
        btnServerTCP.onClick.AddListener(() => SceneManager.LoadScene("Lab2_ServerTCP"));
        btnClientTCP.onClick.AddListener(() => SceneManager.LoadScene("Lab2_ClientTCP"));
        btnServerUDP.onClick.AddListener(() => SceneManager.LoadScene("Lab2_ServerUDP"));
        btnClientUDP.onClick.AddListener(() => SceneManager.LoadScene("Lab2_ClientUDP"));
    }
}