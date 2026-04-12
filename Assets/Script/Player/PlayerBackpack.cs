using System.Collections.Generic;
using UnityEngine;

public class PlayerBackpack : MonoBehaviour
{
    [Header("背包配置")]
    public BackpackData backpackData;

    // 改为普通列表
    public List<InventoryItem> items = new List<InventoryItem>();

    public event System.Action OnInventoryChanged; // 背包物品改变时调用

    void Start()
    {
        BackpackManage.Instance.InitComponent();
    }

    #region 主方法（直接实现逻辑，不再区分服务器客户端）
    public void AddItem(ItemData item, int amount) // 添加物品
    {
        if (amount <= 0) return;
        AddItemInternal(item, amount);
        BackpackManage.Instance.UpdateBackpack();
    }

    public void RemoveItem(InventoryItem item) // 移除物品(丢弃背包外)
    {
        RemoveItemInternal(item);
    }

    public void MoveItem(InventoryItem item, string newGridType, int newGridIndex, int newX, int newY, bool newRotated) // 移动物品
    {
        MoveItemInternal(item, newGridType, newGridIndex, newX, newY, newRotated);
        BackpackManage.Instance.UpdateBackpack();
    }

    public void UseItem(InventoryItem item, Player user) // 使用物品
    {
        UseItemInternal(item, user);
    }
    #endregion

    #region 实际逻辑实现方法
    private void AddItemInternal(ItemData item, int amount)
    {
        // 不可堆叠，添加n个等于新建n个同样物品
        if (!item.isStackable)
        {
            for (int i = 0; i < amount; i++)
            {
                if (FindEmptySlot(item.width, item.height, out string gridType, out int gridIndex, out int x, out int y))
                {
                    InventoryItem newItem = new InventoryItem(item.id, 1, x, y, gridType, gridIndex, false);
                    items.Add(newItem);
                    OnInventoryChanged?.Invoke();
                }
                else break;
            }
            return; // 直接结束，避免进入堆叠逻辑
        }

        //可堆叠：1. 先遍历所有已有物品，先加满未满一组（最大堆叠数）的物品格子 2.加满一组后剩下的数量再新建物品，依次循环
        for (int i = 0; i < items.Count && amount > 0; i++)
        {
            InventoryItem invItem = items[i];

            // ID相同、没满一组的格子
            if (invItem.itemId == item.id && invItem.amount < item.maxStack)
            {
                int canAdd = item.maxStack - invItem.amount; // 可加数量  物品最大堆叠数 - 已有数量

                if (canAdd > 0)
                {
                    int add = Mathf.Min(canAdd, amount);

                    // 修改数量
                    invItem.amount += add;
                    amount -= add;

                    // Struct 不能直接修改，必须 移除 → 修改 → 加回去
                    items.RemoveAt(i);
                    items.Insert(i, invItem);
                    OnInventoryChanged?.Invoke();

                    // 加满了就退出
                    if (amount <= 0) return;
                }
            }
        }

        // 还有剩余数量，新建格子
        if (amount > 0)
        {
            while (amount > 0)
            {
                int addAmount = Mathf.Min(item.maxStack, amount);

                if (FindEmptySlot(item.width, item.height, out string gridType, out int gridIndex, out int x, out int y))
                {
                    InventoryItem newItem = new InventoryItem(item.id, addAmount, x, y, gridType, gridIndex, false);
                    items.Add(newItem);
                    OnInventoryChanged?.Invoke();
                    amount -= addAmount;
                }
                else // 没有空位加了
                {
                    // 后续可加扩展：在邮箱领取、丢地上
                    break;
                }
            }
        }
    }

    private void RemoveItemInternal(InventoryItem item)
    {
        items.Remove(item);
        OnInventoryChanged?.Invoke();
    }

    private void MoveItemInternal(InventoryItem item, string newGridType, int newGridIndex, int newX, int newY, bool newRotated)
    {
        items.Remove(item); // Struct 不能直接修改，必须 移除 → 修改 → 加回去

        InventoryItem newItem = item;
        newItem.gridType = newGridType;
        newItem.gridIndex = newGridIndex;
        newItem.x = newX;
        newItem.y = newY;
        newItem.isRotated = newRotated;

        items.Add(newItem);
        OnInventoryChanged?.Invoke();
    }

    private void UseItemInternal(InventoryItem item, Player user) // 使用物品
    {
        item.item.Use(user);//物品作用方法（数据驱动中、共用）

        int index = items.IndexOf(item);

        if (item.amount > 1) // 物品数量大于1时使用便减1
        {
            InventoryItem newItem = item;
            newItem.amount--;

            items.RemoveAt(index); // Struct 不能直接修改，必须 移除 → 修改 → 加回去
            items.Insert(index, newItem);
            OnInventoryChanged?.Invoke();
        }
        else
        {
            items.Remove(item); // 物品数量为1时使用便删除
            OnInventoryChanged?.Invoke();
        }
    }
    #endregion

    #region 辅助方法
    private bool FindEmptySlot(int width, int height, out string gridType, out int gridIndex, out int x, out int y) // 查找空位
    {
        if (TryFindInGridType("Small", width, height, out gridIndex, out x, out y))
        { gridType = "Small"; return true; }
        if (TryFindInGridType("Middle", width, height, out gridIndex, out x, out y))
        { gridType = "Middle"; return true; }
        if (TryFindInGridType("Large", width, height, out gridIndex, out x, out y))
        { gridType = "Large"; return true; }

        gridType = ""; gridIndex = -1; x = y = -1; return false;
    }

    private bool TryFindInGridType(string gridType, int width, int height, out int gridIndex, out int x, out int y)
    {
        int count = gridType switch
        {
            "Small" => backpackData.smallCount,
            "Middle" => backpackData.middleCount,
            "Large" => backpackData.largeCount,
            _ => 0
        };
        Vector2Int size = GetGridSize(gridType);

        for (int idx = 0; idx < count; idx++)
        {
            for (int yy = 0; yy <= size.y - height; yy++)
                for (int xx = 0; xx <= size.x - width; xx++)
                    if (!IsPositionOccupied(gridType, idx, xx, yy, width, height))
                    { gridIndex = idx; x = xx; y = yy; return true; }
        }

        gridIndex = -1; x = y = -1; return false;
    }

    private bool IsPositionOccupied(string gridType, int gridIndex, int x, int y, int width, int height) // 判断格子是否被占用
    {
        foreach (var item in items)
        {
            if (item.gridType == gridType && item.gridIndex == gridIndex)
            {
                int iw = item.Width;
                int ih = item.Height;
                if (x < item.x + iw && x + width > item.x && y < item.y + ih && y + height > item.y)
                    return true;
            }
        }
        return false;
    }

    private Vector2Int GetGridSize(string gridType)
    {
        return gridType switch
        {
            "Large" => new Vector2Int(backpackData.largeWidth, backpackData.largeHeight),
            "Middle" => new Vector2Int(backpackData.middleWidth, backpackData.middleHeight),
            "Small" => new Vector2Int(backpackData.smallWidth, backpackData.smallHeight),
            _ => Vector2Int.zero
        };
    }
    #endregion

    //箱子调用
    public void AddItemAtPosition(ItemData item, int amount, string gridType, int gridIndex, int x, int y, bool rotated)
    {
        if (amount <= 0) return;
        AddItemAtPositionInternal(item, amount, gridType, gridIndex, x, y, rotated);
        BackpackManage.Instance.UpdateBackpack();
    }

    private void AddItemAtPositionInternal(ItemData item, int amount, string gridType, int gridIndex, int x, int y, bool rotated)
    {
        InventoryItem newItem = new InventoryItem(item.id, amount, x, y, gridType, gridIndex, rotated);
        items.Add(newItem);
        OnInventoryChanged?.Invoke();
    }
}