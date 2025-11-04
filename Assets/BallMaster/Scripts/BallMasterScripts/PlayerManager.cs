using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    private Dictionary<string, PlayerController> players = new Dictionary<string, PlayerController>();
    private int nextPlayerId = 0;
    private string myPlayerId = "";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.isHost)
        {
            myPlayerId = "player_0";
            SpawnPlayer(myPlayerId, true);
        }
    }

    public void SpawnPlayer(string playerId, bool isLocal)
    {
        if (players.ContainsKey(playerId))
        {
            Debug.LogWarning($"{playerId} ya existe, hay un doppleganger!");
            return;
        }

        Vector3 spawnPos = spawnPoints.Length > 0 
            ? spawnPoints[players.Count % spawnPoints.Length].position 
            : Vector3.zero;

        GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        netObj.objectId = playerId;

        PlayerController controller = playerObj.GetComponent<PlayerController>();
        
        if (isLocal)
        {
            controller.SetAsLocalPlayer();
            myPlayerId = playerId;
        }
        else
        {
            controller.SetAsRemotePlayer();
        }

        players[playerId] = controller;
        
        if (NetworkObjectManager.Instance != null)
        {
            NetworkObjectManager.Instance.RegisterNetworkObject(netObj);
        }
    }

    public void HandleClientJoined(string clientId)
    {
        if (!NetworkManager.Instance.isHost) return;

        nextPlayerId++;
        string playerId = "player_" + nextPlayerId;
        
        ExistingPlayersData existingPlayers = new ExistingPlayersData();
        foreach (var kvp in players)
        {
            existingPlayers.players.Add(new ExistingPlayerData
            {
                playerId = kvp.Key,
                position = kvp.Value.transform.position,
                rotation = kvp.Value.transform.rotation
            });
        }
        
        if (existingPlayers.players.Count > 0)
        {
            NetworkManager.Instance.SendExistingPlayersToClient(clientId, existingPlayers);
        }
        
        SpawnPlayer(playerId, false);
        
        NetworkManager.Instance.SendPlayerIdToClient(clientId, playerId);
    }

    public void ReceiveMyPlayerId(string playerId)
    {
        myPlayerId = playerId;
        SpawnPlayer(playerId, true);
    }

    public void SpawnExistingPlayers(ExistingPlayersData playersData)
    {
        foreach (var playerData in playersData.players)
        {
            if (!players.ContainsKey(playerData.playerId))
            {
                GameObject playerObj = Instantiate(playerPrefab, playerData.position, playerData.rotation);
                NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
                netObj.objectId = playerData.playerId;

                PlayerController controller = playerObj.GetComponent<PlayerController>();
                controller.SetAsRemotePlayer();
                
                players[playerData.playerId] = controller;
                
                if (NetworkObjectManager.Instance != null)
                {
                    NetworkObjectManager.Instance.RegisterNetworkObject(netObj);
                }
            }
        }
    }

    public Dictionary<string, PlayerController> GetAllPlayers()
    {
        return players;
    }
}