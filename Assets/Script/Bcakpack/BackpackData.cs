using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Backpack", menuName = "CreateAssetMenu/BackpackData")]
public class BackpackData : ItemData
{
    public float maxWeight; // 背包最大承重

    // 背包格子配置
    public Transform LargeArea;
    public int SlotInLarge;
    public Transform MiddleArea;
    public int SlotInMiddle;
    public Transform SmallArea;
    public int SlotInSmall;
}
