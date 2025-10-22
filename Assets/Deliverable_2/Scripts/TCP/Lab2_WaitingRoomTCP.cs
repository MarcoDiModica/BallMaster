using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

public class Lab2_WaitingRoomTCP : MonoBehaviour
{
    public TextMeshProUGUI infoText;
    public TMP_InputField chatInput;
    public TextMeshProUGUI chatDisplay;
    public Button sendButton;
    public Button backButton;
    public TextMeshProUGUI playerListText;

    private Lab2_TCPClient client;
    private Lab2_TCPServer server;
    
    void Start()
    {
        client = Lab2_TCPClient.Instance;
        server = Lab2_TCPServer.Instance;
        
        if (sendButton != null)
            sendButton.onClick.AddListener(SendMessage);
        
        if (backButton != null)
            backButton.onClick.AddListener(GoBack);
        
        UpdateInfo();
        InvokeRepeating("UpdateInfo", 1f, 1f);
    }
    
    void UpdateInfo()
    {
        if (infoText != null)
        {
            if (server != null)
            {
                int clientCount = server.GetConnectedClientsCount();
                infoText.text = "SERVER - Waiting Room\nConnected Players: " + clientCount;
                
                if (playerListText != null)
                {
                    List<string> names = server.GetClientNames();
                    playerListText.text = "Players:\n" + string.Join("\n", names.ToArray());
                }
            }
            else if (client != null)
            {
                infoText.text = "CLIENT - Waiting Room\nYour name: " + client.GetPlayerName();
            }
        }
    }
    
    void SendMessage()
    {
        string message = chatInput.text;
        
        if (!string.IsNullOrEmpty(message))
        {
            if (client != null)
            {
                client.SendChatMessage(message);
                
                if (chatDisplay != null)
                {
                    chatDisplay.text += "\nYou: " + message;
                }
            }
            else if (server != null)
            {
                server.SendMessageToAll(message);
                
                if (chatDisplay != null)
                {
                    chatDisplay.text += "\nServer: " + message;
                }
            }
            
            chatInput.text = "";
        }
    }
    
    void GoBack()
    {
        if (client != null)
        {
            Destroy(client.gameObject);
        }
        
        if (server != null)
        {
            Destroy(server.gameObject);
        }
        
        SceneManager.LoadScene("Lab2_MainMenu");
    }
}