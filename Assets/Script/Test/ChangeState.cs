using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChangeState : MonoBehaviour
{
    Player_State player;
    public Button reducehealthButton;
    public Button AddfoodButton;

    public Button AddwaterButton;


    void Start()
    {
        reducehealthButton.onClick.AddListener(Health);
        AddfoodButton.onClick.AddListener(Food);
        AddwaterButton.onClick.AddListener(Water);
    }

    public void Health()
    {
        // 单机模式：通过查找标签获取本地玩家组件
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.GetComponent<Player_State>();
        if (player != null)
            player.ReduceHealth(5);
    }
    public void Food()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.GetComponent<Player_State>();
        if (player != null)
            player.AddSatiety(5);
    }
    public void Water()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.GetComponent<Player_State>();
        if (player != null)
            player.AddThirst(5);
    }
}