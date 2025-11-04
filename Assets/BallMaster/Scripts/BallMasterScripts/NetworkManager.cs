using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    [Header("Config")]
    public int port = 7777;
    public float syncRate = 0.1f;

    public bool isHost = false;
    public bool isConnected = false;
    public string lobbyCode = "";
    
    private static Dictionary<string, string> codeToIPMap = new Dictionary<string, string>();

    private Dictionary<string, IPEndPoint> connectedClients = new Dictionary<string, IPEndPoint>();
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running = false;
    private IPEndPoint hostEndPoint;
    private float lastSyncTime = 0;

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

    void Update()
    {
        if (isHost && isConnected)
        {
            lastSyncTime += Time.deltaTime;
            if (lastSyncTime >= syncRate)
            {
                lastSyncTime = 0;
                SyncGameState();
            }
        }
    }

    #region Host

    public void StartHost()
    {
        isHost = true;
        string ip = GetLocalIP();
        lobbyCode = GenerateRandomCode();
        codeToIPMap[lobbyCode] = ip;
        StartUDP(port);
        isConnected = true;
        Debug.Log("Host iniciado. Código: " + lobbyCode);
    }

    void SyncGameState()
    {
        GameStateData_B state = new GameStateData_B();
        NetworkObject[] objects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        foreach (NetworkObject obj in objects)
        {
            state.objects.Add(new ObjectState_B
            {
                objectId = obj.objectId,
                position = obj.transform.position,
                rotation = obj.transform.rotation
            });
        }

        byte[] data = NetworkProtocolBinary.SerializeGameState(state);
        SendToAllClients(data);
    }

    void SendToAllClients(byte[] data)
    {
        foreach (var client in connectedClients.Values)
        {
            try
            {
                udpClient.Send(data, data.Length, client);
            }
            catch (Exception e)
            {
                Debug.LogError("Error enviando: " + e.Message);
            }
        }
    }

    #endregion

    #region Client

    public void JoinHost(string code)
    {
        isHost = false;
        
        try
        {
            string hostIP;
            
            if (codeToIPMap.ContainsKey(code))
            {
                hostIP = codeToIPMap[code];
            }
            else
            {
                hostIP = DecodeIPFromCode(code);
            }
            
            hostEndPoint = new IPEndPoint(IPAddress.Parse(hostIP), port);
            StartUDP(0);
            SendJoinMessage(code);
            isConnected = true;
            Debug.Log("Conectado a: " + hostIP);
        }
        catch (Exception e)
        {
            Debug.LogError("Error conectando: " + e.Message);
        }
    }

    void SendJoinMessage(string code)
    {
        byte[] data = NetworkProtocolBinary.SerializeString(MessageType.Join, code);
        udpClient.Send(data, data.Length, hostEndPoint);
    }

    void SendToHost(byte[] data)
    {
        if (hostEndPoint != null)
        {
            udpClient.Send(data, data.Length, hostEndPoint);
        }
    }

    #endregion

    #region UDP

    void StartUDP(int portToUse)
    {
        udpClient = new UdpClient(portToUse);
        running = true;
        
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                ProcessMessage(data, remoteEP);
            }
            catch (SocketException) { break; }
            catch (Exception e)
            {
                Debug.LogError("Error recibiendo: " + e.Message);
            }
        }
    }

    void ProcessMessage(byte[] data, IPEndPoint sender)
    {
        MessageType_B type = (MessageType_B)NetworkProtocolBinary.PeekHeader(data);

        switch (type)
        {
            case MessageType_B.Join:
                if (isHost) HandleClientJoin(sender, data);
                break;
            case MessageType_B.GameState:
                if (!isHost) HandleGameState(data);
                break;
            case MessageType_B.PlayerInput:
                if (isHost) HandlePlayerInput(data, sender);
                break;
        }
    }

    void HandleClientJoin(IPEndPoint client, byte[] data)
    {
        string clientId = client.ToString();
        
        if (!connectedClients.ContainsKey(clientId))
        {
            connectedClients[clientId] = client;
            
            string receivedCode = NetworkProtocolBinary.DeserializeString(data);
            string clientIP = client.Address.ToString();
            
            if (!string.IsNullOrEmpty(receivedCode) && receivedCode != "JOIN")
            {
                codeToIPMap[receivedCode] = clientIP;
            }
            
            Debug.Log("Cliente conectado: " + clientId);
        }
    }

    void HandleGameState(byte[] data)
    {
        GameStateData_B state = NetworkProtocolBinary.DeserializeGameState(data);
        
        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (NetworkObjectManager.Instance != null)
            {
                NetworkObjectManager.Instance.ApplyGameState(state);
            }
        });
    }

    void HandlePlayerInput(byte[] data, IPEndPoint sender)
    {
        PlayerInputData_B input = NetworkProtocolBinary.DeserializeInput(data);
    }

    #endregion

    #region Utils

    string GetLocalIP()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }
        
        return "127.0.0.1";
    }

    string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Random random = new System.Random();
        
        string prefix = "";
        for (int i = 0; i < 2; i++)
        {
            prefix += chars[random.Next(chars.Length)];
        }
        
        string ip = GetLocalIP();
        string ipEncoded = EncodeIPCompact(ip);
        
        return prefix + ipEncoded;
    }

    string EncodeIPCompact(string ip)
    {
        string[] octets = ip.Split('.');
        string code = "";
        
        for (int i = 0; i < octets.Length; i++)
        {
            int octet = int.Parse(octets[i]);
            string encoded = ToBase36(octet);
            
            if (encoded.Length == 1)
                encoded = "0" + encoded;
            
            code += encoded;
        }
        
        return code;
    }

    string DecodeIPFromCode(string code)
    {
        string ipPart = code.Substring(2);
        
        string ip = "";
        for (int i = 0; i < ipPart.Length; i += 2)
        {
            if (i + 2 > ipPart.Length) break;
            
            string encoded = ipPart.Substring(i, 2);
            int octet = FromBase36(encoded);
            ip += octet.ToString();
            
            if (i + 2 < ipPart.Length)
                ip += ".";
        }
        
        return ip;
    }

    string ToBase36(int value)
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string result = "";
        
        do
        {
            result = chars[value % 36] + result;
            value /= 36;
        } while (value > 0);
        
        return result;
    }

    int FromBase36(string value)
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int result = 0;
        
        for (int i = 0; i < value.Length; i++)
        {
            result = result * 36 + chars.IndexOf(char.ToUpper(value[i]));
        }
        
        return result;
    }

    public void Disconnect()
    {
        running = false;
        
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join(1000);
        
        if (udpClient != null)
            udpClient.Close();

        connectedClients.Clear();
        isConnected = false;
        isHost = false;
        
        Debug.Log("Desconectado");
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    #endregion

    #region Public API

    public void SendInput(float horizontal, float vertical)
    {
        if (!isHost && isConnected)
        {
            PlayerInputData_B input = new PlayerInputData_B
            {
                horizontal = horizontal,
                vertical = vertical
            };

            byte[] data = NetworkProtocolBinary.SerializeInput(input);
            SendToHost(data);
        }
    }

    public int GetPlayerCount()
    {
        return connectedClients.Count + (isHost ? 1 : 0);
    }

    #endregion
}