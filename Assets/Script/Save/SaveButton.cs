using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SaveButton : MonoBehaviour
{
    Player localPlayer;
    Player_State localPlayerState;
    Player_Move localPlayerMove;
    Button button;

    SaveGameData allSaveData;
    PlayerSaveData playerSaveData;

    void Start()
    {
        // 单机模式：通过查找标签获取本地玩家
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
            return;

        localPlayer = playerObj.GetComponent<Player>();
        localPlayerState = playerObj.GetComponent<Player_State>();
        localPlayerMove = playerObj.GetComponent<Player_Move>();

        button = GetComponent<Button>();
        button.onClick.AddListener(Save);

        allSaveData = new SaveGameData();
        playerSaveData = new PlayerSaveData();
    }

    public void Save()
    {
        button.interactable = false;
        
        SaveMessage();

        SaveManager.instance.SaveGameAsync("slot0", allSaveData, success =>
        {
            if (success)
            {
                button.interactable = true;
                Debug.Log("保存成功");
            }
            else
                Debug.LogError("保存失败");
        });
    }


    // 保存游戏数据
    void SaveMessage()
    {
        // 元数据（给存档列表预览显示）
        allSaveData.saveId = "slot0";// 存档名（应修改为动态加载）
        allSaveData.saveTime = DateTime.Now; // 保存时间
        allSaveData.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; // 场景名


        // 玩家数据
        var playerSaveData = new PlayerSaveData()
        {
        position = new Vector3Data(localPlayer.transform.position.x, localPlayer.transform.position.y, localPlayer.transform.position.z),
        playerName = localPlayer.playerName,
        level = localPlayer.level,
        gold = localPlayer.gold,
        health = localPlayerState.health,
        maxHealth = localPlayerState.maxHealth,
        satiety = localPlayerState.satiety,
        maxSatiety = localPlayerState.maxSatiety,
        thirst = localPlayerState.thirst,
        maxThirst = localPlayerState.maxThirst,
        speed = localPlayerMove.speed,
        };
        allSaveData.player = playerSaveData; 



        // 背包数据
        allSaveData.inventory = BackpackManage.Instance.GetSaveData();
    }

}