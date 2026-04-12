using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadState : BaseState
{
    public DeadState(AIStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        stateMachine.controller.PlayDeadTrigger();
        stateMachine.controller.speed = 0;
    }

    public override void OnUpdate() { }

    public override void OnExit() { }


}
