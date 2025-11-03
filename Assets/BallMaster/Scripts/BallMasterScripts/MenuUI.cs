using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MenuUI : MonoBehaviour
{
    [Header("Botones")]
    public Button hostButton;
    public Button joinButton;

    [Header("Join Panel")]
    public GameObject joinPanel;
    public TMP_InputField ipInput;
    public Button connectButton;
    public Button cancelButton;

    void Start()
    {
        if (joinPanel != null)
            joinPanel.SetActive(false);

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        connectButton.onClick.AddListener(OnConnectClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
    }

    void OnHostClicked()
    {
        NetworkManager.Instance.StartHost();
        SceneManager.LoadScene("Map_1");
    }

    void OnJoinClicked()
    {
        joinPanel.SetActive(true);
    }

    void OnConnectClicked()
    {
        string code = ipInput.text.Trim().ToUpper();
        
        if (string.IsNullOrEmpty(code))
        {
            Debug.Log("Ingresa un código válido (ej: 5KY87S)");
            return;
        }

        NetworkManager.Instance.JoinHost(code);
        SceneManager.LoadScene("Map_1");
    }

    void OnCancelClicked()
    {
        joinPanel.SetActive(false);
    }
}