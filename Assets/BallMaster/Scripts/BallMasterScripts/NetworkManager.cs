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
    public int port = 4567;
    public float syncRate = 0.1f;

    public bool isHost = false;
    public bool isConnected = false;
    public string lobbyCode = "";

    private static Dictionary<string, string> codeToIPMap = new Dictionary<string, string>();
    private Dictionary<string, IPEndPoint> connectedClients = new Dictionary<string, IPEndPoint>();
    private Dictionary<string, IPEndPoint> clientIdToEndpoint = new Dictionary<string, IPEndPoint>();
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running = false;
    private IPEndPoint hostEndPoint;

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
        if (isHost && isConnected && connectedClients.Count > 0)
        {
            SyncGameState();
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
    }

    void SyncGameState()
    {
        GameStateData state = new GameStateData();
        NetworkObject[] objects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        foreach (NetworkObject obj in objects)
        {
            state.objects.Add(new ObjectState
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

    void SendToAllClientsExcept(byte[] data, IPEndPoint except)
    {
        foreach (var client in connectedClients.Values)
        {
            if (client.Equals(except)) continue;
            
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
        MessageType type = (MessageType)NetworkProtocolBinary.PeekHeader(data);

        switch (type)
        {
            case MessageType.Join:
                if (isHost) HandleClientJoin(sender, data);
                break;
            case MessageType.GameState:
                if (!isHost) HandleGameState(data);
                break;
            case MessageType.PlayerTransform:
                if (isHost) 
                    HandlePlayerTransformFromClient(data, sender);
                else 
                    HandlePlayerTransformUpdate(data);
                break;
            case MessageType.AssignPlayerId:
                if (!isHost) HandleAssignPlayerId(data);
                break;
            case MessageType.SyncExistingPlayers:
                if (!isHost) HandleSyncExistingPlayers(data);
                break;
            case MessageType.SyncExistingBalls:
                if (!isHost) HandleSyncExistingBalls(data);
                break;
            case MessageType.BallState:
                if (!isHost) HandleBallStates(data);
                break;
            case MessageType.BallLaunched:
                if (isHost)
                    HandleBallLaunchedFromClient(data, sender);
                else
                    HandleBallLaunchedUpdate(data);
                break;
        }
    }

    void HandleClientJoin(IPEndPoint client, byte[] data)
    {
        string clientId = client.ToString();

        if (!connectedClients.ContainsKey(clientId))
        {
            connectedClients[clientId] = client;
            clientIdToEndpoint[clientId] = client;

            UnityMainThread.ExecuteInUpdate(() =>
            {
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.HandleClientJoined(clientId);
                }

                if (BallManager.Instance != null)
                {
                    ExistingBallsData existingBalls = BallManager.Instance.GetExistingBallsData();
                    if (existingBalls.balls.Count > 0)
                    {
                        SendExistingBallsToClient(clientId, existingBalls);
                    }
                }
            });
        }
    }

    void HandleGameState(byte[] data)
    {
        GameStateData state = NetworkProtocolBinary.DeserializeGameState(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (NetworkObjectManager.Instance != null)
            {
                NetworkObjectManager.Instance.ApplyGameState(state);
            }
        });
    }

    void HandlePlayerTransformFromClient(byte[] data, IPEndPoint sender)
    {
        PlayerTransformData transform = NetworkProtocolBinary.DeserializePlayerTransform(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (NetworkObjectManager.Instance != null)
            {
                NetworkObject netObj = NetworkObjectManager.Instance.GetNetworkObject(transform.playerId);
                if (netObj != null)
                {
                    netObj.UpdateState(transform.position, transform.rotation);
                }
            }
        });

        SendToAllClientsExcept(data, sender);
    }

    void HandlePlayerTransformUpdate(byte[] data)
    {
        PlayerTransformData transform = NetworkProtocolBinary.DeserializePlayerTransform(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (NetworkObjectManager.Instance != null)
            {
                NetworkObject netObj = NetworkObjectManager.Instance.GetNetworkObject(transform.playerId);
                if (netObj != null)
                {
                    netObj.UpdateState(transform.position, transform.rotation);
                }
            }
        });
    }

    void HandleAssignPlayerId(byte[] data)
    {
        string playerId = NetworkProtocolBinary.DeserializeString(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.ReceiveMyPlayerId(playerId);
            }
        });
    }

    void HandleSyncExistingPlayers(byte[] data)
    {
        ExistingPlayersData playersData = NetworkProtocolBinary.DeserializeExistingPlayers(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.SpawnExistingPlayers(playersData);
            }
        });
    }

    void HandleSyncExistingBalls(byte[] data)
    {
        ExistingBallsData ballsData = NetworkProtocolBinary.DeserializeExistingBalls(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (BallManager.Instance != null)
            {
                BallManager.Instance.SpawnExistingBalls(ballsData);
            }
        });
    }

    void HandleBallStates(byte[] data)
    {
        List<BallStateData> ballStates = NetworkProtocolBinary.DeserializeBallStates(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (BallManager.Instance != null)
            {
                BallManager.Instance.ApplyBallStates(ballStates);
            }
        });
    }

    void HandleBallLaunchedFromClient(byte[] data, IPEndPoint sender)
    {
        BallLaunchData launchData = NetworkProtocolBinary.DeserializeBallLaunch(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (BallManager.Instance != null)
            {
                Ball ball = BallManager.Instance.GetBall(launchData.ballId);
                if (ball != null)
                {
                    ball.Launch(launchData.direction, launchData.launcherId, launchData.launchPosition);
                }
            }
        });

        SendToAllClientsExcept(data, sender);
    }

    void HandleBallLaunchedUpdate(byte[] data)
    {
        BallLaunchData launchData = NetworkProtocolBinary.DeserializeBallLaunch(data);

        UnityMainThread.ExecuteInUpdate(() =>
        {
            if (BallManager.Instance != null)
            {
                Ball ball = BallManager.Instance.GetBall(launchData.ballId);
                if (ball != null)
                {
                    ball.Launch(launchData.direction, launchData.launcherId, launchData.launchPosition);
                }
            }
        });
    }

    public void SendPlayerIdToClient(string clientId, string playerId)
    {
        if (clientIdToEndpoint.ContainsKey(clientId))
        {
            byte[] data = NetworkProtocolBinary.SerializePlayerId(playerId);
            udpClient.Send(data, data.Length, clientIdToEndpoint[clientId]);
        }
    }

    public void SendExistingPlayersToClient(string clientId, ExistingPlayersData playersData)
    {
        if (clientIdToEndpoint.ContainsKey(clientId))
        {
            byte[] data = NetworkProtocolBinary.SerializeExistingPlayers(playersData);
            udpClient.Send(data, data.Length, clientIdToEndpoint[clientId]);
            Debug.Log($"Sent {playersData.players.Count} existing players to {clientId}");
        }
    }

    public void SendExistingBallsToClient(string clientId, ExistingBallsData ballsData)
    {
        if (clientIdToEndpoint.ContainsKey(clientId))
        {
            byte[] data = NetworkProtocolBinary.SerializeExistingBalls(ballsData);
            udpClient.Send(data, data.Length, clientIdToEndpoint[clientId]);
            Debug.Log($"Sent {ballsData.balls.Count} existing balls to {clientId}");
        }
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
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    #endregion

    #region Public API

    public void SendPlayerTransform(string playerId, Vector3 position, Quaternion rotation)
    {
        if (!isConnected) return;

        PlayerTransformData transform = new PlayerTransformData
        {
            playerId = playerId,
            position = position,
            rotation = rotation
        };

        byte[] data = NetworkProtocolBinary.SerializePlayerTransform(transform);
        
        if (isHost)
        {
            SendToAllClients(data);
        }
        else
        {
            SendToHost(data);
        }
    }

    public int GetPlayerCount()
    {
        if (isHost)
        {
            return connectedClients.Count + 1;
        }
        else
        {
            return isConnected ? 2 : 0;
        }
    }

    public void SendBallStates(List<BallStateData> ballStates)
    {
        if (!isConnected || !isHost) return;

        byte[] data = NetworkProtocolBinary.SerializeBallStates(ballStates);
        SendToAllClients(data);
    }

    public void SendBallLaunch(string ballId, Vector3 direction, string launcherId, Vector3 launchPosition)
    {
        if (!isConnected) return;

        BallLaunchData launchData = new BallLaunchData
        {
            ballId = ballId,
            direction = direction,
            launcherId = launcherId,
            launchPosition = launchPosition
        };

        byte[] data = NetworkProtocolBinary.SerializeBallLaunch(launchData);

        if (isHost)
        {
            SendToAllClients(data);
        }
        else
        {
            SendToHost(data);
        }
    }

    #endregion
}