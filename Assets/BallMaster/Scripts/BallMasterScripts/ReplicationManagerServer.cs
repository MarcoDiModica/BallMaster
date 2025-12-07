using System;
using System.Collections.Generic;
using UnityEngine;

public enum ReplicationCommand : byte
{
    Create = 0,
    Update = 1,
    Destroy = 2
}

public enum ReplicatedObjectType : byte
{
    Player = 0,
    Ball = 1,
    NetworkObject = 2
}

[Serializable]
public class ReplicationPacket
{
    public ReplicationCommand command;
    public string networkId;
    public ReplicatedObjectType objectType;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
}

public class ReplicationManagerServer : MonoBehaviour
{
    [Header("Configuration")]
    public float replicationInterval = 0.1f;

    private NetworkManager networkManager;
    private NetworkObjectManager networkObjectManager;

    private Dictionary<string, ReplicatedObjectType> registeredObjects = new Dictionary<string, ReplicatedObjectType>();
    private List<ReplicationPacket> pendingPackets = new List<ReplicationPacket>();
    private float lastReplicationTime;

    void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        networkObjectManager = FindFirstObjectByType<NetworkObjectManager>();
    }

    void Start()
    {
        if (networkManager != null)
        {
            networkManager.RegisterReplicationServer(this);
        }
    }

    void Update()
    {
        if (networkManager == null || !networkManager.isHost || !networkManager.isConnected)
            return;

        if (Time.time - lastReplicationTime >= replicationInterval)
        {
            BroadcastReplicationPackets();
            lastReplicationTime = Time.time;
        }
    }

    public void RegisterObject(string networkId, ReplicatedObjectType objectType)
    {
        if (!registeredObjects.ContainsKey(networkId))
        {
            registeredObjects[networkId] = objectType;
            
            QueueCommand(ReplicationCommand.Create, networkId, objectType);
        }
    }

    public void UnregisterObject(string networkId)
    {
        if (registeredObjects.ContainsKey(networkId))
        {
            ReplicatedObjectType objectType = registeredObjects[networkId];
            registeredObjects.Remove(networkId);
            
            QueueCommand(ReplicationCommand.Destroy, networkId, objectType);
        }
    }

    public void MarkDirty(string networkId)
    {
        if (registeredObjects.ContainsKey(networkId))
        {
            QueueCommand(ReplicationCommand.Update, networkId, registeredObjects[networkId]);
        }
    }

    private void QueueCommand(ReplicationCommand command, string networkId, ReplicatedObjectType objectType)
    {
        NetworkObject netObj = networkObjectManager?.GetNetworkObject(networkId);
        
        ReplicationPacket packet = new ReplicationPacket
        {
            command = command,
            networkId = networkId,
            objectType = objectType,
            position = netObj != null ? netObj.transform.position : Vector3.zero,
            rotation = netObj != null ? netObj.transform.rotation : Quaternion.identity,
            velocity = Vector3.zero
        };

        if (netObj != null)
        {
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                packet.velocity = rb.linearVelocity;
            }
        }

        pendingPackets.Add(packet);
    }

    private void BroadcastReplicationPackets()
    {
        if (pendingPackets.Count == 0)
            return;

        byte[] data = NetworkProtocolBinary.SerializeReplicationPackets(pendingPackets);
        networkManager.SendReplicationData(data);
        
        pendingPackets.Clear();
    }

    public Dictionary<string, ReplicatedObjectType> GetRegisteredObjects()
    {
        return new Dictionary<string, ReplicatedObjectType>(registeredObjects);
    }

    public int GetRegisteredObjectCount()
    {
        return registeredObjects.Count;
    }
}
