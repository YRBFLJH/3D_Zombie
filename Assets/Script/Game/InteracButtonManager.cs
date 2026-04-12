using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Mathematics;

public class InteracButtonManager : MonoBehaviour
{
    public static InteracButtonManager Intance;

    public GameObject interactButtonPrefab;
    public GameObject interactButtonParent;

    // 字典管理按钮与对应的箱子之间的关联
    Dictionary<GameObject, GameObject> buttonDict = new Dictionary<GameObject, GameObject>();
    // 按钮列表
    List<GameObject> interactButtonList = new List<GameObject>();

    // 操作按钮
    int selectedIndex = 0;
    Color highlightColor = Color.yellow;
    Color normalColor = Color.white;

    void Update()
    {
        if (interactButtonList.Count < 1)
            return;

        // 滚轮控制选择
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.01f)
        {
            selectedIndex--;
            if (selectedIndex < 0)
                selectedIndex = interactButtonList.Count - 1;

            SelectButton(selectedIndex);
        }
        else if (scroll < -0.01f)
        {
            selectedIndex++;
            if (selectedIndex >= interactButtonList.Count)
                selectedIndex = 0;

            SelectButton(selectedIndex);
        }

        // 按键控制操作
        if (Input.GetKeyDown(KeyCode.E) && selectedIndex >= 0 && selectedIndex < interactButtonList.Count)
            InteractBox();

        // 生成第一个按钮/只剩一个按钮时,对其进行高亮
        if (interactButtonList.Count == 1)
            SelectButton(0);
    }

    void Awake()
    {
        Intance = this;
    }

    public void SpawnInteractButton(GameObject targetBox,string interactText)
    {
        if (buttonDict.ContainsKey(targetBox)) return;

        GameObject interactButton = Instantiate(interactButtonPrefab,interactButtonParent.transform);
        interactButton.GetComponentInChildren<TextMeshProUGUI>().text = interactText;

        buttonDict.Add(targetBox, interactButton);
        interactButtonList.Add(interactButton);
    }

    public void DestroyInteractButton(GameObject targetBox)
    {
        if (buttonDict.ContainsKey(targetBox))
        {
            bool removeSelectedButton = selectedIndex == interactButtonList.IndexOf(buttonDict[targetBox]); // 提前判定删除的是否是目前选中的按钮

            Destroy(buttonDict[targetBox]);

            interactButtonList.Remove(buttonDict[targetBox]);
            buttonDict.Remove(targetBox);

            if (removeSelectedButton)
            {
                if (interactButtonList.Count == 0)
                    selectedIndex = -1;
                else
                    selectedIndex = 0;

                RefreshButtonColors();
            }
        }
    }

    void SelectButton(int index)
    {
        selectedIndex = index;
        RefreshButtonColors();
    }

    void RefreshButtonColors()
    {
        for (int i = 0; i < interactButtonList.Count; i++)
        {
            var img = interactButtonList[i].GetComponent<UnityEngine.UI.Image>();
            if (img == null) continue;

            img.color = i == selectedIndex ? highlightColor : normalColor;
        }
    }

    // 通过字典查找按钮对应的箱子并进行操作
    void InteractBox()
    {
        GameObject selectedButton = interactButtonList[selectedIndex];

        foreach (var box in buttonDict)
        {
            if (box.Value == selectedButton)
            {
                box.Key.GetComponent<RewardBox>().StartOpen();
                break;
            }
        }
    }
}
