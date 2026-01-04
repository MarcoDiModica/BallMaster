using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//hide buttons when join panel is active

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

    void Start()
    {
        if (joinPanel != null)
            joinPanel.SetActive(false);

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        connectButton.onClick.AddListener(OnConnectClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

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
        hostButton.gameObject.SetActive(false);
        joinButton.gameObject.SetActive(false);
        quitButton.gameObject.SetActive(false);

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
        hostButton.gameObject.SetActive(true);
        joinButton.gameObject.SetActive(true);
        quitButton.gameObject.SetActive(true);
        
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
