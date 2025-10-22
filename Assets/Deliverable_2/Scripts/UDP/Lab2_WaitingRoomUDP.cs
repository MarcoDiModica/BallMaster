using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

public class Lab2_WaitingRoomUDP : MonoBehaviour
{
    public TextMeshProUGUI infoText;
    public TMP_InputField chatInput;
    public TextMeshProUGUI chatDisplay;
    public Button sendButton;
    public Button backButton;
    public TextMeshProUGUI playerListText;

    private Lab2_UDPClient client;
    private Lab2_UDPServer server;
    
    void Start()
    {
        client = Lab2_UDPClient.Instance;
        server = Lab2_UDPServer.Instance;
        
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
                infoText.text = "SERVER (UDP) - Waiting Room\nConnected Players: " + clientCount;
                
                if (playerListText != null)
                {
                    List<string> names = server.GetClientNames();
                    playerListText.text = "Players:\n" + string.Join("\n", names.ToArray());
                }
            }
            else if (client != null)
            {
                infoText.text = "CLIENT (UDP) - Waiting Room\nYour name: " + client.GetPlayerName();
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