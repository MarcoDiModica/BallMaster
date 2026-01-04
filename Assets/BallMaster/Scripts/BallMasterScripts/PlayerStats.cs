using System;

[Serializable]
public class PlayerStats
{
    public string playerId;
    public string playerName;
    public int kills;
    public int deaths;
    public int pingMs;

    public float KDRatio => deaths == 0 ? kills : (float)kills / deaths;
    public int Score => kills - deaths;

    public PlayerStats(string id, string name)
    {
        playerId = id;
        playerName = name;
        kills = 0;
        deaths = 0;
        pingMs = 0;
    }

    public void AddKill()
    {
        kills++;
    }

    public void AddDeath()
    {
        deaths++;
    }

    public void UpdatePing(int ms)
    {
        pingMs = ms;
    }
}
