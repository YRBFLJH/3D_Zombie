using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

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

    public Transform ItemSpawnPoint;

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
    }

    void Start()
    {
        crosshair = GameObject.FindWithTag("Crosshair").transform;
        crosshair.gameObject.SetActive(false);

        virtualCamera = GameObject.FindWithTag("VirtualCamera");
        var vcam = virtualCamera.GetComponent<CinemachineVirtualCamera>();
        vcam.Follow = lookFllow;
        vcam.LookAt = lookAt;
    }
}