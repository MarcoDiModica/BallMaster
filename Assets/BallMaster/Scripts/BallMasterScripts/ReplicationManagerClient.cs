using System.Collections.Generic;
using UnityEngine;

public class ReplicationManagerClient : MonoBehaviour
{
    private NetworkManager networkManager;
    private NetworkObjectManager networkObjectManager;
    private BallManager ballManager;
    private PlayerManager playerManager;

    private Dictionary<string, GameObject> replicatedObjects = new Dictionary<string, GameObject>();

    void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        networkObjectManager = FindFirstObjectByType<NetworkObjectManager>();
        ballManager = FindFirstObjectByType<BallManager>();
        playerManager = FindFirstObjectByType<PlayerManager>();
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

        NetworkObject netObj = networkObjectManager?.GetNetworkObject(packet.networkId);
        
        if (netObj != null)
        {
            replicatedObjects[packet.networkId] = netObj.gameObject;
            netObj.transform.position = packet.position;
            netObj.transform.rotation = packet.rotation;
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
                Destroy(obj);
            }
        }
    }

    public int GetReplicatedObjectCount()
    {
        return replicatedObjects.Count;
    }
}
