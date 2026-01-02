using System;

[Serializable]
public class PlayerStats
{
    public string playerId;
    public string playerName;
    public int kills;
    public int deaths;

    public float KDRatio => deaths == 0 ? kills : (float)kills / deaths;
    public int Score => kills - deaths;

    public PlayerStats(string id, string name)
    {
        playerId = id;
        playerName = name;
        kills = 0;
        deaths = 0;
    }

    public void AddKill()
    {
        kills++;
    }

    public void AddDeath()
    {
        deaths++;
    }
}
