using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackManage : MonoBehaviour
{
    public static BackpackManage Instance;

    [Header("背包配置")]
    public BackpackData backpackData; // 背包数据
    public Transform backpackArea; // 背包区域（此区域外为背包外，即物品丢弃区域）
    public Slot slotPrefab; // 格子预制体(直接设置为Slot格式，避免实例后再GetComponent，优化性能)

    [Header("大、中、小各区域")]
    public Transform[] largeArea;
    public Transform[] middleArea;
    public Transform[] smallArea;

    [Header("UI 控制")]
    public GameObject showBackpack; // 背包UI

    // 各区域内格子数组集合（知道区域内格子数量、序号）
    List<Slot[,]> largeAreaGrids = new List<Slot[,]>();
    List<Slot[,]> middleAreaGrids = new List<Slot[,]>();
    List<Slot[,]> smallAreaGrids = new List<Slot[,]>();

    private PlayerBackpack playerBackpack;
    private Player localPlayer;

    private InventoryItem currentSelectedItem;  // 当前选中物品
    private InventoryItem draggingItem; // 拖拽物品实际的物品数据

    // 高亮格子
    private Slot[,] highlightGrid;
    private int highlightX, highlightY; // 当前高亮格子坐标（物品移动更换位置的标准）
    private List<Slot> currentHighlightSlots = new List<Slot>(); // 当前高亮格子区域
    private bool canPlaceAtHighlight;
    private Slot lastHightSlot; // 记录上一次可放置的高亮格子（防止闪烁）

    private RectTransform areaRect;
    private Vector2 lastMousePos;

    // 高亮检查间隔（防止每帧检测进行可忽略的没必要作用，性能优化）
    private float checkInterval = 0.05f;
    private float lastCheckTime;

    void Awake()
    {
        Instance = this;

        // 获取组件
        areaRect = backpackArea.GetComponent<RectTransform>();

        // 控制UI状态
        showBackpack.SetActive(false);
    }


    public void GetComponent() // 外部获取联机对象组件方法，若是人物自身脚本可直接GetComponent
    {
        localPlayer = NetworkClient.localPlayer.GetComponent<Player>();
        playerBackpack = NetworkClient.localPlayer.GetComponent<PlayerBackpack>();
    }

    void Start()
    {
        InitBackpack();
        GetComponent();
        playerBackpack.OnInventoryChanged += UpdateBackpack;
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B)) // 开关背包
        {
            SelectItem(new InventoryItem());
            showBackpack.SetActive(!showBackpack.activeSelf);
            if (showBackpack.activeSelf) // 背包打开时更新背包
                UpdateBackpack();
        }

        if (currentSelectedItem.item != null && Input.GetKeyDown(KeyCode.R)) // 选定并旋转
            RotateSelectedItem();
    }

    void InitBackpack() // 初始化背包
    {
        int largeW = backpackData.largeWidth;
        int largeH = backpackData.largeHeight;
        foreach (Transform area in largeArea)
        {
            Slot[,] grid = new Slot[largeW, largeH];
            for (int y = 0; y < largeH; y++)
                for (int x = 0; x < largeW; x++)
                    SpawnSlot(area, x, y, grid);
            largeAreaGrids.Add(grid);
        }


        int middleW = backpackData.middleWidth;
        int middleH = backpackData.middleHeight;
        foreach (Transform area in middleArea)
        {
            Slot[,] grid = new Slot[middleW, middleH];
            for (int y = 0; y < middleH; y++)
                for (int x = 0; x < middleW; x++)
                    SpawnSlot(area, x, y, grid);
            middleAreaGrids.Add(grid);
        }


        int smallW = backpackData.smallWidth;
        int smallH = backpackData.smallHeight;
        foreach (Transform area in smallArea)
        {
            Slot[,] grid = new Slot[smallW, smallH];
            for (int y = 0; y < smallH; y++)
                for (int x = 0; x < smallW; x++)
                    SpawnSlot(area, x, y, grid);
            smallAreaGrids.Add(grid);
        }
    }

    // 生成格子
    void SpawnSlot(Transform parent, int x, int y, Slot[,] grid)
    {
        Slot slot = Instantiate(slotPrefab, parent);

        slot.x = x;
        slot.y = y;
        slot.parentGrid = grid;
        slot.onSlotRightClick += RightClickSlot;
        grid[x, y] = slot;
    }

    // 刷新UI( 清除所有格子，遍历背包物品，将物品放置到格子中。可优化？)
    public void UpdateBackpack()
    {
        ClearAllGrids();

        foreach (var item in playerBackpack.items)
        {
            Slot[,] grid = GetGridByTypeAndIndex(item.gridType, item.gridIndex);
            if (grid == null || item.item == null) continue;

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
        foreach (var i in largeAreaGrids) ClearGrid(i);
        foreach (var i in middleAreaGrids) ClearGrid(i);
        foreach (var i in smallAreaGrids) ClearGrid(i);
    }
    void ClearGrid(Slot[,] grid)
    {
        for (int y = 0; y < grid.GetLength(1); y++) //grid.GetLength(1) ：高度（有多少行）
            for (int x = 0; x < grid.GetLength(0); x++) //grid.GetLength(0) ：宽度（有多少列）
                grid[x, y].ClearOccupied();
    }


    Slot[,] GetGridByTypeAndIndex(string gridType, int index)  // 根据格子所在区域类型和索引获取格子
    {
        return gridType switch
        {
            "Large" => index >= 0 && index < largeAreaGrids.Count ? largeAreaGrids[index] : null,
            "Middle" => index >= 0 && index < middleAreaGrids.Count ? middleAreaGrids[index] : null,
            "Small" => index >= 0 && index < smallAreaGrids.Count ? smallAreaGrids[index] : null,
            _ => null
        };
    }

    public void StartDrag(InventoryItem item)
    {
        draggingItem = item; // 用定义的变量缓存当前正在拖动的物品数据，让系统识别到拖动物品
        highlightGrid = null;
        canPlaceAtHighlight = false;
        lastHightSlot = null;
    }

    public void OnDrag(InventoryItem item, PointerEventData eventData) // 在Slot的OnDrag(PointerEventData eventData)里调用，即拖拽过程中每帧调用
    {
        if (Time.time - lastCheckTime < checkInterval && Vector2.Distance(Input.mousePosition, lastMousePos) < 5f) return; // 防止频繁检查

        lastMousePos = Input.mousePosition;
        lastCheckTime = Time.time;

        Slot targetSlot = GetSlotUnderMouse(eventData); // 实时检测鼠标下的格子

        if (targetSlot != null)
        {
            lastHightSlot = targetSlot;
            UpdateHighlight(item, targetSlot); // 拖动光标检测到格子时更新高亮
        }
        else if (IsPointInAnyBackpackArea(Input.mousePosition, eventData) && lastHightSlot != null)
            UpdateHighlight(item, lastHightSlot); // 拖动光标在背包区域且已经存在高亮时更新高亮
        else // 拖动光标不在背包区域且没有高亮时清除高亮
            ClearHighlight();
    }

    // 更新高亮
    void UpdateHighlight(InventoryItem item, Slot slot)
    {
        // 判断格子是否可以放置物品
        bool canPlace = CanPlace(item, slot.x, slot.y, slot.parentGrid, out int placeX, out int placeY); // 在OnDrag中实时检测

        canPlaceAtHighlight = canPlace;
        highlightGrid = slot.parentGrid;
        highlightX = placeX;
        highlightY = placeY;

        StartHighlight(item, highlightGrid, highlightX, highlightY, canPlace);
    }

    // 判断格子是否可以放置物品
    bool CanPlace(InventoryItem item, int centerX, int centerY, Slot[,] grid, out int finalX, out int finalY)
    {
        int w = item.Width;
        int h = item.Height;
        int gw = grid.GetLength(0);
        int gh = grid.GetLength(1);

        finalX = centerX;
        finalY = centerY;

        // 中心格子（物品左上角格子）
        int startX = Mathf.Max(0, centerX - w + 1);
        int endX = Mathf.Min(gw - w, centerX);
        int startY = Mathf.Max(0, centerY - h + 1);
        int endY = Mathf.Min(gh - h, centerY);

        // 3*3范围
        Vector2Int[] order = new Vector2Int[]
        {
            new(0, 0),    // 中心
            new(1, 0),    // 右
            new(1, 1),    // 右下
            new(0, 1),    // 下
            new(-1, 1),   // 左下
            new(-1, 0),   // 左
            new(-1, -1),  // 左上
            new(0, -1),   // 上
            new(1, -1),   // 右上
        };

        foreach (var offset in order)
        {
            int x = centerX + offset.x;
            int y = centerY + offset.y;

            // 物品必须包含 (centerX, centerY) 这个格子（中心格子）
            int fx = x - (centerX - x); 
            int fy = y - (centerY - y);

            fx = Mathf.Clamp(fx, startX, endX);
            fy = Mathf.Clamp(fy, startY, endY);

            // 检查物品范围是否包含中心格子
            bool containsCenter = (fx <= centerX && fx + w > centerX && fy <= centerY && fy + h > centerY);
            if (!containsCenter) continue;

            // 边界检查
            if (fx < 0 || fy < 0 || fx + w > gw || fy + h > gh)
                continue;

            // 检查是否全部空格子
            bool ok = true;
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    Slot s = grid[fx + dx, fy + dy];
                    if (s.occupiedBy.item != null && !s.occupiedBy.Equals(item))
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok) break;
            }

            if (ok)
            {
                finalX = fx;
                finalY = fy;
                return true;
            }
        }

        return false;
    }
    void StartHighlight(InventoryItem item, Slot[,] g, int x, int y, bool ok) // 开始发亮（调用Slot的SetHighlight）
    {
        ClearHighlight(); // 清除所有高亮

        int w = item.Width;
        int h = item.Height;

        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                int gx = x + dx;
                int gy = y + dy;

                if (gx >= 0 && gx < g.GetLength(0) && gy >= 0 && gy < g.GetLength(1))
                {
                    Slot s = g[gx, gy];
                    s.SetHighlight(true, ok ? Color.green : Color.red); // 高亮放置根据传进的值（ok ： CanPlace(InventoryItem item, int x, int y, Slot[,] grid)）判断亮什么颜色
                    currentHighlightSlots.Add(s); // 方便清除
                }
            }
        }
    }

    // 清除所有高亮
    void ClearHighlight()
    {
        foreach (var s in currentHighlightSlots) s.SetHighlight(false, default);
        currentHighlightSlots.Clear();
    }

    public void EndDrag(InventoryItem item, PointerEventData eventData)
    {
        if (!IsPointInAnyBackpackArea(Input.mousePosition, eventData)) // 结束拖动后光标不在背包区域执行丢弃物品方法
        {
            playerBackpack.RemoveItem(item);
            DropItem(item);
        }
        else if (highlightGrid != null && canPlaceAtHighlight) // 存在高亮格子且可以放置（在Ondrag中已实时检测）
        {
            string t = "";
            int idx = -1;

            if (largeAreaGrids.Contains(highlightGrid))       // 查找这些可放置的绿色高亮格子位置，用于物品移动
            {
                t = "Large";
                idx = largeAreaGrids.IndexOf(highlightGrid);
            }
            else if (middleAreaGrids.Contains(highlightGrid))
            {
                t = "Middle";
                idx = middleAreaGrids.IndexOf(highlightGrid);
            }
            else if (smallAreaGrids.Contains(highlightGrid))
            {
                t = "Small";
                idx = smallAreaGrids.IndexOf(highlightGrid);
            }

            playerBackpack.MoveItem(draggingItem, t, idx, highlightX, highlightY, draggingItem.isRotated);
        }

        // 重置高亮
        ClearHighlight();
        highlightGrid = null;
        canPlaceAtHighlight = false;
        lastHightSlot = null;

        draggingItem = new InventoryItem(); // 清空拖动的物品数据

        UpdateBackpack(); // 更新背包UI
    }

    public Slot GetSlotUnderMouse(PointerEventData e) // 获取鼠标下的格子（要知道当前格子类型，不同区域格子大小会不同），以便拖动时的物品图标能随鼠标经过的格子的大小不同而变化
    { 
        return e.pointerCurrentRaycast.gameObject?.GetComponent<Slot>();
    }  
    bool IsPointInAnyBackpackArea(Vector2 p, PointerEventData e) => RectTransformUtility.RectangleContainsScreenPoint(areaRect, p, e.pressEventCamera); // 判断鼠标是否在背包区域

    void DropItem(InventoryItem item) // 丢弃物品,后续添加
    {
        
    }

    // 注册物品，方便通过数据查找 （外部调用，开局自动查找项目内的所有ItmData，自动注册）
    static Dictionary<int, ItemData> itemMap = new(); // 定义
    public static void ClearRegisteredItems()
    {
        itemMap.Clear();
    }
    public static void RegisterItem(ItemData item) // 注册
    { 
        if (item == null) return;

        if (itemMap.TryGetValue(item.id, out var existing) && existing != null)
        {
            itemMap[item.id] = item;
            return;
        }

        itemMap.Add(item.id, item);
    }
    public static ItemData GetItemData(int id)  // 获取
    { 
        itemMap.TryGetValue(id, out var d); return d; 
    }


    public void SelectItem(InventoryItem item)
    {
        // 如果已经有选中物品，先取消选定框（UI）
        if (currentSelectedItem.item != null)
        {
            var g = GetGridByTypeAndIndex(currentSelectedItem.gridType, currentSelectedItem.gridIndex);
            for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                {
                    int gx = currentSelectedItem.x + dx;
                    int gy = currentSelectedItem.y + dy;
                    if (gx >= 0 && gx < g.GetLength(0) && gy >= 0 && gy < g.GetLength(1))
                        g[gx, gy].selectBorder.gameObject.SetActive(false);
                }
        }

        // 如果点击的是已选中的物品或空物品，就清空选中（数据，要配合第一步的取消UI）
        if (currentSelectedItem.item != null && (item.Equals(currentSelectedItem) || item.item == null))
        {
            currentSelectedItem = new InventoryItem();
            return;
        }

        // 否则（即选中了新物品），显示选定
        currentSelectedItem = item;
        if (currentSelectedItem.item != null)
        {
            var g = GetGridByTypeAndIndex(currentSelectedItem.gridType, currentSelectedItem.gridIndex);
            if (g != null)
            {
                for (int dy = 0; dy < currentSelectedItem.Height; dy++)
                    for (int dx = 0; dx < currentSelectedItem.Width; dx++)
                    {
                        int gx = currentSelectedItem.x + dx;
                        int gy = currentSelectedItem.y + dy;
                        if (gx >= 0 && gx < g.GetLength(0) && gy >= 0 && gy < g.GetLength(1))
                            g[gx, gy].selectBorder.gameObject.SetActive(true);
                    }
            }
        }
    }

    void RotateSelectedItem()
    {
        if (currentSelectedItem.item == null) return; // 只有选中物品才能对其旋转

        // 旋转后的物品数据(互换宽高)
        InventoryItem rotatedItem = new InventoryItem
        (
            currentSelectedItem.itemId,
            currentSelectedItem.amount,
            currentSelectedItem.x,
            currentSelectedItem.y,
            currentSelectedItem.gridType,
            currentSelectedItem.gridIndex,
            !currentSelectedItem.isRotated // 是否旋转(宽高互换)
        );

        var g = GetGridByTypeAndIndex(currentSelectedItem.gridType, currentSelectedItem.gridIndex);

        if (CanRotate(rotatedItem, rotatedItem.x, rotatedItem.y, g))
        {
            playerBackpack.MoveItem
            (
                currentSelectedItem,
                currentSelectedItem.gridType,
                currentSelectedItem.gridIndex,
                currentSelectedItem.x,
                currentSelectedItem.y,
                !currentSelectedItem.isRotated
            );

            // 清空选中，再选中旋转后的物体（实现选转选中不变）
            SelectItem(new InventoryItem());
            SelectItem(rotatedItem);
        }
    }

    void RightClickSlot(Slot slot) // 右击（待做）
    {
        if (slot.occupiedBy.item != null)
        {
            playerBackpack.UseItem(slot.occupiedBy, localPlayer);
            UpdateBackpack();
        }
    } 


    // 判断是否可以旋转
    bool CanRotate(InventoryItem item, int x, int y, Slot[,] grid)
    {
        int w = item.Width;
        int h = item.Height;
        int gw = grid.GetLength(0);
        int gh = grid.GetLength(1);

        // 边界判断
        if (x < 0 || y < 0 || x + w > gw || y + h > gh)
            return false;

        // 查看是否有被占用且不属于自身的格子
        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                Slot s = grid[x + dx, y + dy];
                if (s.occupiedBy.item != null && !s.occupiedBy.Equals(item))
                    return false;
            }
        }
        return true;
    }



    // 保存加载
    public InventorySaveData GetSaveData()
    {
        InventorySaveData data = new();
        foreach (var i in playerBackpack.items)
            data.items.Add(new()
            {
                itemId = i.itemId,
                amount = i.amount,
                gridType = i.gridType,
                gridIndex = i.gridIndex,
                x = i.x,
                y = i.y,
                isRotated = i.isRotated
            });
        return data;
    }

    public void LoadInventory(InventorySaveData data)
    {
        foreach (var i in data.items)
        {
            var item = GetItemData(i.itemId);
            if (item != null) playerBackpack.AddItem(item, i.amount);
        }
    }
}