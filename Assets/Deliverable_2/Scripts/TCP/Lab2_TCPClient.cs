using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class Lab2_TCPClient : MonoBehaviour
{
    public TMP_InputField ipInput;
    public TMP_InputField nameInput;
    public Button connectButton;
    public TextMeshProUGUI statusText;
    
    private Socket clientSocket;
    private Thread receiveThread;
    private bool isConnected = false;
    private int port = 9050;
    private string playerName = "";
    
    private Queue<string> messageQueue = new Queue<string>();
    private object queueLock = new object();
    
    public static Lab2_TCPClient Instance;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(ConnectToServer);
        
        if (ipInput != null)
            ipInput.text = "127.0.0.1";
        
        if (nameInput != null)
            nameInput.text = "Player1";
    }
    
    void ConnectToServer()
    {
        string ip = ipInput.text;
        playerName = nameInput.text;
        
        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(playerName))
        {
            UpdateStatus("Please enter IP and name");
            return;
        }
        
        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), port);
            
            clientSocket.Connect(ipep);
            isConnected = true;
            
            UpdateStatus("Connected to server");
            
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
            
            byte[] nameData = Encoding.ASCII.GetBytes(playerName);
            clientSocket.Send(nameData);
            
            Invoke("GoToWaitingRoom", 0.5f);
        }
        catch (Exception e)
        {
            UpdateStatus("Connection failed: " + e.Message);
        }
    }
    
    void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        
        while (isConnected)
        {
            try
            {
                int recv = clientSocket.Receive(buffer);
                
                if (recv == 0)
                {
                    break;
                }
                
                string message = Encoding.ASCII.GetString(buffer, 0, recv);
                LogMessage("Received: " + message);
            }
            catch (Exception e)
            {
                if (isConnected)
                {
                    LogMessage("Error receiving: " + e.Message);
                }
                break;
            }
        }
    }
    
    public void SendChatMessage(string message)
    {
        if (isConnected && clientSocket != null && clientSocket.Connected)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(message);
                clientSocket.Send(data);
            }
            catch (Exception e)
            {
                LogMessage("Error sending message: " + e.Message);
            }
        }
    }
    
    public string GetPlayerName()
    {
        return playerName;
    }
    
    void UpdateStatus(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
        }
        LogMessage(msg);
    }
    
    void LogMessage(string msg)
    {
        lock (queueLock)
        {
            messageQueue.Enqueue(msg);
        }
    }
    
    void Update()
    {
        lock (queueLock)
        {
            while (messageQueue.Count > 0)
            {
                Debug.Log("[Lab2_TCP_Client] " + messageQueue.Dequeue());
            }
        }
    }
    
    void GoToWaitingRoom()
    {
        SceneManager.LoadScene("Lab2_WaitingRoomTCP");
    }
    
    void OnDestroy()
    {
        isConnected = false;
        
        if (clientSocket != null)
        {
            clientSocket.Close();
        }
    }
}