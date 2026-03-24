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
    public GameObject dragFllowImage;
    public Image highlightImage;

    [Header("数据")]
    public int x, y;
    public Slot[,] parentGrid;
    public InventoryItem occupiedBy;

    public Action<Slot> onSlotRightClick;

    public float cellWidth, cellHeight;

    void Awake()
    {
        dragFllowImage.SetActive(false);
        if (highlightImage != null)
            highlightImage.gameObject.SetActive(false);

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

        Image bg = GetComponent<Image>();
        if (bg != null) bg.raycastTarget = true;

        UpdateUI();
    }

    #region UI更新
    public void UpdateUI()
    {
        // 强制清空，防止残留
        icon.enabled = false;
        count.text = "";

        // 安全判断
        if (occupiedBy.item == null)
        {
            icon.enabled = false;
            count.text = "";
            return;
        }

        bool isTopLeft = (occupiedBy.x == x && occupiedBy.y == y);
        if (isTopLeft)
        {
            icon.sprite = occupiedBy.item.icon;
            icon.enabled = true;
            count.text = occupiedBy.amount > 1 ? occupiedBy.amount.ToString() : "";

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

    public void SetOccupiedBy(InventoryItem item)
    {
        occupiedBy = item;
        UpdateUI();
    }

    public void ClearOccupied()
    {
        occupiedBy = new InventoryItem();
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
        if (occupiedBy.item != null)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                onSlotRightClick?.Invoke(this);
            else if (eventData.button == PointerEventData.InputButton.Left)
                BackpackManage.Instance.SelectItem(occupiedBy);
        }
        else
        {
            BackpackManage.Instance.SelectItem(new InventoryItem());
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (occupiedBy.item == null) return;
        BackpackManage.Instance.StartDrag(occupiedBy);
        GetComponent<Image>().raycastTarget = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (occupiedBy.item == null) return;

        dragFllowImage.SetActive(true);
        dragFllowImage.GetComponent<Image>().sprite = occupiedBy.item.icon;
        dragFllowImage.transform.position = Input.mousePosition;

        Slot targetSlot = BackpackManage.Instance.GetSlotUnderMouse(eventData);
        if (targetSlot != null)
        {
            dragFllowImage.GetComponent<RectTransform>().sizeDelta = new Vector2(
                occupiedBy.Width * targetSlot.cellWidth,
                occupiedBy.Height * targetSlot.cellHeight
            );
        }
        else
        {
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
        if (occupiedBy.item == null) return;
        dragFllowImage.SetActive(false);
        BackpackManage.Instance.SelectItem(new InventoryItem());
        BackpackManage.Instance.EndDrag(occupiedBy, eventData);
        Cursor.visible = true;
        GetComponent<Image>().raycastTarget = true;
        
        // 拖动结束强制刷新整个背包（关键修复）
        BackpackManage.Instance.RefreshAllGrids();
    }
    #endregion
}