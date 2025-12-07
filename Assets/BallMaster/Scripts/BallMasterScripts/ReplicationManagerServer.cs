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
    public float timestamp;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
}

public class ReplicationManagerServer : MonoBehaviour
{
    [Header("Configuration")]
    public float replicationInterval = 0.016f;

    private NetworkManager networkManager;
    private NetworkObjectManager networkObjectManager;

    private Dictionary<string, ReplicatedObjectType> registeredObjects =
        new Dictionary<string, ReplicatedObjectType>();

    private List<ReplicationPacket> pendingPackets = new List<ReplicationPacket>();
    private float lastReplicationTime;
    private float serverStartTime;

    public float ServerTime => Time.time - serverStartTime;

    void Awake()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();
        networkObjectManager = FindFirstObjectByType<NetworkObjectManager>();
        serverStartTime = Time.time;
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

        if (Time.time - lastReplicationTime >= replicationInterval)
        {
            foreach (var kvp in registeredObjects)
            {
                string id = kvp.Key;
                ReplicatedObjectType type = kvp.Value;

                if (type == ReplicatedObjectType.Player)
                {
                    QueueCommand(ReplicationCommand.Update, id, type);
                }
                else
                {
                    NetworkObject netObj = networkObjectManager?.GetNetworkObject(id);
                    if (netObj != null && netObj.isDirty)
                    {
                        QueueCommand(ReplicationCommand.Update, id, type);
                        netObj.isDirty = false;
                    }
                }
            }

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
        string objectId,
        ReplicatedObjectType objectType
    )
    {
        NetworkObject netObj = networkObjectManager?.GetNetworkObject(objectId);

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
            networkId = objectId,
            objectType = objectType,
            timestamp = ServerTime,
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
