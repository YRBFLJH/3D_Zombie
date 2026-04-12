using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChestUI : MonoBehaviour
{
    [Header("箱子引用")]
    public ChestInventory chestInventory;
    public GameObject chestPanel;

    [Header("UI区域")]
    public Transform[] largeArea;
    public Transform[] middleArea;
    public Transform[] smallArea;
    public Slot slotPrefab;

    [Header("重要：箱子UI面板的RectTransform")]
    public RectTransform chestAreaRect;

    private List<Slot[,]> largeAreaGrids = new List<Slot[,]>();
    private List<Slot[,]> middleAreaGrids = new List<Slot[,]>();
    private List<Slot[,]> smallAreaGrids = new List<Slot[,]>();

    private InventoryItem draggingItem;
    private Slot[,] highlightGrid;
    private int highlightX, highlightY;
    private List<Slot> currentHighlightSlots = new List<Slot>();
    private bool canPlaceAtHighlight;
    private Slot lastHightSlot;

    private Vector2 lastMousePos;
    private float checkInterval = 0.05f;
    private float lastCheckTime;

    private InventoryItem selectedItem;

    void Awake()
    {
        if (chestPanel != null)
            chestPanel.SetActive(false);
    }

    void Start()
    {
        if (chestInventory == null)
            chestInventory = GetComponent<ChestInventory>();
        InitChestGrids();
        chestInventory.OnInventoryChanged += RefreshUI;
    }

    void OnDestroy()
    {
        if (chestInventory != null)
            chestInventory.OnInventoryChanged -= RefreshUI;
    }

    private void InitChestGrids()
    {
        BackpackData data = chestInventory.backpackData;
        int largeW = data.largeWidth, largeH = data.largeHeight;
        foreach (Transform area in largeArea)
        {
            Slot[,] grid = new Slot[largeW, largeH];
            for (int y = 0; y < largeH; y++)
                for (int x = 0; x < largeW; x++)
                    SpawnSlot(area, x, y, grid);
            largeAreaGrids.Add(grid);
        }

        int middleW = data.middleWidth, middleH = data.middleHeight;
        foreach (Transform area in middleArea)
        {
            Slot[,] grid = new Slot[middleW, middleH];
            for (int y = 0; y < middleH; y++)
                for (int x = 0; x < middleW; x++)
                    SpawnSlot(area, x, y, grid);
            middleAreaGrids.Add(grid);
        }

        int smallW = data.smallWidth, smallH = data.smallHeight;
        foreach (Transform area in smallArea)
        {
            Slot[,] grid = new Slot[smallW, smallH];
            for (int y = 0; y < smallH; y++)
                for (int x = 0; x < smallW; x++)
                    SpawnSlot(area, x, y, grid);
            smallAreaGrids.Add(grid);
        }
    }

    private void SpawnSlot(Transform parent, int x, int y, Slot[,] grid)
    {
        Slot slot = Instantiate(slotPrefab, parent);
        slot.x = x;
        slot.y = y;
        slot.parentGrid = grid;

        // 覆盖事件
        slot.onLeftClick = OnSlotLeftClick;
        slot.onBeginDragExternal = OnSlotBeginDrag;
        slot.onDragExternal = OnSlotDrag;
        slot.onEndDragExternal = OnSlotEndDrag;
        slot.onSlotRightClick += OnSlotRightClick;

        grid[x, y] = slot;
    }

    #region 箱子事件处理
    private void OnSlotLeftClick(Slot slot, PointerEventData eventData)
    {
        if (slot.occupiedBy.item == null)
            SelectItem(new InventoryItem());
        else
            SelectItem(slot.occupiedBy);
    }

    private void OnSlotRightClick(Slot slot)
    {
        // 可扩展使用物品
    }

    private void OnSlotBeginDrag(Slot slot, PointerEventData eventData)
    {
        if (slot.occupiedBy.item == null) return;
        draggingItem = slot.occupiedBy;
        slot.dragFllowImage.gameObject.SetActive(true);
        slot.dragFllowImage.sprite = slot.occupiedBy.item.icon;
        Cursor.visible = false;
    }

    private void OnSlotDrag(Slot slot, PointerEventData eventData)
    {
        if (draggingItem.item == null) return;
        slot.dragFllowImage.transform.position = Input.mousePosition;

        Slot target = GetSlotUnderMouse(eventData);
        float w = target != null ? target.cellWidth : slot.cellWidth;
        float h = target != null ? target.cellHeight : slot.cellHeight;
        slot.dragRect.sizeDelta = new Vector2(draggingItem.Width * w, draggingItem.Height * h);

        UpdateHighlightFromDrag(eventData);
    }

    private void OnSlotEndDrag(Slot slot, PointerEventData eventData)
    {
        if (draggingItem.item == null) return;
        slot.dragFllowImage.gameObject.SetActive(false);
        Cursor.visible = true;

        EndDragFromChest(draggingItem, eventData);
        draggingItem = new InventoryItem();
        ClearChestHighlight();
        RefreshUI();
        if (BackpackManage.Instance != null)
            BackpackManage.Instance.UpdateBackpack();
    }

    private void UpdateHighlightFromDrag(PointerEventData eventData)
    {
        if (Time.time - lastCheckTime < checkInterval && Vector2.Distance(Input.mousePosition, lastMousePos) < 5f) return;
        lastMousePos = Input.mousePosition;
        lastCheckTime = Time.time;

        Slot targetSlot = GetSlotUnderMouse(eventData);
        if (targetSlot != null)
        {
            lastHightSlot = targetSlot;
            UpdateHighlight(draggingItem, targetSlot);
        }
        else if (IsPointInChestArea(Input.mousePosition, eventData) && lastHightSlot != null)
            UpdateHighlight(draggingItem, lastHightSlot);
        else
            ClearChestHighlight();
    }
    #endregion

    #region 高亮逻辑
    private void UpdateHighlight(InventoryItem item, Slot slot)
    {
        bool canPlace = CanPlace(item, slot.x, slot.y, slot.parentGrid, out int placeX, out int placeY);
        canPlaceAtHighlight = canPlace;
        highlightGrid = slot.parentGrid;
        highlightX = placeX;
        highlightY = placeY;
        StartHighlight(item, highlightGrid, highlightX, highlightY, canPlace);
    }

    private bool CanPlace(InventoryItem item, int centerX, int centerY, Slot[,] grid, out int finalX, out int finalY)
    {
        int w = item.Width, h = item.Height;
        int gw = grid.GetLength(0), gh = grid.GetLength(1);
        finalX = centerX; finalY = centerY;

        int startX = Mathf.Max(0, centerX - w + 1);
        int endX = Mathf.Min(gw - w, centerX);
        int startY = Mathf.Max(0, centerY - h + 1);
        int endY = Mathf.Min(gh - h, centerY);

        Vector2Int[] order = new Vector2Int[]
        {
            new(0,0), new(1,0), new(1,1), new(0,1), new(-1,1),
            new(-1,0), new(-1,-1), new(0,-1), new(1,-1)
        };

        foreach (var offset in order)
        {
            int x = centerX + offset.x;
            int y = centerY + offset.y;
            int fx = Mathf.Clamp(x - (centerX - x), startX, endX);
            int fy = Mathf.Clamp(y - (centerY - y), startY, endY);

            bool containsCenter = (fx <= centerX && fx + w > centerX && fy <= centerY && fy + h > centerY);
            if (!containsCenter) continue;
            if (fx < 0 || fy < 0 || fx + w > gw || fy + h > gh) continue;

            bool ok = true;
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    Slot s = grid[fx + dx, fy + dy];
                    if (s.occupiedBy.item != null && !s.occupiedBy.Equals(item))
                    { ok = false; break; }
                }
            if (ok) { finalX = fx; finalY = fy; return true; }
        }
        return false;
    }

    private void StartHighlight(InventoryItem item, Slot[,] g, int x, int y, bool ok)
    {
        ClearHighlight();
        int w = item.Width, h = item.Height;
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                int gx = x + dx, gy = y + dy;
                if (gx >= 0 && gx < g.GetLength(0) && gy >= 0 && gy < g.GetLength(1))
                {
                    Slot s = g[gx, gy];
                    s.SetHighlight(true, ok ? Color.green : Color.red);
                    currentHighlightSlots.Add(s);
                }
            }
    }

    private void ClearHighlight()
    {
        foreach (var s in currentHighlightSlots) s.SetHighlight(false, default);
        currentHighlightSlots.Clear();
    }

    public void ClearChestHighlight()
    {
        ClearHighlight();
        highlightGrid = null;
        canPlaceAtHighlight = false;
        lastHightSlot = null;
    }
    #endregion

    #region 物品选择（高亮边框）
    public void SelectItem(InventoryItem item)
    {
        if (selectedItem.item != null)
        {
            Slot[,] grid = GetGridByTypeAndIndex(selectedItem.gridType, selectedItem.gridIndex);
            if (grid != null)
            {
                for (int dy = 0; dy < selectedItem.Height; dy++)
                    for (int dx = 0; dx < selectedItem.Width; dx++)
                    {
                        int gx = selectedItem.x + dx;
                        int gy = selectedItem.y + dy;
                        if (gx >= 0 && gx < grid.GetLength(0) && gy >= 0 && gy < grid.GetLength(1))
                            grid[gx, gy].selectBorder.gameObject.SetActive(false);
                    }
            }
        }

        if (item.item == null || item.Equals(selectedItem))
        {
            selectedItem = new InventoryItem();
            return;
        }

        selectedItem = item;
        Slot[,] g2 = GetGridByTypeAndIndex(selectedItem.gridType, selectedItem.gridIndex);
        if (g2 != null)
        {
            for (int dy = 0; dy < selectedItem.Height; dy++)
                for (int dx = 0; dx < selectedItem.Width; dx++)
                {
                    int gx = selectedItem.x + dx;
                    int gy = selectedItem.y + dy;
                    if (gx >= 0 && gx < g2.GetLength(0) && gy >= 0 && gy < g2.GetLength(1))
                        g2[gx, gy].selectBorder.gameObject.SetActive(true);
                }
        }
    }
    #endregion

    #region 移动/丢弃逻辑
    private void EndDragFromChest(InventoryItem item, PointerEventData eventData)
    {
        if (IsPointInBackpackArea(Input.mousePosition, eventData))
        {
            PlayerBackpack playerBackpack = BackpackManage.Instance.playerBackpack;
            bool placed = false;

            // 如果有高亮且可放置，则放到高亮位置
            if (highlightGrid != null && canPlaceAtHighlight)
            {
                if (BackpackManage.Instance.TryGetGridInfo(highlightGrid, out string type, out int idx))
                {
                    playerBackpack.AddItemAtPosition(item.item, item.amount, type, idx, highlightX, highlightY, item.isRotated);
                    placed = true;
                }
            }

            // 没有高亮或获取类型失败，回退到自动寻找空位
            if (!placed)
            {
                playerBackpack.AddItem(item.item, item.amount);
            }

            // 从箱子中移除原物品
            chestInventory.RemoveItem(item);

            // 清除高亮并刷新UI
            ClearChestHighlight();
            RefreshUI();
            BackpackManage.Instance.UpdateBackpack();
            return;
        }
        else if (highlightGrid != null)
        {
            if (canPlaceAtHighlight) // 可放置
            {
                string t = "";
                int idx = -1;
                if (largeAreaGrids.Contains(highlightGrid)) { t = "Large"; idx = largeAreaGrids.IndexOf(highlightGrid); }
                else if (middleAreaGrids.Contains(highlightGrid)) { t = "Middle"; idx = middleAreaGrids.IndexOf(highlightGrid); }
                else if (smallAreaGrids.Contains(highlightGrid)) { t = "Small"; idx = smallAreaGrids.IndexOf(highlightGrid); }

                chestInventory.MoveItem(item, t, idx, highlightX, highlightY, item.isRotated);
            }
            else // 不可放置
            {
                ClearHighlightExternal();
                ClearHighlight();
                highlightGrid = null;
                canPlaceAtHighlight = false;
                lastHightSlot = null;
                draggingItem = new InventoryItem();
                return;
            }
        }
        else
        {
            chestInventory.RemoveItem(item);
            DropItemToWorld(item);
        }
        RefreshUI();
    }

    private void DropItemToWorld(InventoryItem item)
    {
        Debug.Log($"丢弃物品 {item.item.name} 到世界");
        // 实例化掉落物
    }
    #endregion

    #region UI 刷新与工具
    public void RefreshUI()
    {
        if (chestInventory == null) return;
        ClearAllGrids();
        foreach (var item in chestInventory.items)
        {
            Slot[,] grid = GetGridByTypeAndIndex(item.gridType, item.gridIndex);
            if (grid == null || item.item == null) continue;
            for (int dy = 0; dy < item.Height; dy++)
                for (int dx = 0; dx < item.Width; dx++)
                {
                    int gx = item.x + dx, gy = item.y + dy;
                    if (gx >= 0 && gx < grid.GetLength(0) && gy >= 0 && gy < grid.GetLength(1))
                        grid[gx, gy].SetOccupiedBy(item);
                }
        }
        if (selectedItem.item != null)
            SelectItem(selectedItem);
    }

    private void ClearAllGrids()
    {
        foreach (var g in largeAreaGrids) ClearGrid(g);
        foreach (var g in middleAreaGrids) ClearGrid(g);
        foreach (var g in smallAreaGrids) ClearGrid(g);
    }

    private void ClearGrid(Slot[,] grid)
    {
        for (int y = 0; y < grid.GetLength(1); y++)
            for (int x = 0; x < grid.GetLength(0); x++)
                grid[x, y].ClearOccupied();
    }

    private Slot[,] GetGridByTypeAndIndex(string gridType, int index)
    {
        return gridType switch
        {
            "Large" => index >= 0 && index < largeAreaGrids.Count ? largeAreaGrids[index] : null,
            "Middle" => index >= 0 && index < middleAreaGrids.Count ? middleAreaGrids[index] : null,
            "Small" => index >= 0 && index < smallAreaGrids.Count ? smallAreaGrids[index] : null,
            _ => null
        };
    }

    private Slot GetSlotUnderMouse(PointerEventData e) => e.pointerCurrentRaycast.gameObject?.GetComponent<Slot>();
    public bool IsPointInChestArea(Vector2 p, PointerEventData e)
    {
        if (chestAreaRect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(chestAreaRect, p, e.pressEventCamera);
    }

    private bool IsPointInBackpackArea(Vector2 p, PointerEventData e)
    {
        if (BackpackManage.Instance == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(BackpackManage.Instance.areaRect, p, e.pressEventCamera);
    }
    #endregion

    #region 打开/关闭箱子
    public void OpenChest()
    {
        if (BackpackManage.currentOpenChest != null && BackpackManage.currentOpenChest != this)
            BackpackManage.currentOpenChest.CloseChest();

        if (chestPanel == null) return;
        chestPanel.SetActive(true);
        RefreshUI();
        BackpackManage.currentOpenChest = this;

        BackpackManage.Instance.InputBOpen();
        RectTransform rect = BackpackManage.Instance.GetComponent<RectTransform>();

        // 修改缩放
        rect.localScale = new Vector3(0.65f, 0.65f, 0.65f);

        // 修改左右位置（锚点为父物体边界时，offsetMin.x 为左边距，offsetMax.x 为右边距）
        // 例如：左边距 100，右边距 200
        rect.offsetMin = new Vector2(-550, rect.offsetMin.y);   // 设置左边缘距离父物体左边的距离
        rect.offsetMax = new Vector2(-550, rect.offsetMax.y);
    }

    public void CloseChest()
    {
        if (chestPanel != null)
            chestPanel.SetActive(false);
        if (BackpackManage.currentOpenChest == this)
            BackpackManage.currentOpenChest = null;

        BackpackManage.Instance.InputBOpen();
        RectTransform rect = BackpackManage.Instance.GetComponent<RectTransform>();

        // 修改缩放
        rect.localScale = new Vector3(1, 1, 1);

        // 修改左右位置（锚点为父物体边界时，offsetMin.x 为左边距，offsetMax.x 为右边距）
        // 例如：左边距 100，右边距 200
        rect.offsetMin = new Vector2(0, rect.offsetMin.y);   // 设置左边缘距离父物体左边的距离
        rect.offsetMax = new Vector2(0, rect.offsetMax.y);
    }
    #endregion

    // 在 ChestUI 类中添加

    /// <summary> 供外部（背包）调用：根据鼠标位置检测能否放置，并高亮 </summary>
    public bool TryGetPlacementFromExternal(InventoryItem item, PointerEventData eventData,
        out string gridType, out int gridIndex, out int x, out int y)
    {
        gridType = null; gridIndex = -1; x = y = -1;

        Slot targetSlot = GetSlotUnderMouse(eventData);
        if (targetSlot != null)
        {
            if (CanPlace(item, targetSlot.x, targetSlot.y, targetSlot.parentGrid, out int placeX, out int placeY))
            {
                StartHighlight(item, targetSlot.parentGrid, placeX, placeY, true);
                // 获取所属区域类型和索引
                if (largeAreaGrids.Contains(targetSlot.parentGrid))
                {
                    gridType = "Large";
                    gridIndex = largeAreaGrids.IndexOf(targetSlot.parentGrid);
                }
                else if (middleAreaGrids.Contains(targetSlot.parentGrid))
                {
                    gridType = "Middle";
                    gridIndex = middleAreaGrids.IndexOf(targetSlot.parentGrid);
                }
                else if (smallAreaGrids.Contains(targetSlot.parentGrid))
                {
                    gridType = "Small";
                    gridIndex = smallAreaGrids.IndexOf(targetSlot.parentGrid);
                }
                x = placeX; y = placeY;
                return true;
            }
            else
            {
                // 不可放置，显示红色高亮
                StartHighlight(item, targetSlot.parentGrid, targetSlot.x, targetSlot.y, false);
            }
        }
        else if (IsPointInChestArea(Input.mousePosition, eventData) && lastHightSlot != null)
        {
            // 鼠标在箱子区域内但没有具体格子，沿用上一个格子
            if (CanPlace(item, lastHightSlot.x, lastHightSlot.y, lastHightSlot.parentGrid, out int placeX, out int placeY))
            {
                StartHighlight(item, lastHightSlot.parentGrid, placeX, placeY, true);
                if (largeAreaGrids.Contains(lastHightSlot.parentGrid))
                {
                    gridType = "Large";
                    gridIndex = largeAreaGrids.IndexOf(lastHightSlot.parentGrid);
                }
                else if (middleAreaGrids.Contains(lastHightSlot.parentGrid))
                {
                    gridType = "Middle";
                    gridIndex = middleAreaGrids.IndexOf(lastHightSlot.parentGrid);
                }
                else if (smallAreaGrids.Contains(lastHightSlot.parentGrid))
                {
                    gridType = "Small";
                    gridIndex = smallAreaGrids.IndexOf(lastHightSlot.parentGrid);
                }
                x = placeX; y = placeY;
                return true;
            }
        }
        // 无有效放置点，清除高亮
        ClearChestHighlight();
        return false;
    }

    /// <summary> 清除箱子内所有高亮 </summary>
    public void ClearHighlightExternal()
    {
        ClearChestHighlight();
    }
}