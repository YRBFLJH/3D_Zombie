using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackManage : MonoBehaviour
{
    public static BackpackManage Instance;

    [Header("背包配置")]
    public BackpackData backpackData;
    public Transform backpackArea;
    RectTransform areaRect;
    public GameObject slotPrefab;

    [Header("各区域父节点")]
    public Transform[] slotLarge;
    public Transform[] slotMiddle;
    public Transform[] slotSmall;

    private List<Slot[,]> largeGrids = new List<Slot[,]>();
    private List<Slot[,]> middleGrids = new List<Slot[,]>();
    private List<Slot[,]> smallGrids = new List<Slot[,]>();

    static Dictionary<int,ItemData> itemMap = new Dictionary<int,ItemData>();

    private List<InventoryItem> items = new List<InventoryItem>();
    private InventoryItem currentSelectedItem;
    private InventoryItem draggingItem;

    // 高亮记录
    private int highlightX, highlightY;
    private Slot[,] highlightGrid;
    private List<Slot> currentHighlightSlots = new List<Slot>();
    private bool canPlaceAtHighlight;

    // 用于避免闪烁的上次有效格子
    private Slot lastValidSlot;


    //性能优化
    Vector2 lastMousePos;
    float checkInterval = 0.03f; // 拖拽时的检测间隔，防止每帧多次循环遍历，提升性能
    float lastCheckTime;

    void Awake() 
    {
        Instance = this;
        areaRect = backpackArea.GetComponent<RectTransform>();
    }

    void Start() => InitBackpack();

    #region 初始化网格
    void InitBackpack()
    {
        // 大型区域
        int largeW = backpackData.largeWidth;
        int largeH = backpackData.largeHeight;
        foreach (Transform area in slotLarge)
        {
            Slot[,] grid = new Slot[largeW, largeH];
            for (int y = 0; y < largeH; y++)
                for (int x = 0; x < largeW; x++)
                    CreateSlot(area, x, y, grid);
            largeGrids.Add(grid);
        }

        // 中型区域
        int middleW = backpackData.middleWidth;
        int middleH = backpackData.middleHeight;
        foreach (Transform area in slotMiddle)
        {
            Slot[,] grid = new Slot[middleW, middleH];
            for (int y = 0; y < middleH; y++)
                for (int x = 0; x < middleW; x++)
                    CreateSlot(area, x, y, grid);
            middleGrids.Add(grid);
        }

        // 小型区域
        int smallW = backpackData.smallWidth;
        int smallH = backpackData.smallHeight;
        foreach (Transform area in slotSmall)
        {
            Slot[,] grid = new Slot[smallW, smallH];
            for (int y = 0; y < smallH; y++)
                for (int x = 0; x < smallW; x++)
                    CreateSlot(area, x, y, grid);
            smallGrids.Add(grid);
        }
    }

    void CreateSlot(Transform parent, int x, int y, Slot[,] grid)
    {
        GameObject slotObj = Instantiate(slotPrefab, parent);
        Slot slot = slotObj.GetComponent<Slot>();
        slot.x = x;
        slot.y = y;
        slot.parentGrid = grid;
        slot.onSlotRightClick += RightClickSlot;
        grid[x, y] = slot;
    }
    #endregion

    #region 拖拽辅助
    public void StartDrag(InventoryItem item)
    {
        draggingItem = item;
        highlightGrid = null;
        canPlaceAtHighlight = false;
        lastValidSlot = null;
    }

    public void OnDrag(InventoryItem item, PointerEventData eventData)
    {
        if (draggingItem != item) return;

        if (Time.time - lastCheckTime < checkInterval && Vector2.Distance(Input.mousePosition, lastMousePos) < 5f) return;
        lastMousePos = Input.mousePosition;
        lastCheckTime = Time.time;

        Slot targetSlot = GetSlotUnderMouse(eventData);
        bool inBackpack = IsPointInAnyBackpackArea(Input.mousePosition, eventData);

        if (targetSlot != null)
        {
            // 更新最近有效格子
            lastValidSlot = targetSlot;
            // 根据当前鼠标下的格子更新高亮
            UpdateHighlightForSlot(item, targetSlot);
        }
        else if (inBackpack)
        {
            // 鼠标在背包区域内但没有命中格子，使用上次有效格子（如果有）
            if (lastValidSlot != null)
            {
                UpdateHighlightForSlot(item, lastValidSlot);
            }
            else
            {
                ClearHighlight();
            }
        }
        else
        {
            // 鼠标离开背包区域，清除高亮和上次有效
            ClearHighlight();
            lastValidSlot = null;
        }
    }


    /// <summary>
    /// 根据给定格子更新高亮
    /// </summary>
    private void UpdateHighlightForSlot(InventoryItem item, Slot slot)
    {
        int bestX, bestY;
        bool found = FindClosestPlacement(item, slot.x, slot.y, slot.parentGrid, out bestX, out bestY);
        if (found)
        {
            HighlightPlacement(item, slot.parentGrid, bestX, bestY, true);
            highlightX = bestX;
            highlightY = bestY;
            highlightGrid = slot.parentGrid;
            canPlaceAtHighlight = true;
        }
        else
        {
            HighlightPlacement(item, slot.parentGrid, slot.x, slot.y, false);
            highlightX = slot.x;
            highlightY = slot.y;
            highlightGrid = slot.parentGrid;
            canPlaceAtHighlight = false;
        }
    }

    public void EndDrag(InventoryItem item, PointerEventData eventData)
    {
        if (draggingItem != item) return;

        if (!IsPointInAnyBackpackArea(Input.mousePosition, eventData))
        {
            highlightGrid = null;
            DropItem(item);
            RemoveItem(item);
        }

        if (highlightGrid != null && canPlaceAtHighlight)
        {
            TryMoveItem(item, highlightGrid, highlightX, highlightY, item.isRotated);
        }

        ClearHighlight();
        draggingItem = null;
        highlightGrid = null;
        canPlaceAtHighlight = false;
        lastValidSlot = null;
    }

    /// <summary>
    /// 以参考格子 (refX, refY) 为中心，尝试将物品放置在中心、上、下、左、右五个位置
    /// </summary>
    private bool FindClosestPlacement(InventoryItem item, int refX, int refY, Slot[,] grid, out int bestX, out int bestY)
    {
        int width = item.Width;
        int height = item.Height;
        int gridW = grid.GetLength(0);
        int gridH = grid.GetLength(1);

        List<Vector2Int> candidates = new List<Vector2Int>();

        // 中心
        if (refX >= 0 && refX + width <= gridW && refY >= 0 && refY + height <= gridH)
        {
            if (CanPlaceItemAt(item, refX, refY, item.isRotated, grid))
                candidates.Add(new Vector2Int(refX, refY));
        }

        // 上方
        int yUp = refY - height;
        if (yUp >= 0 && yUp + height <= gridH && refX >= 0 && refX + width <= gridW)
        {
            if (CanPlaceItemAt(item, refX, yUp, item.isRotated, grid))
                candidates.Add(new Vector2Int(refX, yUp));
        }

        // 下方
        int yDown = refY + 1;
        if (yDown >= 0 && yDown + height <= gridH && refX >= 0 && refX + width <= gridW)
        {
            if (CanPlaceItemAt(item, refX, yDown, item.isRotated, grid))
                candidates.Add(new Vector2Int(refX, yDown));
        }

        // 左侧
        int xLeft = refX - width;
        if (xLeft >= 0 && xLeft + width <= gridW && refY >= 0 && refY + height <= gridH)
        {
            if (CanPlaceItemAt(item, xLeft, refY, item.isRotated, grid))
                candidates.Add(new Vector2Int(xLeft, refY));
        }

        // 右侧
        int xRight = refX + 1;
        if (xRight >= 0 && xRight + width <= gridW && refY >= 0 && refY + height <= gridH)
        {
            if (CanPlaceItemAt(item, xRight, refY, item.isRotated, grid))
                candidates.Add(new Vector2Int(xRight, refY));
        }

        if (candidates.Count == 0)
        {
            bestX = bestY = -1;
            return false;
        }

        // 选择距离最近
        int bestDist = int.MaxValue;
        bestX = candidates[0].x;
        bestY = candidates[0].y;
        foreach (var pos in candidates)
        {
            int dist = Mathf.Abs(pos.x - refX) + Mathf.Abs(pos.y - refY);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestX = pos.x;
                bestY = pos.y;
            }
        }
        return true;
    }

    private void HighlightPlacement(InventoryItem item, Slot[,] grid, int x, int y, bool canPlace)
    {
        // 位置不变直接return，不重复刷新,性能优化
        if (highlightGrid == grid && highlightX == x && highlightY == y) return;

        ClearHighlight();

        int width = item.Width;
        int height = item.Height;
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int gx = x + dx;
                int gy = y + dy;
                if (gx >= 0 && gx < grid.GetLength(0) && gy >= 0 && gy < grid.GetLength(1))
                {
                    Slot slot = grid[gx, gy];
                    slot.SetHighlight(true, canPlace ? Color.green : Color.red);
                    currentHighlightSlots.Add(slot);
                }
            }
        }
    }

    private void ClearHighlight()
    {
        foreach (Slot slot in currentHighlightSlots)
            slot.SetHighlight(false, Color.clear);
        currentHighlightSlots.Clear();
    }

    public Slot GetSlotUnderMouse(PointerEventData eventData)
    {
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        return hit?.GetComponent<Slot>();
    }

    bool IsPointInAnyBackpackArea(Vector2 mousePos, PointerEventData eventData)
    {
        if(RectTransformUtility.RectangleContainsScreenPoint(areaRect, mousePos, eventData.pressEventCamera))
        {Debug.Log("物品已放入背包");
            return true;

        }
        return false;
    }

    void DropItem(InventoryItem item) => Debug.Log($"丢弃物品：{item.item.itemName} x{item.amount}");
    #endregion

    #region 物品信息注册
    public static void RegisterItem(ItemData item)
    {
        if (!itemMap.ContainsKey(item.id))
        {
            itemMap.Add(item.id, item);
        }
    }

    public static ItemData GetItemData(int itemId)
    {
        if (itemMap.ContainsKey(itemId))
        {
            return itemMap[itemId];
        }
        return null;
    }

    #endregion





    #region 物品操作
    public bool TryFindEmptySlot(int width, int height, Slot[,] grid, out int outX, out int outY)
    {
        int gridW = grid.GetLength(0);
        int gridH = grid.GetLength(1);
        for (int y = 0; y <= gridH - height; y++)
            for (int x = 0; x <= gridW - width; x++)
            {
                bool free = true;
                for (int dy = 0; dy < height; dy++)
                    for (int dx = 0; dx < width; dx++)
                        if (grid[x + dx, y + dy].occupiedBy != null)
                        {
                            free = false;
                            break;
                        }
                if (free)
                {
                    outX = x;
                    outY = y;
                    return true;
                }
            }
        outX = outY = -1;
        return false;
    }

    public void AddItem(ItemData item, int amount)
    {
        // 堆叠
        foreach (InventoryItem invItem in items)
            if (invItem.item.id == item.id && item.isStackable)
            {
                int canAdd = item.maxStack - invItem.amount;
                if (canAdd > 0)
                {
                    int add = Mathf.Min(canAdd, amount);
                    invItem.amount += add;
                    amount -= add;
                    UpdateItemUI(invItem);
                    if (amount <= 0) return;
                }
            }

        if (amount <= 0) return;

        Slot[,] targetGrid = null;
        int outX = -1, outY = -1;

        if (item.width <= 2 && item.height <= 2)
        {
            foreach (var grid in smallGrids)
                if (TryFindEmptySlot(item.width, item.height, grid, out outX, out outY))
                {
                    targetGrid = grid;
                    break;
                }
        }
        if (targetGrid == null && item.width <= 3 && item.height <= 3)
        {
            foreach (var grid in middleGrids)
                if (TryFindEmptySlot(item.width, item.height, grid, out outX, out outY))
                {
                    targetGrid = grid;
                    break;
                }
        }
        if (targetGrid == null)
        {
            foreach (var grid in largeGrids)
                if (TryFindEmptySlot(item.width, item.height, grid, out outX, out outY))
                {
                    targetGrid = grid;
                    break;
                }
        }

        if (targetGrid != null)
        {
            // 记录准确位置
            string type = "";
            int idx = -1;
            if (largeGrids.Contains(targetGrid)) { type = "Large"; idx = largeGrids.IndexOf(targetGrid); }
            else if (middleGrids.Contains(targetGrid)) { type = "Middle"; idx = middleGrids.IndexOf(targetGrid); }
            else if (smallGrids.Contains(targetGrid)) { type = "Small"; idx = smallGrids.IndexOf(targetGrid); }

            InventoryItem newItem = new InventoryItem(item, amount, outX, outY, targetGrid, type, idx);
            items.Add(newItem);
            for (int dy = 0; dy < newItem.Height; dy++)
                for (int dx = 0; dx < newItem.Width; dx++)
                    targetGrid[outX + dx, outY + dy].SetOccupiedBy(newItem);
            UpdateItemUI(newItem);
        }
        else
        {
            Debug.Log("背包已满，无法添加物品：" + item.itemName);
        }
    }

    public void RemoveItem(InventoryItem item)
    {
        items.Remove(item);
        Slot[,] grid = item.parentGrid;
        for (int dy = 0; dy < item.Height; dy++)
            for (int dx = 0; dx < item.Width; dx++)
                grid[item.x + dx, item.y + dy].ClearOccupied();
        for (int dy = 0; dy < item.Height; dy++)
            for (int dx = 0; dx < item.Width; dx++)
                grid[item.x + dx, item.y + dy].UpdateUI();
    }

    public bool TryMoveItem(InventoryItem item, Slot[,] newGrid, int newX, int newY, bool rotated)
    {
        int width = rotated ? item.item.height : item.item.width;
        int height = rotated ? item.item.width : item.item.height;

        if (newX < 0 || newY < 0 || newX + width > newGrid.GetLength(0) || newY + height > newGrid.GetLength(1))
            return false;

        for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                if (newGrid[newX + dx, newY + dy].occupiedBy != null && newGrid[newX + dx, newY + dy].occupiedBy != item)
                    return false;

        Slot[,] oldGrid = item.parentGrid;
        int oldX = item.x, oldY = item.y;
        int oldWidth = item.Width, oldHeight = item.Height;

        for (int dy = 0; dy < oldHeight; dy++)
            for (int dx = 0; dx < oldWidth; dx++)
                oldGrid[oldX + dx, oldY + dy].ClearOccupied();

        item.x = newX;
        item.y = newY;
        item.isRotated = rotated;
        item.parentGrid = newGrid;

        for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                newGrid[newX + dx, newY + dy].SetOccupiedBy(item);

        for (int dy = 0; dy < oldHeight; dy++)
            for (int dx = 0; dx < oldWidth; dx++)
                oldGrid[oldX + dx, oldY + dy].UpdateUI();
        for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                newGrid[newX + dx, newY + dy].UpdateUI();

        return true;
    }

    public bool CanPlaceItemAt(InventoryItem item, int x, int y, bool rotated, Slot[,] grid)
    {
        int width = rotated ? item.item.height : item.item.width;
        int height = rotated ? item.item.width : item.item.height;
        if (x < 0 || y < 0 || x + width > grid.GetLength(0) || y + height > grid.GetLength(1))
            return false;
        for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                if (grid[x + dx, y + dy].occupiedBy != null && grid[x + dx, y + dy].occupiedBy != item)
                    return false;
        return true;
    }

    private void UpdateItemUI(InventoryItem item)
    {
        Slot[,] grid = item.parentGrid;
        for (int dy = 0; dy < item.Height; dy++)
            for (int dx = 0; dx < item.Width; dx++)
                grid[item.x + dx, item.y + dy].UpdateUI();
    }
    #endregion

    #region 选中与旋转
    public void SelectItem(InventoryItem item)
    {
        if (currentSelectedItem == item)
        {
            // 如果点击同一个格子，取消选中
            if (currentSelectedItem != null)
            {
                Slot[,] grid = currentSelectedItem.parentGrid;
                for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                    for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                        grid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].selectBorder.SetActive(false);
                currentSelectedItem = null;
            }
        }
        else
        {
            // 取消之前的选中
            if (currentSelectedItem != null)
            {
                Slot[,] grid = currentSelectedItem.parentGrid;
                for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                    for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                        grid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].selectBorder.SetActive(false);
            }
            // 选中新的格子
            currentSelectedItem = item;
            if (currentSelectedItem != null)
            {
                Slot[,] grid = currentSelectedItem.parentGrid;
                for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                    for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                        grid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].selectBorder.SetActive(true);
            }
        }
    }

    void Update()
    {
        if (currentSelectedItem != null && Input.GetKeyDown(KeyCode.R))
            RotateSelectedItem();
    }

    public void RotateSelectedItem()
    {
        if (currentSelectedItem == null) return;
        if (CanPlaceItemAt(currentSelectedItem, currentSelectedItem.x, currentSelectedItem.y, !currentSelectedItem.isRotated, currentSelectedItem.parentGrid))
        {
            Slot[,] grid = currentSelectedItem.parentGrid;
            for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                    grid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].ClearOccupied();

            currentSelectedItem.isRotated = !currentSelectedItem.isRotated;

            for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                    grid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].SetOccupiedBy(currentSelectedItem);

            for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                    grid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].UpdateUI();
        }
        else
        {
            Debug.Log("无法旋转，空间不足");
        }
    }
    #endregion

    #region 事件响应
    void RightClickSlot(Slot slot)
    {
        if (slot.occupiedBy != null)
            slot.occupiedBy.item.Use(Player.instance);
    }
    #endregion





    #region 辅助存档

    // 保存所有物品
    public InventorySaveData GetSaveData()
    {
        InventorySaveData data = new InventorySaveData();

        foreach (var item in items) // 遍历背包物品
        {
            ItemsSaveData itemData = new ItemsSaveData
        {
            itemId = item.item.id,
            amount = item.amount,
            gridType = item.gridType,
            gridIndex = item.gridIndex,
            x = item.x,
            y = item.y,
            isRotated = item.isRotated
        };
        data.items.Add(itemData);
        Debug.Log($"保存物品: {item.item.itemName}, 数量: {item.amount}");
        }
        return data;
    }

    // 得到物品所在的格子位置（用于加载重建）
    Slot[,] GeetGridByTypeAndIndex(string gridType, int index)
    {
        switch (gridType)
        {
            case "Large":
                if (index >= 0 && index < largeGrids.Count) return largeGrids[index];
                break;
            case "Middle":
                if (index >= 0 && index < middleGrids.Count) return middleGrids[index];
                break;
            case "Small":
                if (index >= 0 && index < smallGrids.Count) return smallGrids[index];
                break;
        }
        return null;
    }

    // 清空背包
    void Clear()
    {
        foreach (var grid in largeGrids) ClearGrid(grid);
        foreach (var grid in middleGrids) ClearGrid(grid);
        foreach (var grid in smallGrids) ClearGrid(grid);

        items.Clear();
        currentSelectedItem = null;
        draggingItem = null;
        ClearHighlight();
    }
    void ClearGrid(Slot[,] grid)
    {
        for (int y = 0; y < grid.GetLength(1); y++)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                grid[x, y].ClearOccupied();
            }
        }
    }

    // 加载背包
    public void LoadInventory(InventorySaveData data)
    {
        Debug.Log($"itemMap 中共有 {itemMap.Count} 个物品");
        foreach (var kvp in itemMap)
        {
            Debug.Log($"注册物品 ID: {kvp.Key}, 名称: {kvp.Value.itemName}");
        }


        Clear();
        foreach (var itemSave in data.items)
        {
            Debug.Log($"尝试加载物品: itemId={itemSave.itemId}, amount={itemSave.amount}, gridType={itemSave.gridType}, gridIndex={itemSave.gridIndex}, x={itemSave.x}, y={itemSave.y}, rotated={itemSave.isRotated}");

            // 根据id找物品数据
            ItemData itemData = GetItemData(itemSave.itemId);
            if (itemData == null) 
            {
                Debug.LogError($"物品数据不存在: itemId={itemSave.itemId}");
                continue;
            }

            // 找到物品保存的网格位置
            Slot[,] targetGrid = GeetGridByTypeAndIndex(itemSave.gridType,itemSave.gridIndex);
            if (targetGrid == null) 
            {
                Debug.LogError($"网格不存在: {itemSave.gridType}_{itemSave.gridIndex}");
                continue;
            }

            // 创建物品
            InventoryItem item = new InventoryItem(itemData, itemSave.amount, itemSave.x, itemSave.y, targetGrid, itemSave.gridType, itemSave.gridIndex, itemSave.isRotated);
            items.Add(item);

            Debug.Log("加载物品：" + itemData.itemName);
            // 占用
            int width = item.Width;
            int height = item.Height;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    targetGrid[item.x + j, item.y + i].SetOccupiedBy(item);
                }
            }

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                targetGrid[item.x + j, item.y + i].UpdateUI();
                }
            }
        }
    }
    #endregion
}
