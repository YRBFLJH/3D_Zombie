using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class KillFeedUI : MonoBehaviour
{
    public static KillFeedUI instance;

    public GameObject killFeedEntryPrefab;
    public Transform killFeedContainer;
    public int maxEntries = 5;

    private List<GameObject> entries = new List<GameObject>();

    void Awake()
    {
        instance = this;
    }

    public void AddKillMessage(string message)
    {
        if (killFeedEntryPrefab == null || killFeedContainer == null) return;

        GameObject entry = Instantiate(killFeedEntryPrefab, killFeedContainer);
        TextMeshProUGUI text = entry.GetComponent<TextMeshProUGUI>();
        if (text != null) text.text = message;

        entries.Add(entry);

        if (entries.Count > maxEntries)
        {
            Destroy(entries[0]);
            entries.RemoveAt(0);
        }

        StartCoroutine(FadeOut(entry, 5f));
    }

    IEnumerator FadeOut(GameObject entry, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (entry != null)
        {
            entries.Remove(entry);
            Destroy(entry);
        }
    }
}
