using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Lab2_UDPServer : MonoBehaviour
{
    private Socket serverSocket;
    private Thread receiveThread;
    private bool isRunning = false;
    private int port = 9051;
    private string serverName = "Lab2_UDP_GameServer";
    
    private List<EndPoint> connectedClients = new List<EndPoint>();
    private Dictionary<string, string> clientNames = new Dictionary<string, string>();
    private Queue<string> messageQueue = new Queue<string>();
    private object queueLock = new object();
    
    public static Lab2_UDPServer Instance;
    
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
        StartServer();
        Invoke("GoToWaitingRoom", 2f);
    }
    
    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
            serverSocket.Bind(ipep);
            
            isRunning = true;
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
            
            LogMessage("UDP Server started on port " + port);
        }
        catch (Exception e)
        {
            LogMessage("Error starting server: " + e.Message);
        }
    }
    
    void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        
        while (isRunning)
        {
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint remote = (EndPoint)sender;
                
                int recv = serverSocket.ReceiveFrom(buffer, ref remote);
                string message = Encoding.ASCII.GetString(buffer, 0, recv);
                
                string clientKey = remote.ToString();
                
                if (!clientNames.ContainsKey(clientKey))
                {
                    clientNames[clientKey] = message;
                    lock (connectedClients)
                    {
                        connectedClients.Add(remote);
                    }
                    LogMessage("New client: " + message + " from " + clientKey);
                    
                    byte[] welcomeData = Encoding.ASCII.GetBytes(serverName);
                    serverSocket.SendTo(welcomeData, welcomeData.Length, SocketFlags.None, remote);
                    
                    BroadcastMessage("SERVER: " + message + " joined", remote);
                }
                else
                {
                    LogMessage("Message from " + clientNames[clientKey] + ": " + message);
                    BroadcastMessage(clientNames[clientKey] + ": " + message, remote);
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    LogMessage("Error receiving: " + e.Message);
                }
            }
        }
    }
    
    void BroadcastMessage(string message, EndPoint sender)
    {
        byte[] data = Encoding.ASCII.GetBytes(message);
        
        lock (connectedClients)
        {
            foreach (EndPoint client in connectedClients)
            {
                if (!client.Equals(sender))
                {
                    try
                    {
                        serverSocket.SendTo(data, data.Length, SocketFlags.None, client);
                    }
                    catch (Exception e)
                    {
                        LogMessage("Error broadcasting: " + e.Message);
                    }
                }
            }
        }
    }
    
    public void SendMessageToAll(string message)
    {
        BroadcastMessage("SERVER: " + message, null);
    }
    
    public int GetConnectedClientsCount()
    {
        lock (connectedClients)
        {
            return connectedClients.Count;
        }
    }
    
    public List<string> GetClientNames()
    {
        return new List<string>(clientNames.Values);
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
                Debug.Log("[Lab2_UDP_Server] " + messageQueue.Dequeue());
            }
        }
    }
    
    void OnDestroy()
    {
        isRunning = false;
        
        if (serverSocket != null)
        {
            serverSocket.Close();
        }
    }
    
    public void GoToWaitingRoom()
    {
        SceneManager.LoadScene("Lab2_WaitingRoomUDP");
    }
}