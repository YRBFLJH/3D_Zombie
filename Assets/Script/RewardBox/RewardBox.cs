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

    Player player;

    public ItemData[] rewards;


    void Awake()
    {
        imageTime = openTime.GetComponent<Image>();
        textTime = openTime.GetComponentInChildren<TextMeshProUGUI>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        var pg = other.gameObject.GetComponent<Player_Getcomponent>();
        if (pg == null || pg.playerCS == null) return;

        player = pg.playerCS;
        if (!player.isLocalPlayer) return;

        if (InteracButtonManager.Instance != null)
            InteracButtonManager.Instance.SpawnInteractButton(gameObject, boxString);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        var pg = other.gameObject.GetComponent<Player_Getcomponent>();
        if (pg == null || pg.playerCS == null) return;
        if (!pg.playerCS.isLocalPlayer) return;

        // 离开范围时取消正在进行的开箱
        if (isOpening)
            StopOpen();

        if (InteracButtonManager.Instance != null)
            InteracButtonManager.Instance.DestroyInteractButton(gameObject);
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
        // 防止重复开启：正在开箱中 或 箱子UI已经打开
        if (isOpening) return;
        if (BackpackManage.currentOpenChest != null) return;

        isOpening = true;
        currentTime = needOpenTime;
        openTime.SetActive(true);
        openCoroutine = StartCoroutine(OpenBox());
    }

    void StopOpen()
    {
        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        isOpening = false;
        openTime.SetActive(false);
    }

    void FinishOpen()
    {
        Debug.Log("打开箱子");
        StopOpen();

        ChestInventory inv = GetComponent<ChestInventory>();

        // 本地先生成物品（确保离线/联机都有东西）
        if (inv.items.Count == 0)
            inv.InitRandomItems();

        GetComponent<ChestUI>().OpenChest();

        // 联机：将本地生成的物品提交至服务器（服务器广播给其他客户端）
        if (NetworkManager.instance != null && NetworkManager.instance.playerId >= 0)
            NetworkManager.instance.SendChestStateSubmit(inv.chestId, inv.items);
    }
}
