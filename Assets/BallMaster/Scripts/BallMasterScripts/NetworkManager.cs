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

    [Header("Config")]
    public int port = 4567;

    public bool isHost = false;
    public bool isConnected = false;
    public string lobbyCode = "";

    private static Dictionary<string, string> codeToIPMap = new Dictionary<string, string>();
    private Dictionary<string, IPEndPoint> connectedClients = new Dictionary<string, IPEndPoint>();
    private Dictionary<string, IPEndPoint> clientIdToEndpoint =
        new Dictionary<string, IPEndPoint>();
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
    }

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
            if (Time.time - lastHeartbeatTime >= HEARTBEAT_INTERVAL)
            {
                SendHeartbeatToClients();
                CheckClientTimeouts();
                lastHeartbeatTime = Time.time;
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
                    HandleHeartbeatFromHost();
                break;
            case MessageType.HeartbeatAck:
                if (isHost)
                    HandleHeartbeatAck(sender);
                break;
            case MessageType.BallDrop:
                if (isHost)
                    HandleBallDropFromClient(data, sender);
                else
                    HandleBallDropUpdate(data);
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
        byte[] data = new byte[] { (byte)MessageType.HeartbeatAck };
        SendToHost(data);
    }

    void HandleHeartbeatAck(IPEndPoint sender)
    {
        string clientId = sender.ToString();
        ExecuteOnMainThread(() =>
        {
            if (clientLastHeartbeat.ContainsKey(clientId))
            {
                clientLastHeartbeat[clientId] = Time.time;
            }
        });
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

        return prefix + ipEncoded;
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
        string lastPart = code.Substring(2).TrimStart('0');
        return subnet + "." + lastPart;
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
