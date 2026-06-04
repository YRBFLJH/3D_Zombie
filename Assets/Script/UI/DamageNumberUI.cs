using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DamageNumberUI : MonoBehaviour
{
    public static DamageNumberUI instance;

    public GameObject damageTextPrefab;
    public int poolSize = 20;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        instance = this;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(damageTextPrefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public void ShowDamage(Vector3 worldPos, float damage)
    {
        if (pool.Count == 0) return;

        GameObject obj = pool.Dequeue();
        obj.SetActive(true);

        // 世界坐标转屏幕坐标
        Vector3 screenPos = Camera.main != null
            ? Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 1.5f + Random.insideUnitSphere * 0.3f)
            : worldPos;

        obj.transform.position = screenPos;

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = Mathf.RoundToInt(damage).ToString();
            text.color = Color.red;
        }

        StartCoroutine(AnimateAndReturn(obj));
    }

    IEnumerator AnimateAndReturn(GameObject obj)
    {
        float duration = 0.8f;
        float elapsed = 0f;
        Vector3 startPos = obj.transform.position;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            obj.transform.position = startPos + Vector3.up * (t * 50f);
            if (text != null)
            {
                Color c = text.color;
                c.a = 1f - t;
                text.color = c;
            }
            yield return null;
        }

        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
