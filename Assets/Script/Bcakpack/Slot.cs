using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System;

public class Slot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI组件")]
    public Image icon;
    public TMP_Text count;
    public GameObject selectBorder;
    public GameObject dragFllowImage;          // 拖拽时跟随鼠标的图标
    public Image highlightImage;               // 拖拽落点高亮图片（需在预制体上添加）

    [Header("数据")]
    public int x, y;
    public Slot[,] parentGrid;
    public InventoryItem occupiedBy;

    public Action<Slot> onSlotRightClick;

    // 当前区域格子尺寸（由 GridLayoutGroup 决定）
    public float cellWidth, cellHeight;

    void Awake()
    {
        dragFllowImage.SetActive(false);
        if (highlightImage != null)
            highlightImage.gameObject.SetActive(false);

        // 获取当前区域格子尺寸（每个区域独立）
        GridLayoutGroup gridGroup = GetComponentInParent<GridLayoutGroup>();
        if (gridGroup != null)
        {
            cellWidth = gridGroup.cellSize.x;
            cellHeight = gridGroup.cellSize.y;
        }

        selectBorder.SetActive(false);
        icon.raycastTarget = false;
        count.raycastTarget = false;
        if (selectBorder != null)
            selectBorder.GetComponent<Image>().raycastTarget = false;

        // 确保格子本身的背景能接收射线（用于点击和拖拽）
        Image bg = GetComponent<Image>();
        if (bg != null) bg.raycastTarget = true;

        UpdateUI();
    }

    #region UI更新
    public void UpdateUI()
    {
        if (occupiedBy != null)
        {
            bool isTopLeft = (occupiedBy.x == x && occupiedBy.y == y);
            if (isTopLeft)
            {
                icon.sprite = occupiedBy.item.icon;
                icon.enabled = true;
                count.text = occupiedBy.amount > 1 ? occupiedBy.amount.ToString() : "";

                // 图标覆盖整个物品区域
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0, 1);
                iconRect.anchorMax = new Vector2(0, 1);
                iconRect.pivot = new Vector2(0, 1);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(occupiedBy.Width * cellWidth, occupiedBy.Height * cellHeight);
            }
            else
            {
                icon.enabled = false;
                count.text = "";
            }
        }
        else
        {
            icon.enabled = false;
            count.text = "";
        }
    }

    public void SetOccupiedBy(InventoryItem item)
    {
        occupiedBy = item;
        UpdateUI();
    }

    public void ClearOccupied()
    {
        occupiedBy = null;
        UpdateUI();
    }
    #endregion

    #region 高亮功能
    public void SetHighlight(bool active, Color color)
    {
        if (highlightImage == null) return;
        highlightImage.gameObject.SetActive(active);
        if (active)
            highlightImage.color = color;
    }
    #endregion

    #region 交互事件
    public void OnPointerClick(PointerEventData eventData)
    {
        if (occupiedBy != null)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                onSlotRightClick?.Invoke(this);
            else if (eventData.button == PointerEventData.InputButton.Left)
                BackpackManage.Instance.SelectItem(occupiedBy);
        }
        else
        {
            BackpackManage.Instance.SelectItem(null);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (occupiedBy == null) return;
        BackpackManage.Instance.StartDrag(occupiedBy);
        GetComponent<Image>().raycastTarget = false;   // 暂时关闭自身射线，避免干扰
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (occupiedBy == null) return;

        // 显示跟随鼠标的图标，并动态调整大小以适应目标区域
        dragFllowImage.SetActive(true);
        dragFllowImage.GetComponent<Image>().sprite = occupiedBy.item.icon;
        dragFllowImage.transform.position = Input.mousePosition;

        // 获取鼠标下方的格子，用于调整图标大小（适应目标区域的格子尺寸）
        Slot targetSlot = BackpackManage.Instance.GetSlotUnderMouse(eventData);
        if (targetSlot != null)
        {
            // 使用目标区域的格子尺寸调整拖拽图标大小
            dragFllowImage.GetComponent<RectTransform>().sizeDelta = new Vector2(
                occupiedBy.Width * targetSlot.cellWidth,
                occupiedBy.Height * targetSlot.cellHeight
            );
        }
        else
        {
            // 没有目标时，使用当前区域的尺寸
            dragFllowImage.GetComponent<RectTransform>().sizeDelta = new Vector2(
                occupiedBy.Width * cellWidth,
                occupiedBy.Height * cellHeight
            );
        }

        BackpackManage.Instance.OnDrag(occupiedBy, eventData);
        Cursor.visible = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (occupiedBy == null) return;
        dragFllowImage.SetActive(false);
        BackpackManage.Instance.SelectItem(null);
        BackpackManage.Instance.EndDrag(occupiedBy, eventData);
        Cursor.visible = true;
        GetComponent<Image>().raycastTarget = true;   // 恢复射线检测
    }
    #endregion
}