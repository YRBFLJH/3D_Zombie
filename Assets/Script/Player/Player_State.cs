using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Telepathy;

public class Player_State : NetworkBehaviour
{
    Player_Getcomponent playerGetcomponent;
    Player player;

    [HideInInspector]
    public float healthRecoverTime = 3; // 自然回血时间
    float lastHealthRecoverTime;
    float healthConsumptionTime = 3; // 持续扣血（受伤流血、无饱食度、无饮水值）
    float lastHealthConsumptionTime;
    [HideInInspector]
    public float satietyConsumptionTime = 3; // 自然消耗饱食度时间
    float lastSatietyConsumptionTime;
    [HideInInspector]
    public float thirstRecoverConsumption = 3; // 自然消耗饮水值时间
    float lastThirstRecoverConsumption;


    // 属性数值
    [HideInInspector]
    [SyncVar]public float speed;


    [HideInInspector]
    [SyncVar(hook = nameof(UpdateHealthUI))]public float health;
    [HideInInspector]
    [SyncVar]public float maxHealth;

    [HideInInspector]
    [SyncVar(hook = nameof(UpdateSatietyUI))]public float satiety;
    [HideInInspector]
    [SyncVar]public float maxSatiety;

    [HideInInspector]
    [SyncVar(hook = nameof(UpdateThirstUI))]public float thirst;
    [HideInInspector]
    [SyncVar]public float maxThirst;

    private void Awake()
    {
        playerGetcomponent = GetComponent<Player_Getcomponent>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        health = maxHealth = 100;
        satiety = maxSatiety = 100;
        thirst = maxThirst = 100;
    }

    [ServerCallback]
    void Update()
    {
        HeathRecover();
        SatietyConsumption();
        ThirstRecoverConsumption();
    }

    // 被攻击，由攻击方调用
    [Server]
    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0)
        {
            health = 0;
            Dead();
        }
    }

    // 死亡
    [Server]
    void Dead()
    {
        health = maxHealth; //测试
        Debug.Log("死亡");
    }

    [Server]
    void HeathRecover() // 自然回血
    {
        if (health > 0 && Time.time - lastHealthRecoverTime >= healthRecoverTime && health < maxHealth)
        {
            lastHealthRecoverTime = Time.time;
            health += 1;
        }
    }

    [Server]
    void SatietyConsumption() // 自然消耗饱食度
    {
        if (satiety > 0 && Time.time - lastSatietyConsumptionTime >= satietyConsumptionTime)
        {
            lastSatietyConsumptionTime = Time.time;
            satiety -= 1;
        }

        if (satiety <= 0)
        {
            satiety = 0;
            ContinueLossHP();
        }
    }

    [Server]
    void ThirstRecoverConsumption() // 自然消耗饮水值
    {
        if (thirst > 0 && Time.time - lastThirstRecoverConsumption >= thirstRecoverConsumption)
        {
            lastThirstRecoverConsumption = Time.time;
            thirst -= 1;
        }

        if (thirst <= 0)
        {
            thirst = 0;
            ContinueLossHP();
        }
    }

    [Server]
    void ContinueLossHP() // 持续扣血
    {
        if (health > 0 && Time.time - lastHealthConsumptionTime >= healthConsumptionTime)
        {
            lastHealthConsumptionTime = Time.time;
            health -= 5;
        }
    }

    void UpdateHealthUI(float oldVal, float newVal) 
    {
        if (!isLocalPlayer) return;
        StateUI.instance.UpdateHealthUI(newVal, maxHealth);
    }

    void UpdateSatietyUI(float oldVal, float newVal) 
    {
        if (!isLocalPlayer) return;
        StateUI.instance.UpdateSatietyUI(newVal, maxSatiety);
    }

    void UpdateThirstUI(float oldVal, float newVal) 
    {
        if (!isLocalPlayer) return;
        StateUI.instance.UpdateThirstUI(newVal, maxThirst);
    }



    // 测试按钮用
    [Command]
    public void CmdReduceHealth(float value)
    {
        health = Mathf.Max(0, health - value);
    }

    [Command]
    public void CmdAddSatiety(float value)
    {
        satiety = Mathf.Min(maxSatiety, satiety + value);
    }

    [Command]
    public void CmdAddThirst(float value)
    {
        thirst = Mathf.Min(maxThirst, thirst + value);
    }
}
