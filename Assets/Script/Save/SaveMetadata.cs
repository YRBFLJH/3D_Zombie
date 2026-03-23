using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]

//  元数据（在存档界面展示的信息）
public class SaveMetadata
{
    public string saveId;
    public DateTime lastPlayTime;
    public string sceneName;        // 保存时所在的场景名称
    public int playerLevel;
    public float playDuration;      // 总游玩时长（秒）
}