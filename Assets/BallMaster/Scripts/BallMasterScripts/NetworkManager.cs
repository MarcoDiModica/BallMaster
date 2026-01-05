using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    private PlayerManager playerManager;
    private BallManager ballManager;
    private NetworkObjectManager networkObjectManager;
    private ReplicationManagerServer replicationServer;
    private ReplicationManagerClient replicationClient;
    private LeaderboardManager leaderboardManager;

    [Header("Config")]
    public int port = 4567;

    public bool isHost = false;
    public bool isConnected = false;
    public bool isWaitingForConnection = false;
    public string lobbyCode = "";
    public string lastConnectionError = "";

    public event Action OnConnectionSuccess;
    public event Action<string> OnConnectionFailed;

    private static Dictionary<string, string> codeToIPMap = new Dictionary<string, string>();
    private Dictionary<string, IPEndPoint> connectedClients = new Dictionary<string, IPEndPoint>();
    private Dictionary<string, IPEndPoint> clientIdToEndpoint =
        new Dictionary<string, IPEndPoint>();
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running = false;
    private IPEndPoint hostEndPoint;

    private Dictionary<string, float> clientLastHeartbeat = new Dictionary<string, float>();
    private Dictionary<string, float> clientHeartbeatSentTime = new Dictionary<string, float>();
    private Dictionary<string, int> clientPingMs = new Dictionary<string, int>();
    private float lastHeartbeatTime;
    private const float HEARTBEAT_INTERVAL = 1f;
    private const float CLIENT_TIMEOUT = 10f;
    private float clientRttMs = 0f;
    private float connectionAttemptTime = 0f;
    private const float CONNECTION_TIMEOUT = 5f;
    private bool receivedFirstResponse = false;
    private float lastJoinSendTime = 0f;
    private const float JOIN_RETRY_INTERVAL = 0.5f;

    void Awake()
    {
        var managers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
        if (managers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        leaderboardManager = FindFirstObjectByType<LeaderboardManager>();
    }

    public void RegisterPlayerManager(PlayerManager pm)
    {
        playerManager = pm;
    }

    public void RegisterBallManager(BallManager bm)
    {
        ballManager = bm;
    }

    public void RegisterNetworkObjectManager(NetworkObjectManager nom)
    {
        networkObjectManager = nom;
    }

    public void RegisterReplicationServer(ReplicationManagerServer rs)
    {
        replicationServer = rs;
    }

    public void RegisterReplicationClient(ReplicationManagerClient rc)
    {
        replicationClient = rc;
        
        // Process buffered packets
        while (bufferedReplicationPackets.Count > 0)
        {
            byte[] data = bufferedReplicationPackets.Dequeue();
            replicationClient.ProcessReplicationPackets(data);
        }
    }

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    
    // Packet buffering for Replication
    private Queue<byte[]> bufferedReplicationPackets = new Queue<byte[]>();

    public void ExecuteOnMainThread(Action action)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }

    void Update()
    {
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
        }

        if (isHost && isConnected)
        {
            if (Time.time - lastHeartbeatTime >= HEARTBEAT_INTERVAL)
            {
                SendHeartbeatToClients();
                CheckClientTimeouts();
                lastHeartbeatTime = Time.time;
            }
        }

        if (isWaitingForConnection && !receivedFirstResponse)
        {
            if (Time.time - lastJoinSendTime >= JOIN_RETRY_INTERVAL)
            {
                SendJoinMessage(currentLobbyCode);
                lastJoinSendTime = Time.time;
            }

            if (Time.time - connectionAttemptTime >= CONNECTION_TIMEOUT)
            {
                lastConnectionError = "Conexión agotada: el servidor no responde";
                isWaitingForConnection = false;
                isConnected = false;
                OnConnectionFailed?.Invoke(lastConnectionError);
                ResetClientState();
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

        int maxRetries = 10;
        int currentPort = port;
        bool success = false;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                StartUDP(currentPort);
                port = currentPort;
                success = true;
                break;
            }
            catch (SocketException)
            {
                currentPort++;
            }
        }

        if (!success)
        {
            Debug.LogError("Could not find available port after " + maxRetries + " attempts");
            isHost = false;
            return;
        }

        isConnected = true;
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
                Debug.LogError("Error sending: " + e.Message);
            }
        }
    }

    public void SendToClient(string clientId, byte[] data)
    {
        if (clientIdToEndpoint.ContainsKey(clientId))
        {
            try
            {
                udpClient.Send(data, data.Length, clientIdToEndpoint[clientId]);
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending to client " + clientId + ": " + e.Message);
            }
        }
    }

    void SendToAllClientsExcept(byte[] data, IPEndPoint except)
    {
        foreach (var client in connectedClients.Values)
        {
            if (client.Equals(except))
                continue;

            try
            {
                udpClient.Send(data, data.Length, client);
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending: " + e.Message);
            }
        }
    }

    #endregion

    #region Client

    private string currentLobbyCode;

    public void JoinHost(string code)
    {
        isHost = false;
        lastConnectionError = "";
        receivedFirstResponse = false;

        if (isConnected || udpClient != null)
        {
            ResetClientState();
        }

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

            int hostPort = DecodePortFromCode(code);
            hostEndPoint = new IPEndPoint(IPAddress.Parse(hostIP), hostPort);
            StartUDP(0);

            currentLobbyCode = code;
            isWaitingForConnection = true;
            connectionAttemptTime = Time.time;
            
            SendJoinMessage(code);
            lastJoinSendTime = Time.time;
        }
        catch (Exception e)
        {
            lastConnectionError = "Código inválido: " + e.Message;
            OnConnectionFailed?.Invoke(lastConnectionError);
            ResetClientState();
        }
    }

    private void ResetClientState()
    {
        running = false;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch { }
            udpClient = null;
        }

        isConnected = false;
        pendingPlayerId = null;
        hostEndPoint = null;

        if (playerManager != null)
        {
            playerManager.ResetState();
        }
    }

    public void SendPendingJoinRequest()
    {
        if (!string.IsNullOrEmpty(currentLobbyCode))
        {
            SendJoinMessage(currentLobbyCode);
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
            catch (SocketException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogError("Error receiving: " + e.Message);
            }
        }
    }

    void ProcessMessage(byte[] data, IPEndPoint sender)
    {
        MessageType type = (MessageType)NetworkProtocolBinary.PeekHeader(data);

        switch (type)
        {
            case MessageType.Join:
                if (isHost)
                    HandleClientJoin(sender, data);
                break;
            case MessageType.PlayerTransform:
                if (isHost)
                    HandlePlayerTransformFromClient(data, sender);
                break;
            case MessageType.AssignPlayerId:
                if (!isHost)
                    HandleAssignPlayerId(data);
                break;
            case MessageType.BallLaunched:
                if (isHost)
                    HandleBallLaunchedFromClient(data, sender);
                else
                    HandleBallLaunchedUpdate(data);
                break;
            case MessageType.BallEquip:
                if (isHost)
                    HandleBallEquipFromClient(data, sender);
                else
                    HandleBallEquipUpdate(data);
                break;
            case MessageType.Replication:
                if (!isHost)
                    HandleReplicationPackets(data);
                break;
            case MessageType.Heartbeat:
                if (!isHost)
                    HandleHeartbeatFromHost(data);
                break;
            case MessageType.HeartbeatAck:
                if (isHost)
                    HandleHeartbeatAck(sender, data);
                break;
            case MessageType.BallDrop:
                if (isHost)
                    HandleBallDropFromClient(data, sender);
                else
                    HandleBallDropUpdate(data);
                break;
            case MessageType.PlayerRespawn:
                if (!isHost)
                    HandlePlayerRespawn(data);
                break;
            case MessageType.KillEvent:
                if (!isHost)
                    HandleKillEvent(data);
                break;
            case MessageType.PingUpdate:
                if (!isHost)
                    HandlePingUpdate(data);
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

            ExecuteOnMainThread(() =>
            {
                clientLastHeartbeat[clientId] = Time.time;

                if (playerManager != null)
                {
                    playerManager.HandleClientJoined(clientId);
                }

                if (replicationServer != null)
                {
                    replicationServer.SendInitialStateToClient(clientId);
                }
            });
        }
    }

    void HandlePlayerTransformFromClient(byte[] data, IPEndPoint sender)
    {
        PlayerTransformData transform = NetworkProtocolBinary.DeserializePlayerTransform(data);

        ExecuteOnMainThread(() =>
        {
            if (networkObjectManager != null)
            {
                NetworkObject netObj = networkObjectManager.GetNetworkObject(transform.playerId);
                if (netObj != null)
                {
                    netObj.UpdateState(transform.position, transform.rotation);
                    netObj.MarkDirty();
                }
            }
        });
    }

    private string pendingPlayerId;

    public string GetPendingPlayerId()
    {
        return pendingPlayerId;
    }

    void HandleAssignPlayerId(byte[] data)
    {
        string playerId = NetworkProtocolBinary.DeserializeString(data);

        ExecuteOnMainThread(() =>
        {
            if (!receivedFirstResponse)
            {
                receivedFirstResponse = true;
                isConnected = true;
                isWaitingForConnection = false;
                OnConnectionSuccess?.Invoke();
            }

            if (playerManager != null)
            {
                playerManager.ReceiveMyPlayerId(playerId);
            }
            else
            {
                pendingPlayerId = playerId;
            }
        });
    }

    void HandleBallLaunchedFromClient(byte[] data, IPEndPoint sender)
    {
        BallLaunchData launchData = NetworkProtocolBinary.DeserializeBallLaunch(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                Ball ball = ballManager.GetBall(launchData.ballId);
                if (ball != null)
                {
                    ball.Launch(
                        launchData.direction,
                        launchData.launcherId,
                        launchData.launchPosition
                    );
                    NetworkObject netObj = ball.GetComponent<NetworkObject>();
                    if (netObj)
                        netObj.MarkDirty();
                }
            }
        });

        SendToAllClientsExcept(data, sender);
    }

    void HandleBallLaunchedUpdate(byte[] data)
    {
        BallLaunchData launchData = NetworkProtocolBinary.DeserializeBallLaunch(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                Ball ball = ballManager.GetBall(launchData.ballId);
                if (ball != null)
                {
                    ball.Launch(
                        launchData.direction,
                        launchData.launcherId,
                        launchData.launchPosition
                    );
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

    #endregion

    public void SendBallLaunch(
        string ballId,
        Vector3 direction,
        string launcherId,
        Vector3 launchPosition
    )
    {
        if (!isConnected)
            return;

        BallLaunchData launchData = new BallLaunchData
        {
            ballId = ballId,
            direction = direction,
            launcherId = launcherId,
            launchPosition = launchPosition,
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

    public void SendBallDrop(
        string ballId,
        Vector3 direction,
        string launcherId,
        Vector3 launchPosition
    )
    {
        if (!isConnected)
            return;

        BallLaunchData launchData = new BallLaunchData
        {
            ballId = ballId,
            direction = direction,
            launcherId = launcherId,
            launchPosition = launchPosition,
        };

        byte[] data = NetworkProtocolBinary.SerializeBallDrop(launchData);

        if (isHost)
        {
            SendToAllClients(data);
        }
        else
        {
            SendToHost(data);
        }
    }

    void HandleBallDropFromClient(byte[] data, IPEndPoint sender)
    {
        BallLaunchData launchData = NetworkProtocolBinary.DeserializeBallLaunch(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                Ball ball = ballManager.GetBall(launchData.ballId);
                if (ball != null)
                {
                    ball.Drop(
                        launchData.direction,
                        launchData.launcherId,
                        launchData.launchPosition
                    );
                    NetworkObject netObj = ball.GetComponent<NetworkObject>();
                    if (netObj)
                        netObj.MarkDirty();
                }
            }
        });

        SendToAllClientsExcept(data, sender);
    }

    void HandleBallDropUpdate(byte[] data)
    {
        BallLaunchData launchData = NetworkProtocolBinary.DeserializeBallLaunch(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                Ball ball = ballManager.GetBall(launchData.ballId);
                if (ball != null)
                {
                    ball.Drop(
                        launchData.direction,
                        launchData.launcherId,
                        launchData.launchPosition
                    );
                }
            }
        });
    }

    public void SendBallEquip(string ballId, string playerId)
    {
        if (!isConnected)
            return;

        BallEquipData equipData = new BallEquipData { ballId = ballId, playerId = playerId };

        byte[] data = NetworkProtocolBinary.SerializeBallEquip(equipData);

        if (isHost)
        {
            SendToAllClients(data);
        }
        else
        {
            SendToHost(data);
        }
    }

    void HandleBallEquipFromClient(byte[] data, IPEndPoint sender)
    {
        BallEquipData equipData = NetworkProtocolBinary.DeserializeBallEquip(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                ballManager.EquipBallNetworked(equipData.ballId, equipData.playerId);
            }
        });

        SendToAllClientsExcept(data, sender);
    }

    void HandleBallEquipUpdate(byte[] data)
    {
        BallEquipData equipData = NetworkProtocolBinary.DeserializeBallEquip(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                ballManager.EquipBallNetworked(equipData.ballId, equipData.playerId);
            }
        });
    }

    public void SendPlayerRespawn(string playerId, Vector3 position)
    {
        if (!isHost)
            return;

        string clientId = playerId.Replace("Player_", "");

        if (clientIdToEndpoint.ContainsKey(clientId))
        {
            PlayerRespawnData respawnData = new PlayerRespawnData
            {
                playerId = playerId,
                respawnPosition = position,
            };

            byte[] data = NetworkProtocolBinary.SerializePlayerRespawn(respawnData);
            SendToClient(clientId, data);
        }
    }

    void HandlePlayerRespawn(byte[] data)
    {
        PlayerRespawnData respawnData = NetworkProtocolBinary.DeserializePlayerRespawn(data);

        ExecuteOnMainThread(() =>
        {
            if (networkObjectManager != null)
            {
                NetworkObject playerObj = networkObjectManager.GetNetworkObject(
                    respawnData.playerId
                );
                if (playerObj != null)
                {
                    PlayerController pc = playerObj.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.Respawn(respawnData.respawnPosition);
                    }
                }
            }
        });
    }

    public void SendKillEvent(string killerId, string killerName, string victimId, string victimName)
    {
        if (!isHost || !isConnected)
            return;

        KillEventData killData = new KillEventData
        {
            killerId = killerId,
            killerName = killerName,
            victimId = victimId,
            victimName = victimName,
        };

        byte[] data = NetworkProtocolBinary.SerializeKillEvent(killData);
        SendToAllClients(data);
    }

    void HandleKillEvent(byte[] data)
    {
        KillEventData killData = NetworkProtocolBinary.DeserializeKillEvent(data);

        ExecuteOnMainThread(() =>
        {
            if (leaderboardManager != null)
            {
                leaderboardManager.RegisterKillFromNetwork(killData);
            }
        });
    }

    void HandleReplicationPackets(byte[] data)
    {
        ExecuteOnMainThread(() =>
        {
            if (replicationClient != null)
            {
                replicationClient.ProcessReplicationPackets(data);
            }
            else
            {
                // Buffer packets if client is not ready (e.g. scene loading)
                bufferedReplicationPackets.Enqueue(data);
            }
        });
    }

    public void SendReplicationData(byte[] data)
    {
        if (!isConnected || !isHost)
            return;
        SendToAllClients(data);
    }

    public void SendReplicationDataToClient(string clientId, byte[] data)
    {
        SendToClient(clientId, data);
    }

    #region Heartbeat System

    void SendHeartbeatToClients()
    {
        float currentTime = Time.time;
        foreach (var kvp in connectedClients)
        {
            string clientId = kvp.Key;
            clientHeartbeatSentTime[clientId] = currentTime;
        }
        using (var stream = new System.IO.MemoryStream())
        using (var writer = new System.IO.BinaryWriter(stream))
        {
            writer.Write((byte)MessageType.Heartbeat);
            writer.Write(currentTime);
            byte[] data = stream.ToArray();
            SendToAllClients(data);
        }
    }

    void CheckClientTimeouts()
    {
        List<string> timedOutClients = new List<string>();

        foreach (var kvp in clientLastHeartbeat)
        {
            if (Time.time - kvp.Value > CLIENT_TIMEOUT)
            {
                timedOutClients.Add(kvp.Key);
            }
        }

        foreach (string clientId in timedOutClients)
        {
            connectedClients.Remove(clientId);
            clientIdToEndpoint.Remove(clientId);
            clientLastHeartbeat.Remove(clientId);
            clientHeartbeatSentTime.Remove(clientId);
            clientPingMs.Remove(clientId);

            ExecuteOnMainThread(() =>
            {
                if (playerManager != null)
                {
                    playerManager.HandleClientDisconnected(clientId);
                }
            });
        }
    }

    void HandleHeartbeatFromHost(byte[] data)
    {
        float sentTime = 0f;
        using (var stream = new System.IO.MemoryStream(data))
        using (var reader = new System.IO.BinaryReader(stream))
        {
            reader.ReadByte();
            sentTime = reader.ReadSingle();
        }
        using (var stream = new System.IO.MemoryStream())
        using (var writer = new System.IO.BinaryWriter(stream))
        {
            writer.Write((byte)MessageType.HeartbeatAck);
            writer.Write(sentTime);
            byte[] ackData = stream.ToArray();
            SendToHost(ackData);
        }
    }

    void HandleHeartbeatAck(IPEndPoint sender, byte[] data)
    {
        string clientId = sender.ToString();
        float sentTime = 0f;
        using (var stream = new System.IO.MemoryStream(data))
        using (var reader = new System.IO.BinaryReader(stream))
        {
            reader.ReadByte();
            sentTime = reader.ReadSingle();
        }
        ExecuteOnMainThread(() =>
        {
            if (clientLastHeartbeat.ContainsKey(clientId))
            {
                clientLastHeartbeat[clientId] = Time.time;
                float rtt = Time.time - sentTime;
                int pingMs = Mathf.RoundToInt(rtt * 1000f);
                clientPingMs[clientId] = pingMs;
                BroadcastPingUpdate(clientId, pingMs);
                UpdateLeaderboardPing(clientId, pingMs);
            }
        });
    }

    void BroadcastPingUpdate(string clientId, int pingMs)
    {
        string playerId = "Player_" + clientId;
        PingUpdateData pingData = new PingUpdateData { playerId = playerId, pingMs = pingMs };
        byte[] data = NetworkProtocolBinary.SerializePingUpdate(pingData);
        SendToAllClients(data);
    }

    void UpdateLeaderboardPing(string clientId, int pingMs)
    {
        string playerId = "Player_" + clientId;
        if (leaderboardManager != null)
        {
            var stats = leaderboardManager.GetPlayerStats(playerId);
            if (stats != null)
                stats.UpdatePing(pingMs);
        }
    }

    void HandlePingUpdate(byte[] data)
    {
        PingUpdateData pingData = NetworkProtocolBinary.DeserializePingUpdate(data);
        ExecuteOnMainThread(() =>
        {
            if (leaderboardManager != null)
            {
                var stats = leaderboardManager.GetPlayerStats(pingData.playerId);
                if (stats != null)
                    stats.UpdatePing(pingData.pingMs);
            }
        });
    }

    public int GetClientPing(string clientId)
    {
        return clientPingMs.ContainsKey(clientId) ? clientPingMs[clientId] : 0;
    }

    public float GetMyPing()
    {
        return clientRttMs;
    }

    #endregion

    public void SendMyPlayerTransform(string playerId, Vector3 pos, Quaternion rot)
    {
        if (!isConnected || isHost)
            return;

        PlayerTransformData transform = new PlayerTransformData
        {
            playerId = playerId,
            position = pos,
            rotation = rot,
        };

        byte[] data = NetworkProtocolBinary.SerializePlayerTransform(transform);
        SendToHost(data);
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
        int portOffset = port - 4567;
        string portSuffix = portOffset > 0 ? portOffset.ToString() : "";

        return prefix + ipEncoded + portSuffix;
    }

    string EncodeIPCompact(string ip)
    {
        string[] parts = ip.Split('.');
        return parts[parts.Length - 1].PadLeft(3, '0');
    }

    string DecodeIPFromCode(string code)
    {
        string ip = GetLocalIP();
        string subnet = ip.Substring(0, ip.LastIndexOf('.'));
        string ipPart = code.Substring(2, 3).TrimStart('0');
        if (string.IsNullOrEmpty(ipPart))
            ipPart = "0";
        return subnet + "." + ipPart;
    }

    int DecodePortFromCode(string code)
    {
        if (code.Length > 5)
        {
            string portPart = code.Substring(5);
            if (int.TryParse(portPart, out int offset))
            {
                return 4567 + offset;
            }
        }
        return 4567;
    }

    public void Disconnect()
    {
        if (isHost)
        {
            running = false;
        }
        ResetClientState();
    }

    #endregion
}
