using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBackpack : NetworkBehaviour
{
    [Header("背包配置")]
    public BackpackData backpackData;

    public readonly SyncList<InventoryItem> items = new SyncList<InventoryItem>();
    public event System.Action OnInventoryChanged;

    void Awake()
    {
        items.Callback += OnItemsChanged;
    }

    void Start()
    {
        BackpackManage.Instance.GetCom();
    }

    void OnItemsChanged(SyncList<InventoryItem>.Operation op, int index, InventoryItem oldItem, InventoryItem newItem)
    {
        OnInventoryChanged?.Invoke();
    }

    public void AddItem(ItemData item, int amount)
    {
        if (amount <= 0) return;

        if (isServer)
            AddItemInternal(item, amount);
        else if (isClientOnly)
            CmdAddItem(item.id, amount);
        else
            AddItemInternal(item, amount);
    }

    public void RemoveItem(InventoryItem item)
    {
        if (isServer)
            RemoveItemInternal(item);
        else if (isClientOnly)
        {
            int index = items.IndexOf(item);
            if (index >= 0)
                CmdRemoveItem(index);
        }
        else
            RemoveItemInternal(item);
    }

    public void MoveItem(InventoryItem item, string newGridType, int newGridIndex, int newX, int newY, bool newRotated)
    {
        if (isServer)
            MoveItemInternal(item, newGridType, newGridIndex, newX, newY, newRotated);
        else if (isClientOnly)
        {
            int index = items.IndexOf(item);
            if (index >= 0)
                CmdMoveItem(index, newGridType, newGridIndex, newX, newY, newRotated);
        }
        else
            MoveItemInternal(item, newGridType, newGridIndex, newX, newY, newRotated);
    }

    public void UseItem(InventoryItem item, Player user)
    {
        if (isServer)
            UseItemInternal(item, user);
        else if (isClientOnly)
        {
            int index = items.IndexOf(item);
            if (index >= 0)
                CmdUseItem(index);
        }
        else
            UseItemInternal(item, user);
    }

    [Server]
    private void AddItemInternal(ItemData item, int amount)
    {
        if (item.isStackable)
        {
            for (int i = 0; i < items.Count; i++)
            {
                InventoryItem invItem = items[i];
                if (invItem.itemId == item.id)
                {
                    int canAdd = item.maxStack - invItem.amount;
                    if (canAdd > 0)
                    {
                        int add = Mathf.Min(canAdd, amount);
                        invItem.amount += add;
                        amount -= add;
                        items[i] = invItem;
                        if (amount <= 0) return;
                    }
                }
            }
        }

        if (amount > 0)
        {
            if (FindEmptySlot(item.width, item.height, out string gridType, out int gridIndex, out int x, out int y))
            {
                InventoryItem newItem = new InventoryItem(item.id, amount, x, y, gridType, gridIndex, false);
                items.Add(newItem);
            }
            else
            {
                Debug.Log($"背包已满，无法添加 {item.itemName} x{amount}");
            }
        }
    }

    [Server]
    private void RemoveItemInternal(InventoryItem item)
    {
        items.Remove(item);
    }

    [Server]
    private void MoveItemInternal(InventoryItem item, string newGridType, int newGridIndex, int newX, int newY, bool newRotated)
    {
        if (!CanPlaceItemAtInternal(item, newGridType, newGridIndex, newX, newY, newRotated))
        {
            Debug.Log("移动失败：目标位置被占用或超出边界");
            return;
        }

        item.gridType = newGridType;
        item.gridIndex = newGridIndex;
        item.x = newX;
        item.y = newY;
        item.isRotated = newRotated;

        int idx = items.IndexOf(item);
        if (idx >= 0)
            items[idx] = item;
    }

    [Server]
    private void UseItemInternal(InventoryItem item, Player user)
    {
        item.item.Use(user);

        if (item.amount > 1)
        {
            int index = items.IndexOf(item);
            item.amount--;
            items[index] = item;
        }
        else
        {
            items.Remove(item);
        }
    }

    [Server]
    private bool FindEmptySlot(int width, int height, out string gridType, out int gridIndex, out int x, out int y)
    {
        if (TryFindInGridType("Small", width, height, out gridIndex, out x, out y))
        { gridType = "Small"; return true; }
        if (TryFindInGridType("Middle", width, height, out gridIndex, out x, out y))
        { gridType = "Middle"; return true; }
        if (TryFindInGridType("Large", width, height, out gridIndex, out x, out y))
        { gridType = "Large"; return true; }

        gridType = "";
        gridIndex = -1;
        x = y = -1;
        return false;
    }

    [Server]
    private bool TryFindInGridType(string gridType, int width, int height, out int gridIndex, out int x, out int y)
    {
        int count = gridType switch
        {
            "Small" => backpackData.smallCount,
            "Middle" => backpackData.middleCount,
            "Large" => backpackData.largeCount,
            _ => 0
        };
        Vector2Int size = GetGridSize(gridType, 0);

        for (int idx = 0; idx < count; idx++)
        {
            for (int yy = 0; yy <= size.y - height; yy++)
                for (int xx = 0; xx <= size.x - width; xx++)
                    if (!IsPositionOccupied(gridType, idx, xx, yy, width, height))
                    {
                        gridIndex = idx; x = xx; y = yy; return true;
                    }
        }

        gridIndex = -1; x = y = -1; return false;
    }

    [Server]
    private bool IsPositionOccupied(string gridType, int gridIndex, int x, int y, int width, int height)
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

    [Server]
    private bool CanPlaceItemAtInternal(InventoryItem item, string gridType, int gridIndex, int x, int y, bool rotated)
    {
        int width = rotated ? item.item.height : item.item.width;
        int height = rotated ? item.item.width : item.item.height;
        Vector2Int size = GetGridSize(gridType, gridIndex);

        if (x < 0 || y < 0 || x + width > size.x || y + height > size.y)
            return false;

        foreach (var existing in items)
        {
            if (existing.Equals(item)) continue;
            if (existing.gridType == gridType && existing.gridIndex == gridIndex)
            {
                int ew = existing.Width;
                int eh = existing.Height;
                if (x < existing.x + ew && x + width > existing.x && y < existing.y + eh && y + height > existing.y)
                    return false;
            }
        }
        return true;
    }

    [Server]
    private Vector2Int GetGridSize(string gridType, int gridIndex)
    {
        return gridType switch
        {
            "Large" => new Vector2Int(backpackData.largeWidth, backpackData.largeHeight),
            "Middle" => new Vector2Int(backpackData.middleWidth, backpackData.middleHeight),
            "Small" => new Vector2Int(backpackData.smallWidth, backpackData.smallHeight),
            _ => Vector2Int.zero
        };
    }

    [Command]
    public void CmdAddItem(int itemId, int amount)
    {
        ItemData item = BackpackManage.GetItemData(itemId);
        if (item != null)
            AddItemInternal(item, amount);
    }

    [Command]
    private void CmdRemoveItem(int itemIndex)
    {
        if (itemIndex >= 0 && itemIndex < items.Count)
            RemoveItemInternal(items[itemIndex]);
    }

    [Command]
    private void CmdMoveItem(int itemIndex, string newGridType, int newGridIndex, int newX, int newY, bool newRotated)
    {
        if (itemIndex >= 0 && itemIndex < items.Count)
            MoveItemInternal(items[itemIndex], newGridType, newGridIndex, newX, newY, newRotated);
    }

    [Command]
    private void CmdUseItem(int itemIndex)
    {
        if (itemIndex >= 0 && itemIndex < items.Count)
            UseItemInternal(items[itemIndex], GetComponent<Player>());
    }
}