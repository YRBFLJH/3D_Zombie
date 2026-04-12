using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardBox : MonoBehaviour
{
    Image imageTime;
    TextMeshProUGUI textTime;

    public string boxString;
    public GameObject openTime;
    public float needOpenTime = 5f;
    float currentTime;
    bool isOpening = false;
    Coroutine openCoroutine;

    public ItemData[] rewards;

    void Awake()
    {
        imageTime = openTime.GetComponent<Image>();
        textTime = openTime.GetComponentInChildren<TextMeshProUGUI>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            InteracButtonManager.Intance.SpawnInteractButton(gameObject ,boxString);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            InteracButtonManager.Intance.DestroyInteractButton(gameObject);
        }
    }

    IEnumerator OpenBox()
    {
        while (currentTime > 0)
        {
            currentTime -= Time.deltaTime;

            imageTime.fillAmount = currentTime / needOpenTime;
            textTime.text = currentTime.ToString("0.0");

            yield return null;
        }

        FinishOpen();
    }

    public void StartOpen()
    {
        if (isOpening) return;

        isOpening = true;
        currentTime = needOpenTime;
        openTime.SetActive(true);
        openCoroutine = StartCoroutine(OpenBox());
    }

    void StopOpen()
    {
        StopCoroutine(openCoroutine);

        isOpening = false;
        openTime.SetActive(false);
    }

    void FinishOpen()
    {
        Debug.Log("打开箱子");
        StopOpen();

        // 打开箱子逻辑
        GetComponent<ChestUI>().OpenChest();
        GetComponent<ChestInventory>().InitRandomItems();
    }
}
