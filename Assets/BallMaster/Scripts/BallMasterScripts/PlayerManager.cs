using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NetworkManager networkManager;
    public NetworkManager NetworkManager => networkManager;
    [SerializeField] private NetworkObjectManager networkObjectManager;

    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    private Dictionary<string, PlayerController> players = new Dictionary<string, PlayerController>();
    private string myPlayerId;

    void Awake()
    {
    }

    void Start()
    {
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                networkManager.RegisterPlayerManager(this);
                
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
    }

    public void HandleClientJoined(string clientId)
    {
        if (networkManager != null && networkManager.isHost)
        {
            // Send existing players to the new client
            ExistingPlayersData data = new ExistingPlayersData();
            foreach (var kvp in players)
            {
                PlayerController pc = kvp.Value;
                data.players.Add(new ExistingPlayerData
                {
                    playerId = kvp.Key,
                    position = pc.transform.position,
                    rotation = pc.transform.rotation
                });
            }
            
            networkManager.SendExistingPlayersToClient(clientId, data);
            
            // Assign ID to new client
            string newPlayerId = "Player_" + clientId;
            networkManager.SendPlayerIdToClient(clientId, newPlayerId);
            
            // Spawn the new player on host
            SpawnPlayer(newPlayerId, false);
            
            // Broadcast new player to OTHER existing clients
            if (players.ContainsKey(newPlayerId))
            {
                PlayerController newPlayer = players[newPlayerId];
                ExistingPlayerData newPlayerData = new ExistingPlayerData
                {
                    playerId = newPlayerId,
                    position = newPlayer.transform.position,
                    rotation = newPlayer.transform.rotation
                };
                networkManager.BroadcastNewPlayerToOtherClients(clientId, newPlayerData);
            }
        }
    }

    public void ReceiveMyPlayerId(string playerId)
    {
        myPlayerId = playerId;
        SpawnPlayer(playerId, true);
    }

    public void SpawnPlayer(string playerId, bool isLocal)
    {
        if (players.ContainsKey(playerId)) return;

        int spawnIndex = Random.Range(0, spawnPoints.Length);
        Vector3 spawnPos = spawnPoints[spawnIndex].position;

        GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        
        PlayerController controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.Initialize(this); 
        }

        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.objectId = playerId;
            if (networkObjectManager != null)
            {
                networkObjectManager.RegisterNetworkObject(netObj);
            }
        }
        
        PlayerNetworkComponent netComp = playerObj.GetComponent<PlayerNetworkComponent>();
        if (netComp != null)
        {
            netComp.Initialize(isLocal);
        }

        players[playerId] = controller;
    }

    public void SpawnExistingPlayers(ExistingPlayersData playersData)
    {
        foreach (var pData in playersData.players)
        {
            if (pData.playerId == myPlayerId) continue;
            
            if (!players.ContainsKey(pData.playerId))
            {
                SpawnPlayer(pData.playerId, false);
                
                if (players.ContainsKey(pData.playerId))
                {
                    players[pData.playerId].transform.position = pData.position;
                    players[pData.playerId].transform.rotation = pData.rotation;
                }
            }
        }
    }

    public void SpawnNetworkedPlayer(ExistingPlayerData playerData)
    {
        // Don't spawn ourselves
        if (playerData.playerId == myPlayerId) return;
        
        // Don't spawn duplicates
        if (players.ContainsKey(playerData.playerId)) return;
        
        SpawnPlayer(playerData.playerId, false);
        
        if (players.ContainsKey(playerData.playerId))
        {
            players[playerData.playerId].transform.position = playerData.position;
            players[playerData.playerId].transform.rotation = playerData.rotation;
        }
    }

    public void HandleClientDisconnected(string clientId)
    {
        // Find the player ID that corresponds to this client
        string playerIdToRemove = "Player_" + clientId;
        
        if (players.ContainsKey(playerIdToRemove))
        {
            PlayerController player = players[playerIdToRemove];
            players.Remove(playerIdToRemove);
            
            if (player != null)
            {
                NetworkObject netObj = player.GetComponent<NetworkObject>();
                if (netObj != null && networkObjectManager != null)
                {
                    networkObjectManager.UnregisterNetworkObject(netObj.objectId);
                }
                
                Destroy(player.gameObject);
            }
            
            Debug.LogWarning($"Player {playerIdToRemove} removed due to client disconnect");
        }
    }

    public void ResetState()
    {
        // Destroy all existing player GameObjects
        foreach (var kvp in players)
        {
            if (kvp.Value != null)
            {
                NetworkObject netObj = kvp.Value.GetComponent<NetworkObject>();
                if (netObj != null && networkObjectManager != null)
                {
                    networkObjectManager.UnregisterNetworkObject(netObj.objectId);
                }
                
                Destroy(kvp.Value.gameObject);
            }
        }
        
        players.Clear();
        myPlayerId = null;
    }
}
