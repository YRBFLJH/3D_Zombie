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

    bool _visualsReady;

    // 外部可覆盖的事件（用于箱子）
    public Action<Slot, PointerEventData> onLeftClick;
    public Action<Slot, PointerEventData> onBeginDragExternal;
    public Action<Slot, PointerEventData> onDragExternal;
    public Action<Slot, PointerEventData> onEndDragExternal;

    float _backpackDragProbeTime = -999f;
    Vector2 _backpackDragProbeLastMouse;
    Slot _backpackCachedSlotUnderMouse;
    const float BackpackDragProbeSqrDist = 25f;

    void Awake()
    {
        EnsureVisualsInitialized();
    }

    void OnEnable()
    {
        EnsureVisualsInitialized();
    }

    /// <summary>挂在未激活面板下时 Awake 可能尚未执行，需在首次绘制前完成绑定。</summary>
    void EnsureVisualsInitialized()
    {
        if (_visualsReady) return;
        if (dragFllowImage == null || highlightImage == null || selectBorder == null || icon == null || count == null)
            return;

        dragFllowImage.gameObject.SetActive(false);
        highlightImage.gameObject.SetActive(false);
        selectBorder.gameObject.SetActive(false);
        icon.enabled = false;

        grid = GetComponentInParent<GridLayoutGroup>();
        if (grid == null) return;

        iconRect = icon.GetComponent<RectTransform>();
        dragRect = dragFllowImage.GetComponent<RectTransform>();
        selfImage = GetComponent<Image>();
        if (iconRect == null || dragRect == null || selfImage == null) return;

        cellWidth = grid.cellSize.x;
        cellHeight = grid.cellSize.y;

        icon.raycastTarget = false;
        count.raycastTarget = false;
        selectBorder.raycastTarget = false;

        _visualsReady = true;
    }

    public void UpdateUI()
    {
        EnsureVisualsInitialized();
        if (!_visualsReady) return;

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
        EnsureVisualsInitialized();
        if (!_visualsReady || highlightImage == null) return;
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
            {
                if (BackpackManage.Instance != null)
                    BackpackManage.Instance.SelectItem(occupiedBy);
            }
            else if (BackpackManage.Instance != null)
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
            if (BackpackManage.Instance != null)
            {
                _backpackDragProbeTime = -999f;
                _backpackDragProbeLastMouse = Input.mousePosition;
                _backpackCachedSlotUnderMouse = null;
                BackpackManage.Instance.StartDrag(occupiedBy); //对应BackpackManage.cs的拖拽
                selfImage.raycastTarget = false;
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (occupiedBy.item == null) return; // 空白格子跳过

        if (onDragExternal != null)
            onDragExternal(this, eventData);

        else
        {
            if (BackpackManage.Instance == null) return;
            // 显示拖拽图标
            dragFllowImage.gameObject.SetActive(true);
            dragFllowImage.sprite = occupiedBy.item.icon;
            dragFllowImage.transform.position = Input.mousePosition;

            float interval = BackpackManage.Instance.DragProbeInterval;
            bool heavy = Time.unscaledTime - _backpackDragProbeTime >= interval
                || Vector2.SqrMagnitude((Vector2)Input.mousePosition - _backpackDragProbeLastMouse) > BackpackDragProbeSqrDist;
            if (heavy)
            {
                _backpackDragProbeTime = Time.unscaledTime;
                _backpackDragProbeLastMouse = Input.mousePosition;
                _backpackCachedSlotUnderMouse = BackpackManage.Instance.GetSlotUnderMouse(eventData);
            }

            var target = _backpackCachedSlotUnderMouse;
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
            dragFllowImage.gameObject.SetActive(false);
            Cursor.visible = true;
            selfImage.raycastTarget = true;
            if (BackpackManage.Instance != null)
            {
                BackpackManage.Instance.SelectItem(new InventoryItem());
                BackpackManage.Instance.EndDrag(occupiedBy, eventData); // 对应BackpackManage.cs的拖拽
            }
        }
    }
}