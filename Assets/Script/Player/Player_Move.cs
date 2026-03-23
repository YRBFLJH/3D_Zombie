using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEngine;

public class Player_Move : MonoBehaviour
{
    //X轴Y轴灵敏度
    public float rotationSpeedX = 2f; 
    public float rotationSpeedY = 1f;


    private float rotationX = 0f; 
    private float rotationY = 0f;  

    public Transform visualCameraFllow; //让摄像机能在玩家站立时旋转角度
    public Transform playerCamera;


    private bool running;
    private float moveSpeed => Player.instance.speed;
    private float speed; //同步修改好的moveSpeed并根据奔跑状态切换
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


    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerAnimator = GetComponent<Player_Animator>();
        playerShoot = GetComponent<Player_Shoot>();
    }

    void Start()
    {
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;

        Player.instance.speed = 3f;
    }

    void Update()
    {
        Move();
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

        if (playerShoot.isAiming)
        {
            playerForwardY = transform.eulerAngles.y;
            yoffset = Mathf.DeltaAngle(playerForwardY,rotationY);
            yoffset = Mathf.Clamp(yoffset, -75f, 40f);

            rotationY = playerForwardY + yoffset;


            rotationX = Mathf.Clamp(rotationX, -30f, 30f);
        }
    
        // 旋转摄像机跟随的点从而旋转摄像机
        visualCameraFllow.rotation = Quaternion.Euler(rotationX, rotationY, 0f);


        //移动
        speed = running ? moveSpeed + 10 : moveSpeed;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 构建基于摄像机朝向的移动方向（只取水平方向，消除Y轴影响）
        cameraForward = playerCamera.forward;
        cameraRight = playerCamera.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        moveDir = cameraRight * horizontal + cameraForward * vertical;
        moveDir.Normalize();

        if (moveDir.magnitude > 0.1f) // 避免微小输入导致的异常
        {
            playerAnimator.PlayIdle(false);

            characterController.Move(moveDir * speed * Time.deltaTime);

            //移动动画
            if (Input.GetKey(KeyCode.LeftShift) && !playerShoot.isAiming)
            {
                playerAnimator.PlayerMove(false);
                playerAnimator.PlayerRun(true);
                running = true;
            }
            else
            {
                playerAnimator.PlayerRun(false);
                playerAnimator.PlayerMove(true);
                running = false;
            }
        }
        else
        {
            playerAnimator.PlayerMove(false);
            playerAnimator.PlayerRun(false);

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
                targetRotation = Quaternion.LookRotation(cameraForward + transform.right * 0.9f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * characterRotateSmooth * 2f);
            }
        }
    }
}