using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackpackManage : MonoBehaviour
{
    public static BackpackManage Instance;
    public BackpackData backpackData; // 背包数据

    public Transform slotContainer; // 背包逻辑格子父物体    统一管理背包格子区域：大、中、小，大的固定一个（序号0），中的、小的按情况派后

    public GameObject slotPrefab; // 背包格子预制体
    private List<GameObject> slots = new List<GameObject>(); // 存储格子实例的列表

    void Foreach()
    {
        for (int i = 0; i < 30; i++)
        {
            
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

    }

    void Start()
    {
        InitBackpack();
    }


    void InitBackpack()
    {
        for (int i = 0; i < 30; i++)
        {

            GameObject slotObj = Instantiate(slotPrefab, slotContainer);
            slots.Add(slotObj);
            Slot slot = slotObj.GetComponent<Slot>();

            // 订阅格子委托事件
            slot.onSlotRightClick += RightClickSlot;
        }
    }

    // 右键点击格子事件
    void RightClickSlot(Slot slot)
    {
        if (slot.currentItem != null)
        {
            slot.currentItem.Use(Player.instance);
        }
    }


    // 格子交换、合并逻辑
    public void SwapSlots(Slot slotA, Slot slotB)
    {
        if(slotB.currentItem == null)
        {
            slotB.SetSlot(slotA.currentItem, slotA.currentAmount);
            slotA.ClearSlot();
            return;
        }

        if (slotB.currentItem != null && slotA.currentItem.id == slotB.currentItem.id && slotA.currentItem.isStackable) // 同种可堆叠物品合并
        {
            int total = slotA.currentAmount + slotB.currentAmount;
            if (total <= slotA.currentItem.maxStack)
            {
                // 全部合并到B，清空A
                slotB.SetSlot(slotB.currentItem, total);
                slotA.ClearSlot();
            }
            else
            {
                // 超过最大堆叠，B满，A剩余
                slotB.SetSlot(slotB.currentItem, slotB.currentItem.maxStack);
                slotA.SetSlot(slotA.currentItem, total - slotB.currentItem.maxStack);
            }
            slotA.UpdateUI();
            slotB.UpdateUI();
        }
        else
        {
            ItemData tempItem = slotA.currentItem;
            int tempAmount = slotA.currentAmount;

            slotA.SetSlot(slotB.currentItem, slotB.currentAmount);
            slotB.SetSlot(tempItem, tempAmount);
        }
    }

    public void AddItem(ItemData item, int amount)
    {
        // 先尝试堆叠到已有的同种物品格子
        foreach (GameObject slotObj in slots)
        {
            Slot slot = slotObj.GetComponent<Slot>();
            if (slot.currentItem != null && slot.currentItem.id == item.id && slot.currentItem.isStackable)
            {
                int availableSpace = item.maxStack - slot.currentAmount;
                if (availableSpace > 0)
                {
                    int addAmount = Mathf.Min(availableSpace, amount);
                    slot.SetSlot(item, slot.currentAmount + addAmount);
                    amount -= addAmount;
                    if (amount <= 0) return; // 已经添加完了
                }
            }
        }

        // 如果还有剩余数量，放到空格子里
        foreach (GameObject slotObj in slots)
        {
            Slot slot = slotObj.GetComponent<Slot>();
            if (slot.currentItem == null) // 空格子
            {
                int addAmount = Mathf.Min(item.maxStack, amount);
                slot.SetSlot(item, addAmount);
                amount -= addAmount;
                if (amount <= 0) return; // 已经添加完了
            }
        }

        Debug.Log("背包已满，无法添加更多物品！");
    }
}
