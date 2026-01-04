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
    public Button quitButton;
    public TextMeshProUGUI errorText;

    void Start()
    {
        if (joinPanel != null)
            joinPanel.SetActive(false);

        if (errorText != null)
            errorText.text = "";

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        connectButton.onClick.AddListener(OnConnectClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        if (networkManager != null)
        {
            networkManager.OnConnectionSuccess += OnConnectionSuccess;
            networkManager.OnConnectionFailed += OnConnectionFailed;
        }

        hostButton.Select();
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnConnectionSuccess -= OnConnectionSuccess;
            networkManager.OnConnectionFailed -= OnConnectionFailed;
        }
    }

    void OnHostClicked()
    {
        networkManager.StartHost();
        SceneManager.LoadScene("Map_1");
    }

    void OnJoinClicked()
    {
        joinPanel.SetActive(true);
        hostButton.gameObject.SetActive(false);
        joinButton.gameObject.SetActive(false);
        quitButton.gameObject.SetActive(false);

        if (errorText != null)
            errorText.text = "";

        ipInput.Select();
    }

    void OnConnectClicked()
    {
        string code = ipInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            if (errorText != null)
                errorText.text = "Ingresa un código válido";
            return;
        }

        if (errorText != null)
            errorText.text = "Conectando...";

        connectButton.interactable = false;
        networkManager.JoinHost(code);
    }

    void OnConnectionSuccess()
    {
        SceneManager.LoadScene("Map_1");
    }

    void OnConnectionFailed(string error)
    {
        if (errorText != null)
            errorText.text = error;

        connectButton.interactable = true;
    }

    void OnCancelClicked()
    {
        joinPanel.SetActive(false);
        hostButton.gameObject.SetActive(true);
        joinButton.gameObject.SetActive(true);
        quitButton.gameObject.SetActive(true);

        connectButton.interactable = true;

        if (errorText != null)
            errorText.text = "";
        
        hostButton.Select();
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

