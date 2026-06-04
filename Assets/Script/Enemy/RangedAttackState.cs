using UnityEngine;

public class RangedAttackState : BaseState
{
    private float lastAttackTime;
    private Enemy_Controller controller;

    public RangedAttackState(AIStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        controller = stateMachine.controller;
        controller.speed = 0;
        lastAttackTime = Time.time;
    }

    public override void OnUpdate()
    {
        if (controller.isDead) return;

        // Always face the player
        if (controller.target != null)
        {
            Vector3 towardPlayer = (controller.target.position - controller.transform.position).normalized;
            towardPlayer.y = 0;
            if (towardPlayer.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(towardPlayer);
                controller.transform.rotation = Quaternion.RotateTowards(
                    controller.transform.rotation, targetRot,
                    controller.rotationSpeed * Time.deltaTime);
            }
        }

        // Check if still in attack range
        if (controller.target != null)
        {
            float dist = Vector3.Distance(controller.transform.position, controller.target.position);
            if (dist > controller.viewDistance * 1.2f)
            {
                stateMachine.ChangeState(controller.runState);
                return;
            }
        }

        // Attack cooldown
        if (Time.time >= lastAttackTime + 3f)
        {
            lastAttackTime = Time.time;
            controller.PlayAttackTrigger();
        }
    }

    public override void OnExit()
    {
        if (controller.state != null)
        {
            controller.state.isAttack = false;
            controller.state.isStopRotate = false;
        }
    }
}
