using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleState : BaseState
{
    public IdleState(AIStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        stateMachine.controller.animIdle = true;
        stateMachine.controller.speed = 0;
    }

    public override void OnUpdate()
    {
        Enemy_Controller ai = stateMachine.controller;

        if (ai.isDead)
        {
            stateMachine.ChangeState(ai.deadState);
            return;
        }

    }

    public override void OnExit()
    {
        stateMachine.controller.animIdle = false;
    }

}
