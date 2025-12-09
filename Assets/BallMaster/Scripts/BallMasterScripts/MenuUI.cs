using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI : MonoBehaviour
{
    [SerializeField]
    private NetworkManager networkManager;

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
        QualitySettings.vSyncCount = 1;

        if (joinPanel != null)
            joinPanel.SetActive(false);

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        connectButton.onClick.AddListener(OnConnectClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);

        hostButton.Select();
    }

    void OnHostClicked()
    {
        networkManager.StartHost();
        SceneManager.LoadScene("Map_1");
    }

    void OnJoinClicked()
    {
        joinPanel.SetActive(true);

        ipInput.Select();
    }

    void OnConnectClicked()
    {
        string code = ipInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            Debug.Log("Ingresa un código válido (ej: 5KY87S)");
            return;
        }

        networkManager.JoinHost(code);
        SceneManager.LoadScene("Map_1");
    }

    void OnCancelClicked()
    {
        joinPanel.SetActive(false);

        hostButton.Select();
    }
}
