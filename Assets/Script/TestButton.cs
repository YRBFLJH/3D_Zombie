using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestButton : MonoBehaviour
{
    public ItemData[] itemData;

    private Button button;
    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnButtonClick);
    }

    
    void OnButtonClick()
    {
        for (int i = 0; i < itemData.Length; i++)
        {
            BackpackManage.Instance.AddItem(itemData[i], 1);
        }
    }
}
