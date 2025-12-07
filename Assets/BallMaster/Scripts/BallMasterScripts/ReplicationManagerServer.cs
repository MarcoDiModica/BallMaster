using System;
using System.Collections.Generic;
using UnityEngine;

public enum ReplicationCommand : byte
{
    Create = 0,
    Update = 1,
    Destroy = 2,
}

public enum ReplicatedObjectType : byte
{
    Player = 0,
    Ball = 1,
    NetworkObject = 2,
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
    public float replicationInterval = 0.033f; //30hz

    private NetworkManager networkManager;
    private NetworkObjectManager networkObjectManager;

    private Dictionary<string, ReplicatedObjectType> registeredObjects =
        new Dictionary<string, ReplicatedObjectType>();

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

    void LateUpdate()
    {
        if (networkManager == null || !networkManager.isHost || !networkManager.isConnected)
            return;

        if (networkObjectManager != null)
        {
            foreach (var netObj in networkObjectManager.GetAllNetworkObjects())
            {
                if (netObj.isDirty && registeredObjects.ContainsKey(netObj.objectId))
                {
                    MarkDirty(netObj.objectId);
                    netObj.isDirty = false;
                }
            }
        }

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
            Debug.Log($"[Server] Registered Object {networkId} as {objectType}");
        }
    }

    public void UnregisterObject(string networkId)
    {
        if (registeredObjects.ContainsKey(networkId))
        {
            ReplicatedObjectType objectType = registeredObjects[networkId];
            registeredObjects.Remove(networkId);
            QueueCommand(ReplicationCommand.Destroy, networkId, objectType);
            Debug.Log($"[Server] Unregistered Object {networkId}");
        }
    }

    public void MarkDirty(string networkId)
    {
        if (registeredObjects.ContainsKey(networkId))
        {
            QueueCommand(ReplicationCommand.Update, networkId, registeredObjects[networkId]);
        }
    }

    public void SendInitialStateToClient(string clientId)
    {
        List<ReplicationPacket> initialPackets = new List<ReplicationPacket>();

        foreach (var kvp in registeredObjects)
        {
            string id = kvp.Key;
            ReplicatedObjectType type = kvp.Value;

            ReplicationPacket packet = CreatePacket(ReplicationCommand.Create, id, type);
            if (packet != null)
                initialPackets.Add(packet);
        }

        if (initialPackets.Count > 0)
        {
            byte[] data = NetworkProtocolBinary.SerializeReplicationPackets(initialPackets);
            networkManager.SendReplicationDataToClient(clientId, data);
            Debug.Log(
                $"[Server] Sent initial state with {initialPackets.Count} objects to {clientId}"
            );
        }
    }

    private void QueueCommand(
        ReplicationCommand command,
        string networkId,
        ReplicatedObjectType objectType
    )
    {
        ReplicationPacket packet = CreatePacket(command, networkId, objectType);
        if (packet != null)
        {
            pendingPackets.Add(packet);
        }
    }

    private ReplicationPacket CreatePacket(
        ReplicationCommand command,
        string networkId,
        ReplicatedObjectType objectType
    )
    {
        NetworkObject netObj = networkObjectManager?.GetNetworkObject(networkId);

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        Vector3 vel = Vector3.zero;

        if (netObj != null)
        {
            pos = netObj.transform.position;
            rot = netObj.transform.rotation;

            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                vel = rb.linearVelocity;
            }
        }
        else if (command != ReplicationCommand.Destroy)
        {
            return null;
        }

        return new ReplicationPacket
        {
            command = command,
            networkId = networkId,
            objectType = objectType,
            position = pos,
            rotation = rot,
            velocity = vel,
        };
    }

    private void BroadcastReplicationPackets()
    {
        if (pendingPackets.Count == 0)
            return;

        byte[] data = NetworkProtocolBinary.SerializeReplicationPackets(pendingPackets);
        networkManager.SendReplicationData(data);

        pendingPackets.Clear();
    }
}
