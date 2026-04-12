using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System;

// 单个格子的脚本，复制自身修改，BackpackManage.cs遍历全体格子形成整体一起调用
public class Slot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI组件")]
    public TMP_Text count;
    public Image icon;
    public Image selectBorder;
    public Image dragFllowImage;
    public Image highlightImage;
    GridLayoutGroup grid;
    RectTransform iconRect;
    public RectTransform dragRect;
    Image selfImage;


    [HideInInspector]
    public int x, y; // 格子坐标（在BackpackManage.cs中判定）
    [HideInInspector]
    public Slot[,] parentGrid; // 在背包中的哪种区域（大、中、小）
    [HideInInspector]
    public InventoryItem occupiedBy; // 物品占用格子
    [HideInInspector]
    public Action<Slot> onSlotRightClick; // 右击格子事件委托

    public float cellWidth, cellHeight;    // 格子宽高，调用GridLayoutGroup中的cellSize

    // 外部可覆盖的事件（用于箱子）
    public Action<Slot, PointerEventData> onLeftClick;
    public Action<Slot, PointerEventData> onBeginDragExternal;
    public Action<Slot, PointerEventData> onDragExternal;
    public Action<Slot, PointerEventData> onEndDragExternal;

    void Awake()
    {
        // 控制初始状态
        dragFllowImage.gameObject.SetActive(false);
        highlightImage.gameObject.SetActive(false);
        selectBorder.gameObject.SetActive(false);
        icon.enabled = false;
        
        // 获取组件
        grid = GetComponentInParent<GridLayoutGroup>();
        iconRect = icon.GetComponent<RectTransform>();
        dragRect = dragFllowImage.GetComponent<RectTransform>();
        selfImage = GetComponent<Image>();


        // 获取单元格宽高，以便拉伸icon
        cellWidth = grid.cellSize.x;
        cellHeight = grid.cellSize.y;

        // 关闭射线探测，防干扰
        icon.raycastTarget = false;
        count.raycastTarget = false;
        selectBorder.raycastTarget = false;
    }

    public void UpdateUI()
    {
        icon.enabled = false;
        count.text = "";

        if (occupiedBy.item == null || occupiedBy.x != x || occupiedBy.y != y) return; // 格子没有物品、不是第一个格子时跳过（将物品尺寸的左上角第一个的格子icon拉伸至尺寸大小）

            // 读取物品信息设置icon
            icon.sprite = occupiedBy.item.icon;
            icon.enabled = true;

            count.text = occupiedBy.amount > 1 ? occupiedBy.amount.ToString() : "";

            // 拉伸icon
            iconRect.anchorMin = new Vector2(0, 1);
            iconRect.anchorMax = new Vector2(0, 1);
            iconRect.pivot = new Vector2(0, 1);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(occupiedBy.Width * cellWidth, occupiedBy.Height * cellHeight);
    }
    
    // 设置格子被占用，格子读取传进的item数据
    public void SetOccupiedBy(InventoryItem item)
    {
        occupiedBy = item;
        UpdateUI();
    }

    // 清空格子被占用
    public void ClearOccupied()
    {
        occupiedBy = new InventoryItem(); //即occupiedBy = null;
        UpdateUI();
    }

    public void SetHighlight(bool active, Color color) // 高亮格子（BackpackManage.cs中判定）
    {
        highlightImage.gameObject.SetActive(active);
        if (active) highlightImage.color = color;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (onLeftClick != null)
                onLeftClick(this, eventData);
            else if (occupiedBy.item != null)
                BackpackManage.Instance.SelectItem(occupiedBy);
            else
                BackpackManage.Instance.SelectItem(new InventoryItem());
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            onSlotRightClick?.Invoke(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (occupiedBy.item == null) return; // 空白格子跳过

        if (onBeginDragExternal != null)
            onBeginDragExternal(this, eventData);
        else
        {
            BackpackManage.Instance.StartDrag(occupiedBy); //对应BackpackManage.cs的拖拽

            selfImage.raycastTarget = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (occupiedBy.item == null) return; // 空白格子跳过

        if (onDragExternal != null)
            onDragExternal(this, eventData);

        else
        {
            // 显示拖拽图标
            dragFllowImage.gameObject.SetActive(true);
            dragFllowImage.sprite = occupiedBy.item.icon;
            dragFllowImage.transform.position = Input.mousePosition;

            var target = BackpackManage.Instance.GetSlotUnderMouse(eventData);
            float w = target != null ? target.cellWidth : cellWidth;
            float h = target != null ? target.cellHeight : cellHeight;

            dragRect.sizeDelta = new Vector2(occupiedBy.Width * w, occupiedBy.Height * h); // 拖拽图标的尺寸拉伸

            BackpackManage.Instance.OnDrag(occupiedBy, eventData); // 对应BackpackManage.cs的拖拽

            Cursor.visible = false; // 隐藏鼠标
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (occupiedBy.item == null) return; // 空白格子跳过
        if (onEndDragExternal != null)
            onEndDragExternal(this, eventData);
        else
        {
            // 隐藏拖拽图标、恢复鼠标可见、清空选中格子
            dragFllowImage.gameObject.SetActive(false);
            BackpackManage.Instance.SelectItem(new InventoryItem());
            Cursor.visible = true;

            BackpackManage.Instance.EndDrag(occupiedBy, eventData); // 对应BackpackManage.cs的拖拽

            selfImage.raycastTarget = true;
        }
    }
}