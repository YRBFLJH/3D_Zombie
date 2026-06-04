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

    [HideInInspector] public float lastLoadTime = -99f; // 读档时间戳，用于短暂屏蔽服务器同步

    [HideInInspector]
    public bool isDead;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private int maxRespawns = 3;
    [HideInInspector] public int respawnCount;
    private Vector3 spawnPoint;

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
        spawnPoint = transform.position;
    }

    void Update()
    {
        // Server-authoritative: stats driven by PlayerStatsSync, not local timers
    }

    // Server applies authoritative stats
    public void ApplyServerStats(float hp, float maxHp, float food, float water, bool dead)
    {
        // 读档后短暂屏蔽服务器同步，避免刚加载的值被覆盖
        if (Time.time - lastLoadTime < 2f) return;

        maxHealth = maxHp;
        health = hp;
        maxSatiety = 100f;
        satiety = food;
        maxThirst = 100f;
        thirst = water;

        if (dead && !isDead)
        {
            isDead = true;
            Player_Move playerMove = playerGetcomponent.playerMoveCS;
            Player_Shoot playerShoot = playerGetcomponent.playerShootCS;
            if (playerMove != null) playerMove.enabled = false;
            if (playerShoot != null) playerShoot.enabled = false;
            Player_Animator playerAnim = playerGetcomponent.playerAnimatorCS;
            if (playerAnim != null) playerAnim.PlayDead();
            if (DeathScreenUI.instance != null) DeathScreenUI.instance.Show(this);
            Debug.Log("Player dead (server)");
        }
    }

    // Damage may be applied by HitResult for visual feedback only
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        // Server is authoritative for death — client only updates display
    }

    // Called when server sends PlayerRespawn
    public void Respawn()
    {
        isDead = false;
        respawnCount++;
        health = maxHealth;
        satiety = maxSatiety;
        thirst = maxThirst;
        CharacterController cc = playerGetcomponent.characterController;
        if (cc != null) cc.enabled = false;
        transform.position = spawnPoint;
        if (cc != null) cc.enabled = true;
        Player_Move playerMove = playerGetcomponent.playerMoveCS;
        Player_Shoot playerShoot = playerGetcomponent.playerShootCS;
        if (playerMove != null) playerMove.enabled = true;
        if (playerShoot != null) playerShoot.enabled = true;
        Player_Animator playerAnim = playerGetcomponent.playerAnimatorCS;
        if (playerAnim != null) playerAnim.PlayDead(false);
        if (DeathScreenUI.instance != null) DeathScreenUI.instance.Hide();
        Debug.Log("Player respawned (server)");
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