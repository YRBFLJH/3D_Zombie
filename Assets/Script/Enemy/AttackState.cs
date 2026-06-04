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
        stateMachine.controller.speed = 0f;
        // 先对准再播攻击；避免路径已走完时本帧既不转向也不对齐
        stateMachine.controller.aligningForAttackSwing = true;
    }

    public override void OnUpdate()
    {
        if (stateMachine.controller.isDead)
        {
            stateMachine.ChangeState(stateMachine.controller.deadState);
            return;
        }

        if (stateMachine.controller.aligningForAttackSwing)
        {
            if (stateMachine.controller.IsFacingTargetForAttack())
            {
                stateMachine.controller.aligningForAttackSwing = false;
                enemyState.isAttack = true;
                stateMachine.controller.PlayAttackTrigger();
            }
            return;
        }

        if (enemyState.isAttack)
            return;

        // 如果攻击结束后还可以攻击（玩家还在面前的攻击范围内），则继续攻击
        if (stateMachine.controller.CanAttack())
        {
            stateMachine.controller.aligningForAttackSwing = true;
            stateMachine.controller.speed = 0f;
            return;
        }

        // 玩家走远了回到Run继续寻路
        stateMachine.ChangeState(stateMachine.controller.runState);
        return;
    }

    public override void OnExit()
    {
        stateMachine.controller.aligningForAttackSwing = false;
        enemyState.isAttack = false;
    }
}
