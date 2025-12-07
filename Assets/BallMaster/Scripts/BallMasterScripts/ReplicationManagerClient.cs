using System.Collections.Generic;
using UnityEngine;

public class ReplicationManagerClient : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject ballPrefab;

    private NetworkManager networkManager;
    private NetworkObjectManager networkObjectManager;

    private Dictionary<string, GameObject> replicatedObjects = new Dictionary<string, GameObject>();

    private PlayerManager playerManager;
    private BallManager ballManager;

    void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        networkObjectManager = FindFirstObjectByType<NetworkObjectManager>();
        playerManager = FindFirstObjectByType<PlayerManager>();
        ballManager = FindFirstObjectByType<BallManager>();
    }

    void Start()
    {
        if (networkManager != null)
        {
            networkManager.RegisterReplicationClient(this);
        }
    }

    public void ProcessReplicationPackets(byte[] data)
    {
        List<ReplicationPacket> packets = NetworkProtocolBinary.DeserializeReplicationPackets(data);

        foreach (var packet in packets)
        {
            switch (packet.command)
            {
                case ReplicationCommand.Create:
                    HandleCreate(packet);
                    break;
                case ReplicationCommand.Update:
                    HandleUpdate(packet);
                    break;
                case ReplicationCommand.Destroy:
                    HandleDestroy(packet);
                    break;
            }
        }
    }

    private void HandleCreate(ReplicationPacket packet)
    {
        if (replicatedObjects.ContainsKey(packet.networkId))
            return;

        NetworkObject existingNetObj = networkObjectManager?.GetNetworkObject(packet.networkId);
        
        if (existingNetObj != null)
        {
            replicatedObjects[packet.networkId] = existingNetObj.gameObject;
            existingNetObj.UpdateState(packet.position, packet.rotation);
            Debug.Log($"[Client] Linked existing {packet.objectType} ({packet.networkId})");
            
            RegisterWithSpecificManager(existingNetObj.gameObject, packet.objectType, packet.networkId);
            return;
        }

        GameObject prefabToSpawn = null;
        switch (packet.objectType)
        {
            case ReplicatedObjectType.Player:
                prefabToSpawn = playerPrefab;
                break;
            case ReplicatedObjectType.Ball:
                prefabToSpawn = ballPrefab;
                break;
            default:
                break;
        }

        if (prefabToSpawn != null)
        {
            GameObject newObj = Instantiate(prefabToSpawn, packet.position, packet.rotation);
            
            NetworkObject netObj = newObj.GetComponent<NetworkObject>();
            if (netObj == null) netObj = newObj.AddComponent<NetworkObject>();
            
            netObj.objectId = packet.networkId;
            
            if (networkObjectManager != null)
            {
                networkObjectManager.RegisterNetworkObject(netObj);
            }
            
            replicatedObjects[packet.networkId] = newObj;
            
            if (packet.objectType == ReplicatedObjectType.Player)
            {
                PlayerNetworkComponent pnc = newObj.GetComponent<PlayerNetworkComponent>();
                if (pnc != null)
                {
                    bool isMine = playerManager != null && playerManager.IsMyPlayer(packet.networkId);
                    pnc.Initialize(isMine);
                }
            }
            
            RegisterWithSpecificManager(newObj, packet.objectType, packet.networkId);

            Debug.Log($"[Client] Created {packet.objectType} ({packet.networkId})");
        }
    }

    private void RegisterWithSpecificManager(GameObject obj, ReplicatedObjectType type, string netId)
    {
        if (type == ReplicatedObjectType.Ball && ballManager != null)
        {
            Ball ball = obj.GetComponent<Ball>();
            if (ball != null)
            {
                ball.Initialize(ballManager, networkObjectManager);
                if (!ballManager.GetAllBalls().ContainsKey(netId))
                {
                    ballManager.GetAllBalls().Add(netId, ball);
                }
            }
        }
    }

    private void HandleUpdate(ReplicationPacket packet)
    {
        NetworkObject netObj = networkObjectManager?.GetNetworkObject(packet.networkId);
        
        if (netObj != null)
        {
            netObj.UpdateState(packet.position, packet.rotation);
            
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null && packet.velocity != Vector3.zero)
            {
                rb.linearVelocity = packet.velocity;
            }
            
            if (!replicatedObjects.ContainsKey(packet.networkId))
            {
                replicatedObjects[packet.networkId] = netObj.gameObject;
            }
        }
    }

    private void HandleDestroy(ReplicationPacket packet)
    {
        if (replicatedObjects.ContainsKey(packet.networkId))
        {
            GameObject obj = replicatedObjects[packet.networkId];
            replicatedObjects.Remove(packet.networkId);
            
            if (obj != null)
            {
                networkObjectManager?.UnregisterNetworkObject(packet.networkId);
                
                if (packet.objectType == ReplicatedObjectType.Ball && ballManager != null)
                {
                    if (ballManager.GetAllBalls().ContainsKey(packet.networkId))
                        ballManager.GetAllBalls().Remove(packet.networkId);
                }

                Destroy(obj);
                Debug.Log($"[Client] Destroyed {packet.networkId}");
            }
        }
    }

    public int GetReplicatedObjectCount()
    {
        return replicatedObjects.Count;
    }
}
