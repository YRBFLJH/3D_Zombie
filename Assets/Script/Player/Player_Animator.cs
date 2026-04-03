using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Player_Animator : NetworkBehaviour
{
    private NetworkAnimator netAnimator;
    private Animator anim;

    private readonly int idleHash = Animator.StringToHash("isIdle");
    private readonly int movingHash = Animator.StringToHash("isMoving");
    private readonly int runningHash = Animator.StringToHash("isRunning");
    private readonly int armedHash = Animator.StringToHash("isArmed");
    private readonly int aimHash = Animator.StringToHash("isAim");

    private float ik_AllWeight;
    private float ik_BodyWeight;
    private float ik_HeadWeight;
    private float ik_EyeWeight;

    [SyncVar] private Vector3 syncLookAtPosition;  // 同步的注视位置（世界坐标）

    public float cameraRightOffset;

    void Awake()
    {
        netAnimator = GetComponent<NetworkAnimator>();
        anim = netAnimator.animator;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // 本地玩家计算自己当前的注视目标点（例如摄像机前方 4 米处 + 右侧偏移）
        Vector3 localLookAt = Camera.main.transform.position 
                              + Camera.main.transform.forward * 4f 
                              + Camera.main.transform.right * cameraRightOffset;

        // 通过命令将目标点发送给服务器（可以增加角度变化阈值优化性能）
        CmdUpdateLookAt(localLookAt);
    }

    [Command]
    void CmdUpdateLookAt(Vector3 lookPos)
    {
        // 服务器收到后设置同步变量，会广播给所有客户端
        syncLookAtPosition = lookPos;
    }

    void OnAnimatorIK(int layerIndex)
    {
        // 所有玩家（包括本地）都使用同步到的目标点
        // 对于本地玩家，syncLookAtPosition 会很快被自己的 Update 更新，所以效果一致
        Vector3 targetPos = syncLookAtPosition;

        // 防止目标点未初始化时出现异常（例如刚生成时）
        if (targetPos == Vector3.zero)
            targetPos = transform.position + transform.forward * 4f;

        // 根据层设置权重（保持你的逻辑）
        if (layerIndex == 0)
        {
            ik_AllWeight = 0.8f;
            ik_BodyWeight = 0.1f;
            ik_HeadWeight = 0.1f;
            ik_EyeWeight = 0.1f;
        }
        else if (layerIndex == 1)
        {
            ik_AllWeight = 1f;
            ik_BodyWeight = 0.9f;
            ik_HeadWeight = 0.45f;
            ik_EyeWeight = 0.3f;
        }

        anim.SetLookAtWeight(ik_AllWeight, ik_BodyWeight, ik_HeadWeight, ik_EyeWeight);
        anim.SetLookAtPosition(targetPos);
    }

    // 下面的动画状态同步方法保持不变
    public void PlayIdle(bool isIdle) => CmdSetBool(idleHash, isIdle);
    public void PlayMove(bool isMoving) => CmdSetBool(movingHash, isMoving);
    public void PlayRun(bool isRunning) => CmdSetBool(runningHash, isRunning);
    public void PlayArmed(bool isArmed) => CmdSetBool(armedHash, isArmed);
    public void PlayAim(bool isAiming) => CmdSetBool(aimHash, isAiming);
    public void PlayReload() => CmdPlayReload();

    [Command] void CmdSetBool(int hash, bool value) => RpcSetBool(hash, value);
    [ClientRpc] void RpcSetBool(int hash, bool value) => anim.SetBool(hash, value);

    [Command] void CmdPlayReload() => RpcPlayReload();
    [ClientRpc] void RpcPlayReload() => anim.SetTrigger("canReload");
}