using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Equipment,  // 着身装备(盔甲、武器、背包)
    Consumable, // 消耗品
    Material    // 材料
}

public class ItemData : ScriptableObject
{
    [Header("基础信息")]
    public string itemName;        // 物品名
    public int id;                 // 物品ID
    public Sprite icon;            // 图标
    public int maxStack;       // 最大堆叠数
    public bool isStackable; // 是否可堆叠

    [Header("背包属性")]
    public int width;          // 占用格子宽度
    public int height;         // 占用格子高度
    public float weight;           // 重量
    public ItemType itemType; 


    public virtual void Use(Player user)
    {
        Debug.Log("使用物品：" + itemName);
    }


}
