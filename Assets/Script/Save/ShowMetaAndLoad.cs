using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;

public class ShowMetaAndLoad : MonoBehaviour
{
    Player localPlayer;

    [Header("组件")]
    public TMP_Text textId;
    public TMP_Text textLastSaveTime;
    public TMP_Text textAllPlayTime;
    private Button loadButton;

    [Header("数据")]
    string saveId;
    DateTime lastSaveTime;
    float playTime;

    void Awake() 
    {
        loadButton = GetComponent<Button>();
        SaveManager.instance.sceneName = SceneManager.GetActiveScene().name;
    }

    void Start() 
    {
        localPlayer = NetworkClient.localPlayer.GetComponent<Player>();
        loadButton.onClick.AddListener(Load);
    }

    void OnEnable() 
    {
        GetMeta();
        textId.text = "存档: " + saveId;
        textLastSaveTime.text = "上次保存时间: " + lastSaveTime;
        textAllPlayTime.text = "游玩时长: " + playTime;
    }

    void GetMeta()
    {
        Debug.Log($"找到 {SaveManager.instance.GetAllSaveList().Count} 个存档");
        SaveManager.instance.GetAllSaveList().ForEach(meta =>
        {
            Debug.Log($"存档: ID={meta.saveId}, 场景={meta.sceneName}, 当前场景={SaveManager.instance.sceneName}");
            if (meta.sceneName == SaveManager.instance.sceneName)
            {
                saveId = meta.saveId;
                lastSaveTime = meta.lastPlayTime;
                playTime = meta.playDuration;
            }
        });
    }

    void Load()
    {
        SaveManager.instance.LoadGameAsync(saveId, data =>
        {
            if (data != null)
            {
                // 玩家数据
                localPlayer.Teleportation(data.player.position); // 玩家位置

                // 背包数据
                BackpackManage.Instance.LoadInventory(data.inventory);
                Debug.Log($"加载的背包物品数量: {data.inventory?.items.Count ?? 0}");

                Debug.Log("加载成功");
            }
            else
            {
                Debug.Log("加载失败");
            }
        });
    }
}
