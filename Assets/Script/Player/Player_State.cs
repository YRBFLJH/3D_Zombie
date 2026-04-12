using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_State : MonoBehaviour
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
    public float speed;

    [HideInInspector]
    private float _health;
    public float health
    {
        get => _health;
        set
        {
            float oldVal = _health;
            _health = value;
            UpdateHealthUI(oldVal, _health);
        }
    }

    [HideInInspector]
    public float maxHealth;

    [HideInInspector]
    private float _satiety;
    public float satiety
    {
        get => _satiety;
        set
        {
            float oldVal = _satiety;
            _satiety = value;
            UpdateSatietyUI(oldVal, _satiety);
        }
    }

    [HideInInspector]
    public float maxSatiety;

    [HideInInspector]
    private float _thirst;
    public float thirst
    {
        get => _thirst;
        set
        {
            float oldVal = _thirst;
            _thirst = value;
            UpdateThirstUI(oldVal, _thirst);
        }
    }

    [HideInInspector]
    public float maxThirst;

    private void Awake()
    {
        playerGetcomponent = GetComponent<Player_Getcomponent>();
        player = playerGetcomponent.playerCS;
    }

    void Start()
    {
        health = maxHealth = 100;
        satiety = maxSatiety = 100;
        thirst = maxThirst = 100;
    }

    void Update()
    {
        HeathRecover();
        SatietyConsumption();
        ThirstRecoverConsumption();
    }

    // 被攻击，由攻击方调用
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
    void Dead()
    {
        health = maxHealth; //测试
        Debug.Log("死亡");
    }

    void HeathRecover() // 自然回血
    {
        if (health > 0 && Time.time - lastHealthRecoverTime >= healthRecoverTime && health < maxHealth)
        {
            lastHealthRecoverTime = Time.time;
            health += 1;
        }
    }

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
        StateUI.instance.UpdateHealthUI(newVal, maxHealth);
    }

    void UpdateSatietyUI(float oldVal, float newVal)
    {
        StateUI.instance.UpdateSatietyUI(newVal, maxSatiety);
    }

    void UpdateThirstUI(float oldVal, float newVal)
    {
        StateUI.instance.UpdateThirstUI(newVal, maxThirst);
    }

    // 测试按钮用
    public void ReduceHealth(float value)
    {
        health = Mathf.Max(0, health - value);
    }

    public void AddSatiety(float value)
    {
        satiety = Mathf.Min(maxSatiety, satiety + value);
    }

    public void AddThirst(float value)
    {
        thirst = Mathf.Min(maxThirst, thirst + value);
    }
}