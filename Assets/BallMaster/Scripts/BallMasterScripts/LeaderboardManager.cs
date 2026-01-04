using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    private Dictionary<string, PlayerStats> playerStats = new Dictionary<string, PlayerStats>();

    private static readonly string[] randomNames = new string[]
    {
        "Shadow", "Phoenix", "Thunder", "Blaze", "Frost",
        "Storm", "Viper", "Ghost", "Raven", "Wolf",
        "Dragon", "Titan", "Nova", "Ace", "Hawk",
        "Ninja", "Rocket", "Turbo", "Flash", "Bolt"
    };

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

    public void RegisterPlayer(string playerId)
    {
        if (!playerStats.ContainsKey(playerId))
        {
            string randomName = GenerateRandomName();
            playerStats[playerId] = new PlayerStats(playerId, randomName);
        }
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

        if (KillFeedUI.Instance != null)
        {
            string killerName = playerStats[killerId].playerName;
            string victimName = playerStats[victimId].playerName;
            KillFeedUI.Instance.AddKillEntry(killerName, victimName);
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

        if (KillFeedUI.Instance != null)
        {
            KillFeedUI.Instance.AddKillEntry(data.killerName, data.victimName);
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

    private string GenerateRandomName()
    {
        string baseName = randomNames[Random.Range(0, randomNames.Length)];
        int number = Random.Range(100, 999);
        return $"{baseName}_{number}";
    }
}
