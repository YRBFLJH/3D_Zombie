using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SaveButton : MonoBehaviour
{
    Button button;

    SaveGameData allSaveData;
    PlayerSaveData playerSaveData;

    void Start()
    {
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
        position = new Vector3Data(Player.instance.transform.position.x, Player.instance.transform.position.y, Player.instance.transform.position.z),
        playerName = Player.instance.playerName,
        level = Player.instance.level,
        gold = Player.instance.gold,
        health = Player.instance.health,
        maxHealth = Player.instance.maxHealth,
        experience = Player.instance.experience,
        maxExperience = Player.instance.maxExperience,
        speed = Player.instance.speed,
        };
        allSaveData.player = playerSaveData; 



        // 背包数据
        allSaveData.inventory = BackpackManage.Instance.GetSaveData();
        Debug.Log($"保存时背包物品数量: {BackpackManage.Instance.GetSaveData().items.Count}");
    }

}
