using Mirror;
using UnityEngine;

public class Enemy_State : NetworkBehaviour
{
    [HideInInspector]
    [SyncVar] public bool isAttack;
    [HideInInspector]
    [SyncVar] public bool isStopRotate; // 动画开始时应停止旋转

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
        NetworkServer.Destroy(gameObject.transform.parent.gameObject);
    }
}