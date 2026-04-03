using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunState : BaseState
{
    public RunState(AIStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        stateMachine.controller.animRun = true;
        stateMachine.controller.speed = 5.5f;
    }

    public override void OnUpdate()
    {
        if (stateMachine.controller.isDead)
        {
            stateMachine.ChangeState(stateMachine.controller.deadState);
            return;
        }

        if (stateMachine.controller.CanAttack())
        {
            stateMachine.ChangeState(stateMachine.controller.attackState);
        }
    }

    public override void OnExit()
    {
        stateMachine.controller.animRun = false;
    }
}
