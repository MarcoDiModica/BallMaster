using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameUI : MonoBehaviour
{
    [Header("Info")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI codeText;

    [Header("Botones")]
    public Button copyCodeButton;
    public Button backButton;

    void Start()
    {
        backButton.onClick.AddListener(OnBackClicked);
        
        if (copyCodeButton != null)
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
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
                codeText.text = "Código: " + NetworkManager.Instance.lobbyCode;
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