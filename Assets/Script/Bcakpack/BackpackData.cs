using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Backpack", menuName = "CreateAssetMenu/BackpackData")]
public class BackpackData : ItemData
{

    [Header("格子区域配置")]
    public float maxWeight; // 背包最大承重

    // 各种区域的数量
    public int smallCount;
    public int middleCount;
    public int largeCount;

    // 背包格子配置(每个区域几行几列)
    public int largeWidth;
    public int largeHeight;

    public int middleWidth;
    public int middleHeight;

    public int smallWidth;
    public int smallHeight;

}
