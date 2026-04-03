using System.Collections;
using System.Collections.Generic;
using Mirror;
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
        player = NetworkClient.localPlayer.GetComponent<Player_State>();
        player.CmdReduceHealth(5);
    }
    public void Food()
    {
        player = NetworkClient.localPlayer.GetComponent<Player_State>();
        player.CmdAddSatiety(5);
    }
    public void Water()
    {
        player = NetworkClient.localPlayer.GetComponent<Player_State>();
        player.CmdAddThirst(5);
    }
}
