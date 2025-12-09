using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    [Header("Info")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI codeText;

    [Header("Botones")]
    public Button copyCodeButton;
    public Button backButton;

    [Header("Paneles")]
    public GameObject pauseMenuPanel;

    [SerializeField]
    private NetworkManager networkManager;

    private PlayerController localPlayer;

    void Start()
    {
        networkManager = FindObjectOfType<NetworkManager>();

        backButton.onClick.AddListener(OnBackClicked);

        if (copyCodeButton != null)
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);

        pauseMenuPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (networkManager == null)
            return;

        if (statusText != null)
        {
            string role = networkManager.isHost ? "HOST" : "CLIENTE";
            int players = networkManager.GetPlayerCount();
            statusText.text = $"Rol: {role}\nJugadores: {players}";
        }

        if (codeText != null)
        {
            if (networkManager.isHost)
            {
                codeText.text = networkManager.lobbyCode;
            }
            else
            {
                codeText.text = "";
            }
        }

        if (copyCodeButton != null)
        {
            copyCodeButton.gameObject.SetActive(networkManager.isHost);
        }

        if (
            Keyboard.current.pKey.wasPressedThisFrame
            || Keyboard.current.escapeKey.wasPressedThisFrame
            || Gamepad.current.startButton.wasPressedThisFrame
        )
        {
            TogglePause();
        }
    }

    void TogglePause()
    {
        bool isPaused = !pauseMenuPanel.activeSelf;
        pauseMenuPanel.SetActive(isPaused);

        if (isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            copyCodeButton.Select();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (localPlayer == null)
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(
                FindObjectsSortMode.None
            );
            foreach (var player in players)
            {
                PlayerNetworkComponent netComp = player.GetComponent<PlayerNetworkComponent>();
                if (netComp != null && netComp.IsLocalPlayer)
                {
                    localPlayer = player;
                    break;
                }
            }
        }

        if (localPlayer != null)
        {
            localPlayer.SetPaused(isPaused);
        }
    }

    void OnCopyCodeClicked()
    {
        if (
            networkManager != null
            && networkManager.isHost
            && !string.IsNullOrEmpty(networkManager.lobbyCode)
        )
        {
            GUIUtility.systemCopyBuffer = networkManager.lobbyCode;

            if (copyCodeButton != null)
            {
                Text buttonText = copyCodeButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    string originalText = buttonText.text;
                    buttonText.text = "¡Copiado!";
                    StartCoroutine(ResetButtonText(buttonText, originalText));
                }
            }
        }
    }

    System.Collections.IEnumerator ResetButtonText(Text buttonText, string originalText)
    {
        yield return new UnityEngine.WaitForSeconds(2f);
        buttonText.text = originalText;
    }

    void OnBackClicked()
    {
        if (networkManager != null)
        {
            networkManager.Disconnect();
        }
        SceneManager.LoadScene("Main_Menu");
    }
}
