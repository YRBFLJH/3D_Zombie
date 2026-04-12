using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEngine;

public class Player_Move : MonoBehaviour
{
    Player_Getcomponent playerComponent;
    Player Player;

    //X轴Y轴灵敏度
    public float rotationSpeedX = 2f; 
    public float rotationSpeedY = 1f;

    private float rotationX = 0f; 
    private float rotationY = 0f;  

    public Transform virtualCameraFllow; //让摄像机能在玩家站立时旋转角度
    Transform virtualCamera;

    private bool running;
    float playerSpeed;
    public float speed; //同步修改好的playerSpeed并根据奔跑状态切换
    private CharacterController characterController;

    // 角色旋转的灵敏度
    private float characterRotateSmooth = 2.75f;

    private Player_Animator playerAnimator;

    Vector3 cameraForward;
    Vector3 cameraRight;
    Vector3 moveDir;
    Quaternion targetRotation;

    private Player_Shoot playerShoot;

    float playerForwardY;
    float yoffset;

    float gravity = -9.8f;
    float verticalVelocity;

    public bool isMouse = false;

    void Awake()
    {
        playerComponent = GetComponent<Player_Getcomponent>();
    }

    void Start()
    {   
        Player = playerComponent.playerCS;
        playerAnimator = playerComponent.playerAnimatorCS;
        characterController = playerComponent.characterController;
        playerShoot = playerComponent.playerShootCS;

        virtualCamera = playerComponent.virtualCamera.transform;

        playerSpeed = 3f;
    }

    void Update() 
    {   
        Move();
        CursorChange();
    }

    void CursorChange()
    {
        if (!isMouse)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void Move()
    {
        //摄像机视角
        float mouseX = Input.GetAxis("Mouse X") * rotationSpeedY;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSpeedX;
        
        // 累积旋转角度
        rotationY += mouseX;
        rotationX -= mouseY;
    
        rotationX = Mathf.Clamp(rotationX, -45f, 45f);

        if (playerShoot.isAiming)  // 瞄准时视角变化
        {
            rotationX = Mathf.Clamp(rotationX, -30f, 30f);
        }
    
        // 旋转摄像机跟随的点从而旋转摄像机
        virtualCameraFllow.rotation = Quaternion.Euler(rotationX, rotationY, 0f);

        //移动
        speed = running ? playerSpeed + 5.5f : playerSpeed;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 构建基于摄像机朝向的移动方向（只取水平方向，消除Y轴影响）
        cameraForward = virtualCamera.forward;
        cameraRight = virtualCamera.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        if (characterController.isGrounded)
        {
            //  grounded 时重置下落速度
            verticalVelocity = -0.5f;
        }
        else
        {
            // 空中自由落体下落
            verticalVelocity += gravity * Time.deltaTime;
        }

        moveDir = cameraRight * horizontal + cameraForward * vertical;
        moveDir.Normalize();

        Vector3 moveWithGravity = moveDir * speed;
        moveWithGravity.y = verticalVelocity;

        if (moveDir.magnitude > 0.1f) // 避免微小输入导致的异常
        {
            playerAnimator.PlayIdle(false);

            characterController.Move(moveWithGravity * Time.deltaTime);

            //移动动画
            if (Input.GetKey(KeyCode.LeftShift) && !playerShoot.isAiming)
            {
                playerAnimator.PlayMove(false);
                playerAnimator.PlayRun(true);
                running = true;
            }
            else
            {
                playerAnimator.PlayRun(false);
                playerAnimator.PlayMove(true);
                running = false;
            }
        }
        else
        {
            playerAnimator.PlayMove(false);
            playerAnimator.PlayRun(false);

            characterController.Move(Vector3.zero);
            playerAnimator.PlayIdle(true);
            running = false;
        }

        Vector3 moveDirFlat = new Vector3(moveDir.x, 0, moveDir.z);

        if (!playerShoot.isAiming) 
        {
            if (moveDirFlat.magnitude > 0.01f)
            {
                targetRotation = Quaternion.LookRotation(moveDirFlat);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * characterRotateSmooth);
            }
        }
        else //瞄准时
        {
            cameraForward.y = 0;
            cameraForward.Normalize();

            if (cameraForward.magnitude > 0.01f)
            {
                targetRotation = Quaternion.LookRotation(cameraForward + transform.right * 0.95f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * characterRotateSmooth * 2f);
            }
        }
    }
}