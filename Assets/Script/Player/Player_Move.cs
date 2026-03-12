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
    public float moveSpeed = 3; //供外部修改
    private float speed; //同步修改好的moveSpeed并根据奔跑状态切换
    private CharacterController characterController;

    // 角色旋转的灵敏度
    public float characterRotateSmooth = 3f;


    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Move();
    }

    void Move()
    {
        if (Input.GetMouseButton(1))
        {
            gameObject.GetComponent<Player_Animator>().SetAnimation(PlayerAnimationState.Aim);
        }
        else gameObject.GetComponent<Player_Animator>().SetAnimation(PlayerAnimationState.EndAim);

        //摄像机视角
        float mouseX = Input.GetAxis("Mouse X") * rotationSpeedY;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSpeedX;
        
        mouseX = +mouseX;
        mouseY = +mouseY;
        
        // 累积旋转角度
        rotationY += mouseX;
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -45f, 45f);
    
        // 旋转摄像机跟随的点从而旋转摄像机
        visualCameraFllow.rotation = Quaternion.Euler(rotationX, rotationY, 0f);


        //移动
        speed = running ? moveSpeed + 10 : moveSpeed;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 构建基于摄像机朝向的移动方向（只取水平方向，消除Y轴影响）
        Vector3 cameraForward = playerCamera.forward;
        Vector3 cameraRight = playerCamera.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDir = cameraRight * horizontal + cameraForward * vertical;
        moveDir.Normalize();

        if (moveDir.magnitude > 0.1f) // 避免微小输入导致的异常
        {
            characterController.Move(moveDir * speed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveDir.x, 0, moveDir.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * characterRotateSmooth);

            //移动动画
            if (Input.GetKey(KeyCode.LeftShift))
            {
                gameObject.GetComponent<Player_Animator>().SetAnimation(PlayerAnimationState.Run);
                running = true;
            }
            else
            {
                gameObject.GetComponent<Player_Animator>().SetAnimation(PlayerAnimationState.Walk);
                running = false;
            }
        }
        else
        {
            characterController.Move(Vector3.zero);
            gameObject.GetComponent<Player_Animator>().SetAnimation(PlayerAnimationState.Idle);
            running = false;
        }
    }
}