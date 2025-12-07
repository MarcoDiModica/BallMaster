using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    private PlayerManager playerManager;
    private BallManager ballManager;
    private NetworkObjectManager networkObjectManager;
    private ReplicationManagerServer replicationServer;
    private ReplicationManagerClient replicationClient;

    [Header("Config")]
    public int port = 4567;

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
    
    // Heartbeat system
    private Dictionary<string, float> clientLastHeartbeat = new Dictionary<string, float>();
    private float lastHeartbeatTime;
    private const float HEARTBEAT_INTERVAL = 2f;
    private const float CLIENT_TIMEOUT = 10f;
    
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
    
    public void RegisterPlayerManager(PlayerManager pm) { playerManager = pm; }
    public void RegisterBallManager(BallManager bm) { ballManager = bm; }
    public void RegisterNetworkObjectManager(NetworkObjectManager nom) { networkObjectManager = nom; }
    public void RegisterReplicationServer(ReplicationManagerServer rs) { replicationServer = rs; }
    public void RegisterReplicationClient(ReplicationManagerClient rc) { replicationClient = rc; }

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

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
            // Heartbeat system - send to clients and check for timeouts
            if (Time.time - lastHeartbeatTime >= HEARTBEAT_INTERVAL)
            {
                SendHeartbeatToClients();
                CheckClientTimeouts();
                lastHeartbeatTime = Time.time;
            }

            if (networkObjectManager != null)
            {
                foreach (var obj in networkObjectManager.GetAllNetworkObjects())
                {
                    if (obj.isDirty)
                    {
                        SendObjectUpdate(obj);
                        obj.isDirty = false;
                    }
                }
            }
        }
    }
    
    void SendObjectUpdate(NetworkObject obj)
    {
         GameStateData state = new GameStateData();
         state.objects.Add(new ObjectState
         {
             objectId = obj.objectId,
             position = obj.transform.position,
             rotation = obj.transform.rotation
         });
         
         byte[] data = NetworkProtocolBinary.SerializeGameState(state);
         SendToAllClients(data);
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
        if (networkObjectManager == null) return;

        GameStateData state = new GameStateData();
        foreach (NetworkObject obj in networkObjectManager.GetAllNetworkObjects())
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
                Debug.LogError("Error sending: " + e.Message);
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

        // Clean up any existing connection before reconnecting
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

            hostEndPoint = new IPEndPoint(IPAddress.Parse(hostIP), port);
            StartUDP(0);
            
            currentLobbyCode = code;
            isConnected = true;
        }
        catch (Exception e)
        {
            Debug.LogError("Error connecting: " + e.Message);
        }
    }

    private void ResetClientState()
    {
        // Stop existing network threads and cleanup
        running = false;
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }
        
        if (udpClient != null)
        {
            try { udpClient.Close(); } catch { }
            udpClient = null;
        }
        
        // Reset connection state
        isConnected = false;
        pendingPlayerId = null;
        hostEndPoint = null;
        
        // Notify PlayerManager to clean up
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
            catch (SocketException) { break; }
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
            case MessageType.BallEquip:
                if (isHost)
                    HandleBallEquipFromClient(data, sender);
                else
                    HandleBallEquipUpdate(data);
                break;
            case MessageType.Replication:
                if (!isHost) HandleReplicationPackets(data);
                break;
            case MessageType.Heartbeat:
                if (!isHost) HandleHeartbeatFromHost();
                break;
            case MessageType.HeartbeatAck:
                if (isHost) HandleHeartbeatAck(sender);
                break;
            case MessageType.PlayerSpawned:
                if (!isHost) HandlePlayerSpawned(data);
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
            clientLastHeartbeat[clientId] = Time.time;

            ExecuteOnMainThread(() =>
            {
                if (playerManager != null)
                {
                    playerManager.HandleClientJoined(clientId);
                }

                if (ballManager != null)
                {
                    ExistingBallsData existingBalls = ballManager.GetExistingBallsData();
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

        ExecuteOnMainThread(() =>
        {
            if (networkObjectManager != null)
            {
                networkObjectManager.ApplyGameState(state);
            }
        });
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
                }
            }
        });

        SendToAllClientsExcept(data, sender);
    }

    void HandlePlayerTransformUpdate(byte[] data)
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

    void HandleSyncExistingPlayers(byte[] data)
    {
        ExistingPlayersData playersData = NetworkProtocolBinary.DeserializeExistingPlayers(data);

        ExecuteOnMainThread(() =>
        {
            if (playerManager != null)
            {
                playerManager.SpawnExistingPlayers(playersData);
            }
        });
    }

    void HandlePlayerSpawned(byte[] data)
    {
        ExistingPlayerData playerData = NetworkProtocolBinary.DeserializePlayerSpawned(data);

        ExecuteOnMainThread(() =>
        {
            if (playerManager != null)
            {
                playerManager.SpawnNetworkedPlayer(playerData);
            }
        });
    }

    public void BroadcastNewPlayerToOtherClients(string exceptClientId, ExistingPlayerData playerData)
    {
        byte[] data = NetworkProtocolBinary.SerializePlayerSpawned(playerData);
        
        foreach (var kvp in connectedClients)
        {
            if (kvp.Key == exceptClientId) continue;
            
            try
            {
                udpClient.Send(data, data.Length, kvp.Value);
            }
            catch (Exception e)
            {
                Debug.LogError("Error broadcasting new player: " + e.Message);
            }
        }
    }

    void HandleSyncExistingBalls(byte[] data)
    {
        ExistingBallsData ballsData = NetworkProtocolBinary.DeserializeExistingBalls(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                ballManager.SpawnExistingBalls(ballsData);
            }
        });
    }

    void HandleBallStates(byte[] data)
    {
        List<BallStateData> ballStates = NetworkProtocolBinary.DeserializeBallStates(data);

        ExecuteOnMainThread(() =>
        {
            if (ballManager != null)
            {
                ballManager.ApplyBallStates(ballStates);
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
                    ball.Launch(launchData.direction, launchData.launcherId, launchData.launchPosition);
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
        }
    }

    public void SendExistingBallsToClient(string clientId, ExistingBallsData ballsData)
    {
        if (clientIdToEndpoint.ContainsKey(clientId))
        {
            byte[] data = NetworkProtocolBinary.SerializeExistingBalls(ballsData);
            udpClient.Send(data, data.Length, clientIdToEndpoint[clientId]);
        }
    }

    #endregion

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

    public void SendBallEquip(string ballId, string playerId)
    {
        if (!isConnected) return;

        BallEquipData equipData = new BallEquipData
        {
            ballId = ballId,
            playerId = playerId
        };

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

    void HandleReplicationPackets(byte[] data)
    {
        ExecuteOnMainThread(() =>
        {
            if (replicationClient != null)
            {
                replicationClient.ProcessReplicationPackets(data);
            }
        });
    }

    public void SendReplicationData(byte[] data)
    {
        if (!isConnected || !isHost) return;
        SendToAllClients(data);
    }

    #region Heartbeat System

    void SendHeartbeatToClients()
    {
        byte[] data = new byte[] { (byte)MessageType.Heartbeat };
        SendToAllClients(data);
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
            Debug.LogWarning($"Client {clientId} timed out - removing from connected clients");
            connectedClients.Remove(clientId);
            clientIdToEndpoint.Remove(clientId);
            clientLastHeartbeat.Remove(clientId);
            
            ExecuteOnMainThread(() =>
            {
                if (playerManager != null)
                {
                    playerManager.HandleClientDisconnected(clientId);
                }
            });
        }
    }

    void HandleHeartbeatFromHost()
    {
        // Client responds to host heartbeat
        byte[] data = new byte[] { (byte)MessageType.HeartbeatAck };
        SendToHost(data);
    }

    void HandleHeartbeatAck(IPEndPoint sender)
    {
        string clientId = sender.ToString();
        if (clientLastHeartbeat.ContainsKey(clientId))
        {
            clientLastHeartbeat[clientId] = Time.time;
        }
    }

    #endregion

    public void SendMyPlayerTransform(string playerId, Vector3 pos, Quaternion rot)
    {
        if (!isConnected || isHost) return;

        PlayerTransformData transform = new PlayerTransformData
        {
            playerId = playerId,
            position = pos,
            rotation = rot
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
}