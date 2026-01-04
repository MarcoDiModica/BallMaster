using System.Collections.Generic;
using UnityEngine;

public class LagCompensationManager : MonoBehaviour
{
    public static LagCompensationManager Instance { get; private set; }

    private NetworkManager networkManager;
    private NetworkObjectManager networkObjectManager;
    private ReplicationManagerServer replicationServer;

    private float serverStartTime;

    public float ServerTime => Time.time - serverStartTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        networkManager = FindFirstObjectByType<NetworkManager>();
        networkObjectManager = FindFirstObjectByType<NetworkObjectManager>();
        replicationServer = FindFirstObjectByType<ReplicationManagerServer>();
        serverStartTime = Time.time;
    }

    void LateUpdate()
    {
        if (networkManager == null || !networkManager.isHost)
            return;

        RecordAllPlayerHistory();
    }

    void RecordAllPlayerHistory()
    {
        if (networkObjectManager == null)
            return;

        float currentServerTime = ServerTime;

        foreach (var netObj in networkObjectManager.GetAllNetworkObjects())
        {
            if (netObj != null && netObj.objectId.StartsWith("Player_"))
            {
                netObj.RecordHistory(currentServerTime);
            }
        }
    }

    public bool CheckHitWithCompensation(Vector3 ballPosition, float ballRadius, string shooterPlayerId, int shooterPingMs, out string hitPlayerId)
    {
        hitPlayerId = null;

        if (networkObjectManager == null)
            return false;

        float compensationTime = shooterPingMs / 1000f;
        float targetTime = ServerTime - compensationTime;

        foreach (var netObj in networkObjectManager.GetAllNetworkObjects())
        {
            if (netObj == null || !netObj.objectId.StartsWith("Player_"))
                continue;

            if (netObj.objectId == shooterPlayerId)
                continue;

            Vector3 historicalPosition = netObj.GetPositionAtTime(targetTime);

            float playerRadius = 0.5f;
            float distance = Vector3.Distance(ballPosition, historicalPosition);

            if (distance <= ballRadius + playerRadius)
            {
                hitPlayerId = netObj.objectId;
                return true;
            }
        }

        return false;
    }

    public Vector3 GetPlayerPositionAtTime(string playerId, float serverTime)
    {
        if (networkObjectManager == null)
            return Vector3.zero;

        NetworkObject netObj = networkObjectManager.GetNetworkObject(playerId);
        if (netObj == null)
            return Vector3.zero;

        return netObj.GetPositionAtTime(serverTime);
    }

    public float GetCompensatedTime(string clientId)
    {
        if (networkManager == null)
            return ServerTime;

        int pingMs = networkManager.GetClientPing(clientId);
        float halfRtt = (pingMs / 1000f) / 2f;
        return ServerTime - halfRtt;
    }
}
