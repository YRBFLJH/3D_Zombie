using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// 开局注册所有物品
public class RegisterItem : MonoBehaviour
{
    void Awake()
    {
        ItemData[] allItems = Resources.LoadAll<ItemData>("CreateAssetMenu");
        foreach (var item in allItems)
        {
            BackpackManage.RegisterItem(item);
            Debug.Log($"注册物品: {item.itemName} (ID={item.id})");
        }
    }
}
