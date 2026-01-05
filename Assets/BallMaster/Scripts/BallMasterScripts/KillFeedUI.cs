using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class KillFeedUI : MonoBehaviour
{
    [Header("Settings")]
    public GameObject killFeedEntryPrefab;
    public Transform killFeedContainer;
    public int maxEntries = 5;
    public float entryDuration = 4f;

    private List<GameObject> activeEntries = new List<GameObject>();

    void Start()
    {
        // Initialization if needed
    }

    public void AddKillEntry(string killerName, string victimName)
    {
        GameObject entry = Instantiate(killFeedEntryPrefab, killFeedContainer);
        entry.SetActive(true);

        TextMeshProUGUI text = entry.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"{killerName} eliminó a {victimName}";
        }

        activeEntries.Add(entry);

        if (activeEntries.Count > maxEntries)
        {
            GameObject oldest = activeEntries[0];
            activeEntries.RemoveAt(0);
            Destroy(oldest);
        }

        StartCoroutine(RemoveEntryAfterDelay(entry, entryDuration));
    }

    private IEnumerator RemoveEntryAfterDelay(GameObject entry, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (entry != null && activeEntries.Contains(entry))
        {
            activeEntries.Remove(entry);
            Destroy(entry);
        }
    }
}
