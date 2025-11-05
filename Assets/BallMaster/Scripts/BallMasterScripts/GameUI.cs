using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.InputSystem;

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

    private PlayerController localPlayer;

    void Start()
    {
        backButton.onClick.AddListener(OnBackClicked);

        if (copyCodeButton != null)
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);

        pauseMenuPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (NetworkManager.Instance == null)
            return;

        if (statusText != null)
        {
            string role = NetworkManager.Instance.isHost ? "HOST" : "CLIENTE";
            int players = NetworkManager.Instance.GetPlayerCount();
            statusText.text = $"Rol: {role}\nJugadores: {players}";
        }

        if (codeText != null)
        {
            if (NetworkManager.Instance.isHost)
            {
                codeText.text = NetworkManager.Instance.lobbyCode;
            }
            else
            {
                codeText.text = "";
            }
        }

        if (copyCodeButton != null)
        {
            copyCodeButton.gameObject.SetActive(NetworkManager.Instance.isHost);
        }
        
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
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
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        // Buscar el jugador local y pausar sus controles
        if (localPlayer == null)
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player.IsLocalPlayer())
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
        if (NetworkManager.Instance.isHost && !string.IsNullOrEmpty(NetworkManager.Instance.lobbyCode))
        {
            GUIUtility.systemCopyBuffer = NetworkManager.Instance.lobbyCode;
            Debug.Log("Código copiado: " + NetworkManager.Instance.lobbyCode);
            
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
        NetworkManager.Instance.Disconnect();
        SceneManager.LoadScene("Main_Menu");
    }
}