using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NetworkManager networkManager;
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
                     // Determine if we need to send the join request now that the scene is loaded
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
            
            string newPlayerId = "Player_" + clientId;
            networkManager.SendPlayerIdToClient(clientId, newPlayerId);
            
            SpawnPlayer(newPlayerId, false); 
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
}
