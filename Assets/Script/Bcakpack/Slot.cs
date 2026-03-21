using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using TMPro; 

public class Slot : MonoBehaviour,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
{
    // UI信息
    public Image icon;
    public Image dragIcon; // 拖拽时显示的图标
    private Canvas dragCanvas; // 用于设置拖拽图标的渲染层级
    public TMP_Text count;
    public GameObject selectBorder; // 选中框( 高亮显示 )

    // 数据
    public ItemData currentItem;
    public int currentAmount;

    // 事件委托(委托给背包管理脚本处理逻辑事件，本脚本负责UI更换，做到逻辑与UI分离)
    public Action<Slot> onSlotRightClick;

    public void Awake()
    {
        selectBorder.SetActive(false);
        dragIcon.gameObject.SetActive(false);
        dragCanvas = dragIcon.gameObject.GetComponent<Canvas>();

        // 关闭其余射线检测，防止干扰
        icon.raycastTarget = false;
        dragIcon.raycastTarget = false;
        count.raycastTarget = false;
        selectBorder.GetComponent<Image>().raycastTarget = false;

        UpdateUI();
    }

    public void SetSlot(ItemData item, int amount)
    {
        amount = Mathf.Min(amount, item.maxStack); // 限制堆叠数量不超过最大堆叠数

        currentItem = item;
        currentAmount = amount;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (currentItem != null)
        {
            icon.sprite = currentItem.icon;
            icon.enabled = true;
            count.text = currentAmount > 1 ? currentAmount.ToString() : "";
        }
        else
        {
            ClearSlot();
        }
    }

    public void ClearSlot()
    {
        currentItem = null;
        currentAmount = 0;
        icon.sprite = null;
        icon.enabled = false;
        count.text = "";
    }

    // 点击事件
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentItem != null)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                onSlotRightClick?.Invoke(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                selectBorder.SetActive(!selectBorder.activeSelf);
            }
        }
    }

    // 拖拽事件
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem == null) return; // 没有物品无法拖拽

        GetComponent<Image>().raycastTarget = false; // 关闭射线检测，允许穿透事件到下方格子
    }
    public void OnDrag(PointerEventData eventData)
    {
        Cursor.visible = false;

        dragIcon.gameObject.SetActive(true); // 显示拖拽图标
        dragIcon.sprite = currentItem.icon;
        dragCanvas.sortingOrder = 999; // 确保拖拽图标在最上层
        dragIcon.rectTransform.position = Input.mousePosition; // 跟随鼠标移动
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        Cursor.visible = true;

        dragCanvas.sortingOrder = 0;
        dragIcon.gameObject.SetActive(false); // 隐藏拖拽图标

        GetComponent<Image>().raycastTarget = true; // 恢复射线检测

        // 拖拽结束不同处理（格子物品交换、拖出背包外丢弃）
        GameObject targetObj = eventData.pointerCurrentRaycast.gameObject;

        Debug.Log($"射线命中物体：{targetObj.name}");
        Debug.Log($"该物体是否有 Slot 脚本：{targetObj.GetComponent<Slot>() != null}");
        if (targetObj != null  &&  targetObj.GetComponent<Slot>() != null && targetObj != this)
        {
            Slot targetSlot = targetObj.GetComponent<Slot>();
            BackpackManage.Instance.SwapSlots(this, targetSlot);
            Debug.Log("交换了物品");
        }
        else if (targetObj == this)    //不会进入此分支，但依旧正常运行无报错
        {
            Debug.Log("物品已返回原位");
            return; // 拖回原位不处理
        }
        else if (!IsPointInBackpackArea(Input.mousePosition,eventData))
        {
            ClearSlot();
        }
    }


    // 判断鼠标点是否在背包区域
    bool IsPointInBackpackArea(Vector2 mousePos, PointerEventData eventData)
    {
        return RectTransformUtility.RectangleContainsScreenPoint
        (
            BackpackManage.Instance.slotContainer.GetComponent<RectTransform>(), 
            mousePos, 
            eventData.pressEventCamera
        );   
    }
    
}
