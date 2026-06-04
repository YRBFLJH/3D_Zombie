using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Animator : MonoBehaviour
{
    private Animator anim;

    Player_Shoot shoot;
    Player player;

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
    private Vector3 remoteLookDirection = Vector3.forward;
    private Vector3 smoothedRemoteLookDirection = Vector3.forward;
    private bool hasRemoteLookDirection = false;
    [SerializeField] private float remoteLookSmoothSpeed = 30f;

    public float cameraRightOffset;

    void Awake()
    {
        anim = GetComponent<Animator>();
        shoot = GetComponent<Player_Shoot>();

        player = GetComponent<Player>();
    }

    void Update()
    {
        bool isLocal = player != null && player.isLocalPlayer;

        if (isLocal && Camera.main != null)
        {
            // 本地玩家：继续使用本地相机方向作为IK目标
            lookAtPosition = Camera.main.transform.position
                             + Camera.main.transform.forward * 4f
                             + Camera.main.transform.right * cameraRightOffset;
        }
        else
        {
            // 远端玩家：优先使用网络同步过来的视线方向
            Vector3 basePos = transform.position + Vector3.up * 1.5f;
            if (hasRemoteLookDirection)
            {
                float t = Mathf.Clamp01(Time.deltaTime * remoteLookSmoothSpeed);
                smoothedRemoteLookDirection = Vector3.Slerp(smoothedRemoteLookDirection, remoteLookDirection, t);
            }
            else
            {
                smoothedRemoteLookDirection = transform.forward;
            }

            Vector3 forward = smoothedRemoteLookDirection;
            Vector3 right = transform.right;
            lookAtPosition = basePos + forward * 4f + right * cameraRightOffset;
        }
    }

    private int ikDebugFrame = 0;

    void OnAnimatorIK(int layerIndex)
    {
        ikDebugFrame++;
        bool isLocal = player != null && player.isLocalPlayer;
        if (!isLocal && ikDebugFrame % 120 == 1)
        {
            Debug.Log($"[IK Debug] OnAnimatorIK called! layer={layerIndex} lookAt={lookAtPosition} " +
                      $"hasRemote={hasRemoteLookDirection} remoteDir={remoteLookDirection}");
        }

        Vector3 targetPos;
        if (!isLocal && hasRemoteLookDirection)
        {
            // Remote: compute look target directly from synced direction
            Vector3 headPos = anim.GetBoneTransform(HumanBodyBones.Head)?.position
                              ?? (transform.position + Vector3.up * 1.5f);
            targetPos = headPos + smoothedRemoteLookDirection * 4f;
        }
        else
        {
            targetPos = lookAtPosition;
        }

        if (targetPos == Vector3.zero)
            targetPos = transform.position + transform.forward * 4f;

        // 根据层设置权重（保持你的逻辑）
        if (layerIndex == 0)
        {
            ik_AllWeight = 0.8f;
            ik_BodyWeight = 0.2f;
            ik_HeadWeight = 0.5f;
            ik_EyeWeight = 0.4f;
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
    public void PlayDead(bool isDead = true) => anim.SetBool("isDead", isDead);

    public void SetRemoteLookDirection(Vector3 lookDir)
    {
        if (lookDir.sqrMagnitude < 0.0001f) return;
        remoteLookDirection = lookDir.normalized;
        if (!hasRemoteLookDirection)
        {
            smoothedRemoteLookDirection = remoteLookDirection;
        }
        hasRemoteLookDirection = true;
    }
}