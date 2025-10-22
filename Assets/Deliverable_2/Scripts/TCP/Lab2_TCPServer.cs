using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Lab2_TCPServer : MonoBehaviour
{
    private Socket serverSocket;
    private Thread listenThread;
    private bool isRunning = false;
    private int port = 9050;
    private string serverName = "Lab2_GameServer";
    
    private List<Socket> connectedClients = new List<Socket>();
    private List<string> clientNames = new List<string>();
    private Queue<string> messageQueue = new Queue<string>();
    private object queueLock = new object();
    
    public static Lab2_TCPServer Instance;
    
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
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
            serverSocket.Bind(ipep);
            serverSocket.Listen(10);
            
            isRunning = true;
            listenThread = new Thread(ListenForConnections);
            listenThread.Start();
            
            LogMessage("Server started on port " + port);
        }
        catch (Exception e)
        {
            LogMessage("Error starting server: " + e.Message);
        }
    }
    
    void ListenForConnections()
    {
        while (isRunning)
        {
            try
            {
                Socket client = serverSocket.Accept();
                
                lock (connectedClients)
                {
                    connectedClients.Add(client);
                }
                
                LogMessage("Client connected: " + client.RemoteEndPoint.ToString());
                
                byte[] welcomeData = Encoding.ASCII.GetBytes(serverName);
                client.Send(welcomeData);
                
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    LogMessage("Error accepting client: " + e.Message);
                }
            }
        }
    }
    
    void HandleClient(Socket client)
    {
        byte[] buffer = new byte[1024];
        string clientName = "";
        bool firstMessage = true;
        
        while (isRunning && client.Connected)
        {
            try
            {
                int recv = client.Receive(buffer);
                
                if (recv == 0)
                {
                    break;
                }
                
                string message = Encoding.ASCII.GetString(buffer, 0, recv);
                
                if (firstMessage)
                {
                    clientName = message;
                    lock (clientNames)
                    {
                        clientNames.Add(clientName);
                    }
                    LogMessage("Client name: " + clientName);
                    BroadcastMessage("SERVER: " + clientName + " joined the room", client);
                    firstMessage = false;
                }
                else
                {
                    LogMessage("Message from " + clientName + ": " + message);
                    BroadcastMessage(clientName + ": " + message, client);
                }
            }
            catch (Exception e)
            {
                LogMessage("Error handling client: " + e.Message);
                break;
            }
        }
        
        lock (connectedClients)
        {
            connectedClients.Remove(client);
        }
        
        lock (clientNames)
        {
            clientNames.Remove(clientName);
        }
        
        client.Close();
        LogMessage("Client disconnected: " + clientName);
        BroadcastMessage("SERVER: " + clientName + " left the room", null);
    }
    
    void BroadcastMessage(string message, Socket sender)
    {
        byte[] data = Encoding.ASCII.GetBytes(message);
        
        lock (connectedClients)
        {
            foreach (Socket client in connectedClients)
            {
                if (client != sender && client.Connected)
                {
                    try
                    {
                        client.Send(data);
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
        lock (clientNames)
        {
            return new List<string>(clientNames);
        }
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
                Debug.Log("[Lab2_TCP_Server] " + messageQueue.Dequeue());
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
        
        lock (connectedClients)
        {
            foreach (Socket client in connectedClients)
            {
                client.Close();
            }
            connectedClients.Clear();
        }
    }
    
    public void GoToWaitingRoom()
    {
        SceneManager.LoadScene("Lab2_WaitingRoomTCP");
    }
}