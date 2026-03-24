using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackManage : MonoBehaviour
{
    public static BackpackManage Instance;

    [Header("背包配置")]
    public BackpackData backpackData;
    public Transform backpackArea;
    public GameObject slotPrefab;

    [Header("各区域父节点")]
    public Transform[] slotLarge;
    public Transform[] slotMiddle;
    public Transform[] slotSmall;

    [Header("UI 控制")]
    public GameObject showBag;

    public List<Slot[,]> largeGrids = new List<Slot[,]>();
    public List<Slot[,]> middleGrids = new List<Slot[,]>();
    public List<Slot[,]> smallGrids = new List<Slot[,]>();

    private PlayerBackpack playerBackpack;
    private Player localPlayer;

    private InventoryItem currentSelectedItem;
    private InventoryItem draggingItem;
    private Slot[,] highlightGrid;
    private int highlightX, highlightY;
    private List<Slot> currentHighlightSlots = new List<Slot>();
    private bool canPlaceAtHighlight;
    private Slot lastValidSlot;

    private RectTransform areaRect;
    private Vector2 lastMousePos;
    private float checkInterval = 0.03f;
    private float lastCheckTime;

    void Awake()
    {
        Instance = this;
        areaRect = backpackArea.GetComponent<RectTransform>();
        showBag.SetActive(false);
    }

    public void GetCom()
    {
        playerBackpack = NetworkClient.localPlayer.GetComponent<PlayerBackpack>();
    }

    void Start()
    {
        InitBackpack();
        Invoke(nameof(DelayInit), 0.1f); // 延迟初始化，防止联机未加载完成
    }

    void DelayInit()
    {
        if (NetworkClient.localPlayer != null)
        {
            localPlayer = NetworkClient.localPlayer.GetComponent<Player>();
            playerBackpack = NetworkClient.localPlayer.GetComponent<PlayerBackpack>();
        }
        else
        {
            localPlayer = FindObjectOfType<Player>();
            playerBackpack = localPlayer?.GetComponent<PlayerBackpack>();
        }

        if (playerBackpack != null)
        {
            playerBackpack.OnInventoryChanged += RefreshAllGrids;
            RefreshAllGrids(); // 强制第一次刷新
        }
        else
        {
            Debug.LogError("未找到 PlayerBackpack 组件！");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            showBag.SetActive(!showBag.activeSelf);
            RefreshAllGrids(); // 打开背包强制刷新
        }

        if (currentSelectedItem.item != null && Input.GetKeyDown(KeyCode.R))
        {
            RotateSelectedItem();
        }
    }

    void InitBackpack()
    {
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
        grid[x,y] = slot;
    }

    // 【全局强制刷新：所有UI不刷新问题都靠它】
    public void RefreshAllGrids()
    {
        if (playerBackpack == null) return;

        ClearAllGrids();

        foreach (var item in playerBackpack.items)
        {
            Slot[,] grid = GetGridByTypeAndIndex(item.gridType, item.gridIndex);
            if (grid == null) continue;

            int width = item.Width;
            int height = item.Height;
            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    int gx = item.x + dx;
                    int gy = item.y + dy;
                    if (gx >= 0 && gx < grid.GetLength(0) && gy >= 0 && gy < grid.GetLength(1))
                    {
                        grid[gx, gy].SetOccupiedBy(item);
                    }
                }
            }
        }
    }

    void ClearAllGrids()
    {
        foreach (var grid in largeGrids) ClearGridUI(grid);
        foreach (var grid in middleGrids) ClearGridUI(grid);
        foreach (var grid in smallGrids) ClearGridUI(grid);
    }

    void ClearGridUI(Slot[,] grid)
    {
        for (int y = 0; y < grid.GetLength(1); y++)
            for (int x = 0; x < grid.GetLength(0); x++)
                grid[x, y].ClearOccupied();
    }

    Slot[,] GetGridByTypeAndIndex(string gridType, int index)
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

    public void StartDrag(InventoryItem item)
    {
        draggingItem = item;
        highlightGrid = null;
        canPlaceAtHighlight = false;
        lastValidSlot = null;
    }

    public void OnDrag(InventoryItem item, PointerEventData eventData)
    {
        if (draggingItem.item == null || !draggingItem.Equals(item)) return;

        if (Time.time - lastCheckTime < checkInterval && Vector2.Distance(Input.mousePosition, lastMousePos) < 5f) return;
        lastMousePos = Input.mousePosition;
        lastCheckTime = Time.time;

        Slot targetSlot = GetSlotUnderMouse(eventData);
        bool inBackpack = IsPointInAnyBackpackArea(Input.mousePosition, eventData);

        if (targetSlot != null)
        {
            lastValidSlot = targetSlot;
            UpdateHighlightForSlot(item, targetSlot);
        }
        else if (inBackpack)
        {
            if (lastValidSlot != null)
                UpdateHighlightForSlot(item, lastValidSlot);
            else
                ClearHighlight();
        }
        else
        {
            ClearHighlight();
            lastValidSlot = null;
        }
    }

    void UpdateHighlightForSlot(InventoryItem item, Slot slot)
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

    bool FindClosestPlacement(InventoryItem item, int refX, int refY, Slot[,] grid, out int bestX, out int bestY)
    {
        int width = item.Width;
        int height = item.Height;
        int gridW = grid.GetLength(0);
        int gridH = grid.GetLength(1);
        List<Vector2Int> candidates = new List<Vector2Int>();

        if (refX >= 0 && refX + width <= gridW && refY >= 0 && refY + height <= gridH)
            if (CanPlaceItemAtGrid(item, refX, refY, item.isRotated, grid))
                candidates.Add(new Vector2Int(refX, refY));

        int yUp = refY - height;
        if (yUp >= 0 && yUp + height <= gridH && refX >= 0 && refX + width <= gridW)
            if (CanPlaceItemAtGrid(item, refX, yUp, item.isRotated, grid))
                candidates.Add(new Vector2Int(refX, yUp));

        int yDown = refY + 1;
        if (yDown >= 0 && yDown + height <= gridH && refX >= 0 && refX + width <= gridW)
            if (CanPlaceItemAtGrid(item, refX, yDown, item.isRotated, grid))
                candidates.Add(new Vector2Int(refX, yDown));

        int xLeft = refX - width;
        if (xLeft >= 0 && xLeft + width <= gridW && refY >= 0 && refY + height <= gridH)
            if (CanPlaceItemAtGrid(item, xLeft, refY, item.isRotated, grid))
                candidates.Add(new Vector2Int(xLeft, refY));

        int xRight = refX + 1;
        if (xRight >= 0 && xRight + width <= gridW && refY >= 0 && refY + height <= gridH)
            if (CanPlaceItemAtGrid(item, xRight, refY, item.isRotated, grid))
                candidates.Add(new Vector2Int(xRight, refY));

        if (candidates.Count == 0)
        {
            bestX = bestY = -1;
            return false;
        }

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

    bool CanPlaceItemAtGrid(InventoryItem item, int x, int y, bool rotated, Slot[,] grid)
    {
        int width = rotated ? item.item.height : item.item.width;
        int height = rotated ? item.item.width : item.item.height;
        if (x < 0 || y < 0 || x + width > grid.GetLength(0) || y + height > grid.GetLength(1))
            return false;
        for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                if (grid[x + dx, y + dy].occupiedBy.item != null && !grid[x + dx, y + dy].occupiedBy.Equals(item))
                    return false;
        return true;
    }

    void HighlightPlacement(InventoryItem item, Slot[,] grid, int x, int y, bool canPlace)
    {
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

    void ClearHighlight()
    {
        foreach (Slot slot in currentHighlightSlots)
            slot.SetHighlight(false, Color.clear);
        currentHighlightSlots.Clear();
    }

    public void EndDrag(InventoryItem item, PointerEventData eventData)
    {
        if (draggingItem.item == null || !draggingItem.Equals(item)) return;

        if (!IsPointInAnyBackpackArea(Input.mousePosition, eventData))
        {
            playerBackpack.RemoveItem(item);
            DropItem(item);
        }
        else if (highlightGrid != null && canPlaceAtHighlight)
        {
            string targetType = "";
            int targetIdx = -1;
            if (largeGrids.Contains(highlightGrid)) { targetType = "Large"; targetIdx = largeGrids.IndexOf(highlightGrid); }
            else if (middleGrids.Contains(highlightGrid)) { targetType = "Middle"; targetIdx = middleGrids.IndexOf(highlightGrid); }
            else if (smallGrids.Contains(highlightGrid)) { targetType = "Small"; targetIdx = smallGrids.IndexOf(highlightGrid); }

            if (targetType != "")
            {
                playerBackpack.MoveItem(item, targetType, targetIdx, highlightX, highlightY, item.isRotated);
            }
        }

        ClearHighlight();
        draggingItem = new InventoryItem();
        highlightGrid = null;
        canPlaceAtHighlight = false;
        lastValidSlot = null;
        
        RefreshAllGrids(); // 强制刷新
    }

    public Slot GetSlotUnderMouse(PointerEventData eventData)
    {
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        return hit?.GetComponent<Slot>();
    }

    bool IsPointInAnyBackpackArea(Vector2 mousePos, PointerEventData eventData)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(areaRect, mousePos, eventData.pressEventCamera);
    }

    void DropItem(InventoryItem item) { }

    static Dictionary<int, ItemData> itemMap = new Dictionary<int, ItemData>();

    public static void RegisterItem(ItemData item)
    {
        if (!itemMap.ContainsKey(item.id))
            itemMap.Add(item.id, item);
    }

    public static ItemData GetItemData(int itemId)
    {
        itemMap.TryGetValue(itemId, out ItemData data);
        return data;
    }

    public void SelectItem(InventoryItem item)
    {
        if (currentSelectedItem.item != null)
        {
            Slot[,] oldGrid = GetGridByTypeAndIndex(currentSelectedItem.gridType, currentSelectedItem.gridIndex);
            if (oldGrid != null)
            {
                for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                    for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                        oldGrid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].selectBorder.SetActive(false);
            }
        }

        currentSelectedItem = item;

        if (currentSelectedItem.item != null)
        {
            Slot[,] newGrid = GetGridByTypeAndIndex(currentSelectedItem.gridType, currentSelectedItem.gridIndex);
            if (newGrid != null)
            {
                for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                    for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                        newGrid[currentSelectedItem.x + dx, currentSelectedItem.y + dy].selectBorder.SetActive(true);
            }
        }
    }

    public void RotateSelectedItem()
    {
        if (currentSelectedItem.item == null) return;

        Slot[,] grid = GetGridByTypeAndIndex(currentSelectedItem.gridType, currentSelectedItem.gridIndex);
        if (grid == null) return;

        if (CanPlaceItemAtGrid(currentSelectedItem, currentSelectedItem.x, currentSelectedItem.y,
                               !currentSelectedItem.isRotated, grid))
        {
            playerBackpack.MoveItem(currentSelectedItem, currentSelectedItem.gridType, currentSelectedItem.gridIndex,
                                    currentSelectedItem.x, currentSelectedItem.y, !currentSelectedItem.isRotated);
            RefreshAllGrids();
        }
        else
        {
            Debug.Log("无法旋转，空间不足");
        }
    }

    void RightClickSlot(Slot slot)
    {
        if (slot.occupiedBy.item != null)
        {
            playerBackpack.UseItem(slot.occupiedBy, localPlayer);
            RefreshAllGrids();
        }
    }

    public InventorySaveData GetSaveData()
    {
        InventorySaveData data = new InventorySaveData();
        foreach (var item in playerBackpack.items)
        {
            data.items.Add(new ItemsSaveData
            {
                itemId = item.itemId,
                amount = item.amount,
                gridType = item.gridType,
                gridIndex = item.gridIndex,
                x = item.x,
                y = item.y,
                isRotated = item.isRotated
            });
        }
        return data;
    }

    public void LoadInventory(InventorySaveData data)
    {
        foreach (var itemSave in data.items)
        {
            ItemData itemData = GetItemData(itemSave.itemId);
            if (itemData != null)
            {
                playerBackpack.AddItem(itemData, itemSave.amount);
            }
        }
        RefreshAllGrids(); // 读档后刷新
    }
}