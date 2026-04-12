using System.Collections.Generic;
using UnityEngine;

public class ChestInventory : MonoBehaviour
{
    [Header("箱子配置（与背包相同）")]
    public BackpackData backpackData;

    // 改为普通列表
    public List<InventoryItem> items = new List<InventoryItem>();

    // 供 UI 注册刷新回调
    public event System.Action OnInventoryChanged;

    void Start()
    {
        // 在修改 items 后手动调用 OnInventoryChanged，与 SyncList 回调效果一致
        // 所有对 items 的修改方法均需触发事件
    }

    #region 公共接口（直接调用）
    public void AddItem(ItemData item, int amount)
    {
        AddItemInternal(item, amount);
    }

    public void AddItemAtPosition(ItemData item, int amount, string gridType, int gridIndex, int x, int y, bool rotated)
    {
        AddItemAtPositionInternal(item, amount, gridType, gridIndex, x, y, rotated);
    }

    public void RemoveItem(InventoryItem item)
    {
        RemoveItemInternal(item);
    }

    public void MoveItem(InventoryItem item, string newGridType, int newGridIndex, int newX, int newY, bool rotated)
    {
        MoveItemInternal(item, newGridType, newGridIndex, newX, newY, rotated);
    }
    #endregion

    #region 内部实现
    private void AddItemInternal(ItemData item, int amount)
    {
        if (amount <= 0) return;

        // 不可堆叠
        if (!item.isStackable)
        {
            for (int i = 0; i < amount; i++)
            {
                if (FindEmptySlot(item.width, item.height, out string gridType, out int gridIndex, out int x, out int y))
                {
                    items.Add(new InventoryItem(item.id, 1, x, y, gridType, gridIndex, false));
                }
                else break;
            }
            OnInventoryChanged?.Invoke();
            return;
        }

        // 可堆叠：先合并已有物品
        for (int i = 0; i < items.Count && amount > 0; i++)
        {
            InventoryItem inv = items[i];
            if (inv.itemId == item.id && inv.amount < item.maxStack)
            {
                int canAdd = item.maxStack - inv.amount;
                int add = Mathf.Min(canAdd, amount);
                inv.amount += add;
                amount -= add;
                items[i] = inv;
            }
        }

        // 剩余数量新建物品
        while (amount > 0)
        {
            int add = Mathf.Min(item.maxStack, amount);
            if (FindEmptySlot(item.width, item.height, out string gridType, out int gridIndex, out int x, out int y))
            {
                items.Add(new InventoryItem(item.id, add, x, y, gridType, gridIndex, false));
                amount -= add;
            }
            else break;
        }
        OnInventoryChanged?.Invoke();
    }

    private void AddItemAtPositionInternal(ItemData item, int amount, string gridType, int gridIndex, int x, int y, bool rotated)
    {
        InventoryItem newItem = new InventoryItem(item.id, amount, x, y, gridType, gridIndex, rotated);
        items.Add(newItem);
        OnInventoryChanged?.Invoke();
    }

    private void RemoveItemInternal(InventoryItem item)
    {
        items.Remove(item);
        OnInventoryChanged?.Invoke();
    }

    private void MoveItemInternal(InventoryItem item, string newGridType, int newGridIndex, int newX, int newY, bool rotated)
    {
        int idx = -1;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].itemId == item.itemId &&
                items[i].x == item.x &&
                items[i].y == item.y &&
                items[i].gridType == item.gridType &&
                items[i].gridIndex == item.gridIndex)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0) return;

        InventoryItem newItem = items[idx];
        newItem.gridType = newGridType;
        newItem.gridIndex = newGridIndex;
        newItem.x = newX;
        newItem.y = newY;
        newItem.isRotated = rotated;
        items[idx] = newItem;
        OnInventoryChanged?.Invoke();
    }
    #endregion

    #region 辅助方法（查找空位，与 PlayerBackpack 一致）
    private bool FindEmptySlot(int width, int height, out string gridType, out int gridIndex, out int x, out int y)
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

    private bool IsPositionOccupied(string gridType, int gridIndex, int x, int y, int width, int height)
    {
        foreach (var item in items)
        {
            if (item.gridType == gridType && item.gridIndex == gridIndex)
            {
                int iw = item.Width, ih = item.Height;
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

    public void InitRandomItems()
    {
        ItemData[] itemData = GetComponent<RewardBox>().rewards;

        for (int i = 0; i < itemData.Length; i++)
        {
            AddItem(itemData[i], 5);
        }
    }
}