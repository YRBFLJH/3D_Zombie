using UnityEngine;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "ZombieGame/EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    [Header("基础属性")]
    public string enemyName = "Zombie";
    public float maxHealth = 100f;
    public float walkSpeed = 3f;
    public float runSpeed = 5.5f;
    public float rotationSpeed = 320f;
    public GameObject deathEffect;

    [Header("攻击属性")]
    public float attackDamage = 10f;
    public float attackRange = 0.85f;
    public float attackCooldown = 1.5f;
    public float attackFaceAngleThreshold = 22f;

    [Header("探测属性")]
    public float viewDistance = 8f;
    public float viewAngle = 50f;

    [Header("寻路属性")]
    public float repathDistance = 1.5f;
    public float randomMoveTime = 5f;
    public int patrolRadius = 5;

    [Header("AI类型")]
    public bool isRangedAttacker;
    public GameObject rangedProjectilePrefab;
    public float projectileSpeed = 20f;

    [Header("掉落")]
    public LootTable lootTable;
}
