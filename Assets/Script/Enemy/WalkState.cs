using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WalkState : BaseState
{
    public WalkState(AIStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        stateMachine.controller.animWalk = true;
        stateMachine.controller.speed = 3f;
    }

    public override void OnUpdate()
    {
        if (stateMachine.controller.isDead)
        {
            stateMachine.ChangeState(stateMachine.controller.deadState);
            return;
        }
    }

    public override void OnExit()
    {
        stateMachine.controller.animWalk = false;
    }
}
