using System.Collections;
using System.Collections.Generic;
using Mirror;
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
        backpack = NetworkClient.localPlayer.GetComponent<PlayerBackpack>();

        for (int i = 0; i < itemData.Length; i++)
        {
            backpack.AddItem(itemData[i], 5);
        }
    }
}
