using UnityEngine;

public class Enemy_State : MonoBehaviour
{
    [HideInInspector]
    public bool isAttack;
    [HideInInspector]
    public bool isStopRotate; // 动画开始时应停止旋转

    public BoxCollider Weapon;

    // 动画结束调用
    void StartAttack()
    {
        isStopRotate = true;
    }
    void EndAttack()
    {
        isAttack = false;
        isStopRotate = false;
    }

    // 攻击帧开启检测
    void StartAttackOne()
    {
        Weapon.isTrigger = true;
    }
    void EndAttackOne()
    {
        Weapon.isTrigger = false;
    }
    void StartAttackTwo()
    {
        Weapon.isTrigger = true;
    }
    void EndAttackTwo()
    {
        Weapon.isTrigger = false;
    }

    void EndDead()
    {
        Invoke(nameof(DestroyDead), 2f);
    }

    void DestroyDead()
    {
        Destroy(gameObject.transform.parent.gameObject);
    }
}