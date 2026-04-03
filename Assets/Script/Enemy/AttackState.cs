using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : BaseState
{
    Enemy_State enemyState;

    public AttackState(AIStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        enemyState = stateMachine.controller.state;

        if (!enemyState.isAttack)
        {
            enemyState.isAttack = true;
            stateMachine.controller.RpcPlayAttackTrigger();
            stateMachine.controller.speed = 0;
        }
    }

    public override void OnUpdate()
    {
        if (stateMachine.controller.isDead)
        {
            stateMachine.ChangeState(stateMachine.controller.deadState);
            return;
        }

        if (enemyState.isAttack)
            return;

        // 如果攻击结束后还可以攻击（玩家还在面前的攻击范围内），则继续攻击
        if (stateMachine.controller.CanAttack())
            {
                enemyState.isAttack = true;
                stateMachine.controller.RpcPlayAttackTrigger();
                stateMachine.controller.speed = 0;
                return;
            }

        // 玩家走远了回到Run继续寻路
        stateMachine.ChangeState(stateMachine.controller.runState);
        return;
        
    }

    public override void OnExit()
    {
        enemyState.isAttack = false;
    }
}
