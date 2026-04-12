using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StateUI : MonoBehaviour
{
    public static StateUI instance;
    public Image healthBar,satietyBar,thirstBar;

    Player_Getcomponent playerGetcomponent;
    Player_State playerState;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if(Player.Instance == null) return;
        playerGetcomponent = Player.Instance.GetComponent<Player_Getcomponent>();
        playerState = playerGetcomponent.playerStateCS;
    }

    public void UpdateHealthUI(float health,float maxHealth)
    {
        healthBar.fillAmount = health / maxHealth;
    }

    public void UpdateSatietyUI(float satiety,float maxSatiety)
    {
        satietyBar.fillAmount = satiety / maxSatiety;
    }

    public void UpdateThirstUI(float thirst,float maxThirst)
    {
        thirstBar.fillAmount = thirst / maxThirst;
    }
}
