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
    public Transform crosshair; //准星位置

    private Animator animator;

    //Hash比字符串更快性能更好
    private readonly int idleHash = Animator.StringToHash("isIdle");
    private readonly int movingHash = Animator.StringToHash("isMoving");
    private readonly int runningHash = Animator.StringToHash("isRunning");
    private readonly int armedHash = Animator.StringToHash("isArmed");
    private readonly int aimHash = Animator.StringToHash("isAim");


    private float ik_AllWeight;
    private float ik_BodyWeight;
    private float ik_HeadWeight;
    private float ik_EyeWeight;

    public float cameraRightOffset;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void PlayIdle(bool isIdle)
    {
        animator.SetBool(idleHash, isIdle);
    }

    public void PlayerMove(bool isMoving)
    {
        animator.SetBool(movingHash, isMoving);
    }

    public void PlayerRun(bool isRunning)
    {
        animator.SetBool(runningHash, isRunning);
    }

    public void PlayArmed(bool isArmed)
    {
        animator.SetBool(armedHash, isArmed);
    }

    public void PlayAim(bool isAiming)
    {
        animator.SetBool(aimHash, isAiming);
    }

    public void PlayReload()
    {
        animator.SetTrigger("canReload");
    }
    

    void OnAnimatorIK(int layerIndex) //动画IK
    {
        if(layerIndex == 0) //只在基础层设置IK
        {
            ik_AllWeight = 0.8f;
            ik_BodyWeight = 0.1f;
            ik_HeadWeight = 0.1f;
            ik_EyeWeight = 0.1f;
        }
        else if(layerIndex == 1)
        {
            ik_AllWeight = 1;
            ik_BodyWeight = 0.9f;
            ik_HeadWeight = 0.45f;
            ik_EyeWeight = 0.3f;
        }


        animator.SetLookAtWeight(ik_AllWeight, ik_BodyWeight, ik_HeadWeight, ik_EyeWeight); //设置权重(参数按顺序：总的、身体、头部、眼睛的权重)
        animator.SetLookAtPosition(Camera.main.transform.position + Camera.main.transform.forward * 4f + Camera.main.transform.right * cameraRightOffset); //设置IK目标位置（这里设置为摄像机前方一定距离的位置，可以根据需要调整）
    }

  
}
