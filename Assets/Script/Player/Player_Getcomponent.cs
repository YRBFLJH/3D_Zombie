using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class Player_Getcomponent : MonoBehaviour
{
    // 自身组件
    [HideInInspector]
    public Player playerCS;
    [HideInInspector]
    public Player_Move playerMoveCS;
    [HideInInspector]
    public Player_Shoot playerShootCS;
    [HideInInspector]
    public Player_ChangeHandItem playerChangeHandItemCS;
    [HideInInspector]
    public Player_Animator playerAnimatorCS;
    [HideInInspector]
    public CharacterController characterController;
    [HideInInspector]
    public Player_State playerStateCS;

    public Transform lookFllow;
    public Transform lookAt;

    // 外部组件
    [HideInInspector]
    public Transform crosshair;
    [HideInInspector]
    public GameObject virtualCamera;


    void Awake()
    {
        playerCS = GetComponent<Player>();
        playerMoveCS = GetComponent<Player_Move>();
        playerShootCS = GetComponent<Player_Shoot>();
        playerChangeHandItemCS = GetComponent<Player_ChangeHandItem>();
        playerAnimatorCS = GetComponent<Player_Animator>();
        playerStateCS = GetComponent<Player_State>();
        characterController = GetComponent<CharacterController>();

        crosshair = GameObject.FindWithTag("Crosshair").transform;
        virtualCamera = GameObject.FindWithTag("VirtualCamera");
    }

    void Start()
    {
        crosshair.gameObject.SetActive(false);

        var vcam = virtualCamera.GetComponent<CinemachineVirtualCamera>();
        vcam.Follow = lookFllow;
        vcam.LookAt = lookAt;
    }
}