using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    private Dictionary<string, PlayerStats> playerStats = new Dictionary<string, PlayerStats>();
    private KillFeedUI killFeedUI;

    private static readonly string[] randomNames = new string[]
    {
        "Patricio Estrella", "Bob Esponja", "Calamardo Tentáculos", "Don Cangrejo", "Arenita Mejillas",
        "Sheldon J. Plankton", "Homero Simpson", "Marge Simpson", "Bart Simpson", "Lisa Simpson",
        "Ned Flanders", "Montgomery Burns", "Mickey Mouse", "Pato Donald", "Goofy",
        "Minnie Mouse", "Bugs Bunny", "Pato Lucas", "Piolín", "Silvestre",
        "Goku", "Vegeta", "Piccolo", "Naruto Uzumaki", "Sasuke Uchiha",
        "Monkey D. Luffy", "Roronoa Zoro", "Scooby Doo", "Shaggy Rogers", 
        "Pedro Picapiedra", "Pablo Mármol", "Tom", "Jerry", "Rick Sánchez", 
        "Morty Smith", "Peter Griffin", "Stewie Griffin", "Brian Griffin"
    };

    void Awake()
    {
        killFeedUI = FindFirstObjectByType<KillFeedUI>();
    }

    public string RegisterPlayer(string playerId)
    {
        if (!playerStats.ContainsKey(playerId))
        {
            string randomName = GenerateRandomName();
            playerStats[playerId] = new PlayerStats(playerId, randomName);
            return randomName;
        }
        return playerStats[playerId].playerName;
    }

    public void RegisterPlayerWithName(string playerId, string playerName, int kills = 0, int deaths = 0)
    {
        if (!playerStats.ContainsKey(playerId))
        {
            playerStats[playerId] = new PlayerStats(playerId, playerName);
        }
        else
        {
             playerStats[playerId].playerName = playerName;
        }
        playerStats[playerId].kills = kills;
        playerStats[playerId].deaths = deaths;
    }

    public void UnregisterPlayer(string playerId)
    {
        if (playerStats.ContainsKey(playerId))
        {
            playerStats.Remove(playerId);
        }
    }

    public void RegisterKill(string killerId, string victimId)
    {
        if (!playerStats.ContainsKey(killerId))
            RegisterPlayer(killerId);

        if (!playerStats.ContainsKey(victimId))
            RegisterPlayer(victimId);

        playerStats[killerId].AddKill();
        playerStats[victimId].AddDeath();

        if (killFeedUI != null)
        {
            string killerName = playerStats[killerId].playerName;
            string victimName = playerStats[victimId].playerName;
            killFeedUI.AddKillEntry(killerName, victimName);
        }
    }

    public PlayerStats GetPlayerStats(string playerId)
    {
        return playerStats.ContainsKey(playerId) ? playerStats[playerId] : null;
    }

    public List<PlayerStats> GetAllStatsSorted()
    {
        return playerStats.Values
            .OrderByDescending(p => p.kills)
            .ThenBy(p => p.deaths)
            .ToList();
    }

    public void RegisterKillFromNetwork(KillEventData data)
    {
        EnsurePlayerExists(data.killerId, data.killerName);
        EnsurePlayerExists(data.victimId, data.victimName);

        playerStats[data.killerId].AddKill();
        playerStats[data.victimId].AddDeath();

        if (killFeedUI != null)
        {
            killFeedUI.AddKillEntry(data.killerName, data.victimName);
        }
    }

    private void EnsurePlayerExists(string playerId, string playerName)
    {
        if (!playerStats.ContainsKey(playerId))
        {
            playerStats[playerId] = new PlayerStats(playerId, playerName);
        }
    }

    public int GetPlayerCount()
    {
        return playerStats.Count;
    }
    
    public Dictionary<string, string> GetAllPlayerNames()
    {
        Dictionary<string, string> names = new Dictionary<string, string>();
        foreach(var kvp in playerStats)
        {
            names[kvp.Key] = kvp.Value.playerName;
        }
        return names;
    }

    private string GenerateRandomName()
    {
        return randomNames[Random.Range(0, randomNames.Length)];
    }
}
