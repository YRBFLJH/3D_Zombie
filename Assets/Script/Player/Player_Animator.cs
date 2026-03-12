using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//动作种类
public enum PlayerAnimationState
{
    Idle,
    Walk,
    Run,
    Armed,
    Aim,
    EndAim,
    ReLoad,
    Injured
}

public class Player_Animator : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void SetAnimation(PlayerAnimationState state)
    {
        ResetAnimationState();

        switch(state)
        {
            case PlayerAnimationState.Idle:
                animator.SetBool("isIdle",true);
                break;
            case PlayerAnimationState.Walk:
                animator.SetBool("isMoving",true);
                break;
            case PlayerAnimationState.Run:
                animator.SetBool("isRunning",true);
                break;
            case PlayerAnimationState.Armed:
                animator.SetBool("isArmed",true);
                break;
            case PlayerAnimationState.Aim:
                animator.SetBool("isAim",true);
                break;
            case PlayerAnimationState.EndAim:
                animator.SetBool("isAim",false);
                break;
        }
    }

    void ResetAnimationState()
    {
        animator.SetBool("isIdle",false);
        animator.SetBool("isMoving",false);
        animator.SetBool("isRunning",false);
        // animator.SetBool("isArmed",false);
        // animator.SetBool("isAim",false);
    }
}
