using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class TestButton : MonoBehaviour
{
    PlayerBackpack backpack;
    public ItemData[] itemData;

    private Button button;
    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnButtonClick);
    }
    
    void OnButtonClick()
    {
        // 单机模式：通过查找标签获取本地玩家背包组件
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            backpack = playerObj.GetComponent<PlayerBackpack>();

        if (backpack != null)
        {
            for (int i = 0; i < itemData.Length; i++)
            {
                backpack.AddItem(itemData[i], 5);
            }
        }
    }
}