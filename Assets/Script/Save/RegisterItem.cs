using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// 开局注册所有物品
public class RegisterItem : MonoBehaviour
{
    void Awake()
    {
        // 运行时启动时重建缓存，避免因脚本域/静态缓存导致旧配置未刷新
        BackpackManage.ClearRegisteredItems();

        ItemData[] allItems = Resources.LoadAll<ItemData>("CreateAssetMenu");
        foreach (var item in allItems)
        {
            BackpackManage.RegisterItem(item);
        }
    }
}
