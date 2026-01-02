using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class LeaderboardUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject leaderboardPanel;

    [Header("Row Template")]
    public GameObject rowPrefab;
    public Transform rowContainer;

    [Header("Header Texts")]
    public TextMeshProUGUI headerNameText;
    public TextMeshProUGUI headerKillsText;
    public TextMeshProUGUI headerDeathsText;

    private List<GameObject> spawnedRows = new List<GameObject>();

    void Start()
    {
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);
    }

    void Update()
    {
        bool tabPressed = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
        bool selectPressed = Gamepad.current != null && Gamepad.current.selectButton.isPressed;

        bool shouldShow = tabPressed || selectPressed;

        if (leaderboardPanel != null && leaderboardPanel.activeSelf != shouldShow)
        {
            leaderboardPanel.SetActive(shouldShow);

            if (shouldShow)
            {
                RefreshLeaderboard();
            }
        }
    }

    void RefreshLeaderboard()
    {
        foreach (var row in spawnedRows)
        {
            Destroy(row);
        }
        spawnedRows.Clear();

        if (LeaderboardManager.Instance == null)
            return;

        List<PlayerStats> allStats = LeaderboardManager.Instance.GetAllStatsSorted();

        for (int i = 0; i < allStats.Count; i++)
        {
            PlayerStats stats = allStats[i];
            GameObject row = Instantiate(rowPrefab, rowContainer);
            row.SetActive(true);

            TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();

            if (texts.Length >= 3)
            {
                texts[0].text = stats.playerName;
                texts[1].text = stats.kills.ToString();
                texts[2].text = stats.deaths.ToString();
            }

            spawnedRows.Add(row);
        }
    }
}
