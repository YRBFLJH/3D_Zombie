using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Backpack", menuName = "CreateAssetMenu/BackpackData")]
public class BackpackData : ItemData
{

    [Header("格子区域配置")]
    public float maxWeight; // 背包最大承重

    // 背包格子配置(每个区域里的格子数量、区域几行几列)
    public int slotsInLarge;
    public int largeWidth;
    public int largeHeight;

    public int slotsInMiddle;
    public int middleWidth;
    public int middleHeight;

    public int slotsInSmall;
    public int smallWidth;
    public int smallHeight;

}
