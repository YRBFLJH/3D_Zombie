using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 物品在背包的数据(每个此数据代表背包中的一个物品堆)
public class InventoryItem
{
    public ItemData item;
    public int amount;
    public int x, y;
    public bool isRotated;  // 是否旋转（占用格子形状改变）
    public Slot[,] parentGrid;

    // 根据旋转状态获取物品的宽高
    public int Width => isRotated ? item.height : item.width;
    public int Height => isRotated ? item.width : item.height;

    public InventoryItem(ItemData data, int amount, int x, int y, Slot[,] grid, bool rotated = false)
    {
        item = data;
        this.amount = amount;
        this.x = x;
        this.y = y;
        parentGrid = grid;
        isRotated = rotated;
    }
}
