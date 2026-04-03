using UnityEngine;
using System;

// 处于背包的物品的数据结构(由外部实时更改)
[System.Serializable]
public struct InventoryItem : IEquatable<InventoryItem>
{
    public int itemId; // 物品ID，用于通过Id获取物品数据
    public int amount;
    public int x, y; // 在背包的坐标，确定左上角第一个格子
    public bool isRotated;

    // 物品所在区域的类型和序号
    public string gridType;
    public int gridIndex;

    public ItemData item => BackpackManage.GetItemData(itemId);

    // 旋转时(isRotated为true)宽高互换
    public int Width => isRotated ? item.height : item.width;
    public int Height => isRotated ? item.width : item.height;

    public InventoryItem(int itemId, int amount, int x, int y, string gridType, int gridIndex, bool rotated = false)
    {
        this.itemId = itemId;
        this.amount = amount;
        this.x = x;
        this.y = y;
        this.gridType = gridType;
        this.gridIndex = gridIndex;
        isRotated = rotated;
    }

    // 让 SyncList 能正确识别、查找、比较物品（为true便能确定是同一个物品）
    public bool Equals(InventoryItem other)
    {
        return itemId == other.itemId
            && x == other.x
            && y == other.y
            && gridType == other.gridType
            && gridIndex == other.gridIndex;
    }
}