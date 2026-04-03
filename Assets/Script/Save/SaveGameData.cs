using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.VisualScripting;

    // 保存游戏数据（中介点）


[Serializable] // 标识该类可序列化
public class SaveGameData   
{
    public string saveId;  // 保存ID
    public DateTime saveTime;   // 保存时间
    public string sceneName;    // 当前场景名称

    public PlayerSaveData player; // 玩家数据
    public InventorySaveData inventory; // 背包数据
    public List<QuestSaveData> quests;  // 任务数据

    // 提前初始化，防止空值报错
    public SaveGameData()
    {
        player = new PlayerSaveData();
        inventory = new InventorySaveData();
        quests = new List<QuestSaveData>();
    }
}


[Serializable]
public class PlayerSaveData // 玩家数据
{
    public String playerName;
    public float speed,health,maxHealth,satiety,maxSatiety,thirst,maxThirst;
    public int level,gold;
    public Vector3Data position; //Vector3不能直接序列号，需自定义
}


[Serializable]
public class InventorySaveData // 背包数据
{
    public List<ItemsSaveData> items = new List<ItemsSaveData>();
}
[Serializable]
public class ItemsSaveData // 背包物品数据
{
    public int itemId;
    public int amount;
    public string gridType; // 所处的大、中、小区域
    public int gridIndex; // 所在区域的索引（例如有2个小区域，要知道是这2个中的哪一个）
    public int x, y; // 所在格子的左上角格子坐标(不同尺寸物品，故设置以左上角格子为标记)
    public bool isRotated; // 物品是否旋转
}


[Serializable]
public class QuestSaveData // 任务数据
{
    public int questId;
    public int state;
}


[Serializable]
public class Vector3Data // Vector3不能直接序列号，需自定义
{
    public float x,y,z;

    public Vector3Data(float x,float y,float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static implicit operator UnityEngine.Vector3(Vector3Data v) => new UnityEngine.Vector3(v.x, v.y, v.z);
    public static implicit operator Vector3Data(UnityEngine.Vector3 v) => new Vector3Data(v.x, v.y, v.z);
}