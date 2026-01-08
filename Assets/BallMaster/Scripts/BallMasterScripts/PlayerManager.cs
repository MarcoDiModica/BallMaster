using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NetworkManager networkManager;
    public NetworkManager NetworkManager => networkManager;
    [SerializeField] private NetworkObjectManager networkObjectManager;

    private ReplicationManagerServer replicationServer;
    private LeaderboardManager leaderboardManager;

    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    private Dictionary<string, PlayerController> players = new Dictionary<string, PlayerController>();
    private string myPlayerId;

    void Start()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();

        if (networkManager != null)
        {
            networkManager.RegisterPlayerManager(this);
            replicationServer = FindFirstObjectByType<ReplicationManagerServer>(); 
            leaderboardManager = FindFirstObjectByType<LeaderboardManager>(); 

            if (networkManager.isHost)
            {
                SpawnPlayer("Host", true);
            }
            else if (networkManager.isConnected)
            {
                 networkManager.SendPendingJoinRequest();
                 
                 string pendingId = networkManager.GetPendingPlayerId();
                 if(!string.IsNullOrEmpty(pendingId))
                 {
                     ReceiveMyPlayerId(pendingId);
                 }
            }
        }
    }

    public void HandleClientJoined(string clientId)
    {
        if (networkManager != null && networkManager.isHost)
        {
            string newPlayerId = "Player_" + clientId;
            networkManager.SendPlayerIdToClient(clientId, newPlayerId);
            
            SpawnPlayer(newPlayerId, false);
        }
    }

    public void ReceiveMyPlayerId(string playerId)
    {
        myPlayerId = playerId;
    }

    public void SpawnPlayer(string playerId, bool isLocal)
    {
        if (players.ContainsKey(playerId)) return;

        int spawnIndex = Random.Range(0, spawnPoints.Length);
        Vector3 spawnPos = spawnPoints[spawnIndex].position;

        GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        
        PlayerController controller = playerObj.GetComponent<PlayerController>();
        if (controller != null) controller.Initialize(this); 

        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj == null) netObj = playerObj.AddComponent<NetworkObject>();
        
        netObj.objectId = playerId;

        if (networkObjectManager != null)
             networkObjectManager.RegisterNetworkObject(netObj);
        
        PlayerNetworkComponent netComp = playerObj.GetComponent<PlayerNetworkComponent>();
        if (netComp != null) netComp.Initialize(isLocal);

        players[playerId] = controller;

        if (leaderboardManager != null && networkManager.isHost)
        {
            string name = leaderboardManager.RegisterPlayer(playerId);
            networkManager.BroadcastPlayerNameSync(playerId, name);
        }
        
        if (networkManager.isHost && replicationServer != null)
        {
            replicationServer.RegisterObject(playerId, ReplicatedObjectType.Player);
        }
    }

    public void HandleClientDisconnected(string clientId)
    {
        string playerIdToRemove = "Player_" + clientId;
        if (players.ContainsKey(playerIdToRemove))
        {
            PlayerController player = players[playerIdToRemove];
            players.Remove(playerIdToRemove);
            
            if (player != null)
            {
                if (replicationServer != null && networkManager.isHost)
                {
                    replicationServer.UnregisterObject(playerIdToRemove); // Sends Destroy packet
                }
                
                Destroy(player.gameObject);
                
                if (networkObjectManager != null)
                    networkObjectManager.UnregisterNetworkObject(playerIdToRemove);
            }
        }
    }
    
    public void ResetState()
    {
        foreach (var kvp in players)
        {
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        }
        players.Clear();
        myPlayerId = null;
    }
    
    public bool IsMyPlayer(string id)
    {
        return id == myPlayerId;
    }
}
