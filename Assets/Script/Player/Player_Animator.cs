using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Animator : MonoBehaviour
{
    private Animator anim;

    Player_Shoot shoot;

    private readonly int idleHash = Animator.StringToHash("isIdle");
    private readonly int movingHash = Animator.StringToHash("isMoving");
    private readonly int runningHash = Animator.StringToHash("isRunning");
    private readonly int armedHash = Animator.StringToHash("isArmed");
    private readonly int aimHash = Animator.StringToHash("isAim");

    private float ik_AllWeight;
    private float ik_BodyWeight;
    private float ik_HeadWeight;
    private float ik_EyeWeight;

    private Vector3 lookAtPosition;  // 注视位置（世界坐标）

    public float cameraRightOffset;

    void Awake()
    {
        anim = GetComponent<Animator>();
        shoot = GetComponent<Player_Shoot>();
    }

    void Update()
    {
        // 计算当前注视目标点（例如摄像机前方 4 米处 + 右侧偏移）
        lookAtPosition = Camera.main.transform.position 
                         + Camera.main.transform.forward * 4f 
                         + Camera.main.transform.right * cameraRightOffset;
    }

    void OnAnimatorIK(int layerIndex)
    {
        Vector3 targetPos = lookAtPosition;

        // 防止目标点未初始化时出现异常（例如刚生成时）
        if (targetPos == Vector3.zero)
            targetPos = transform.position + transform.forward * 4f;

        // 根据层设置权重（保持你的逻辑）
        if (layerIndex == 0)
        {
            ik_AllWeight = 0.8f;
            ik_BodyWeight = 0f;
            ik_HeadWeight = 0.1f;
            ik_EyeWeight = 0.1f;
        }
        else if (layerIndex == 1)
        {
            ik_AllWeight = 1f;
            ik_BodyWeight = 0.35f;
            ik_HeadWeight = 0.45f;
            ik_EyeWeight = 0.3f;
        }

        anim.SetLookAtWeight(ik_AllWeight, ik_BodyWeight, ik_HeadWeight, ik_EyeWeight);
        anim.SetLookAtPosition(targetPos);
    }

    // 下面的动画状态方法直接本地调用
    public void PlayIdle(bool isIdle) => anim.SetBool(idleHash, isIdle);
    public void PlayMove(bool isMoving) => anim.SetBool(movingHash, isMoving);
    public void PlayRun(bool isRunning) => anim.SetBool(runningHash, isRunning);
    public void PlayArmed(bool isArmed) => anim.SetBool(armedHash, isArmed);
    public void PlayAim(bool isAiming) => anim.SetBool(aimHash, isAiming);
    public void PlayReload() => anim.SetTrigger("canReload");
}