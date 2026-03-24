using UnityEngine;

[System.Serializable]
public struct InventoryItem
{
    public int itemId;
    public int amount;
    public int x, y;
    public bool isRotated;

    public string gridType;
    public int gridIndex;

    public ItemData item => BackpackManage.GetItemData(itemId);
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
}