using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Mirror;


public class Player : NetworkBehaviour
{
    public static Player Instance;
    public bool isArmed;

    [HideInInspector]
    public CharacterController characterController;

    Player_Getcomponent playerGetcomponent;


    [HideInInspector]
    public int level,gold;

    [HideInInspector]
    public string playerName;

    void Awake()
    {        
        Instance = this;
        playerGetcomponent = GetComponent<Player_Getcomponent>();
    }

    void Start()
    {
        characterController = playerGetcomponent.characterController;
    }

    // 瞬移 (供外部调用:回档、技能)
    public void Teleportation(Vector3 endPosition)
    {
        characterController.enabled = false;
        transform.position = endPosition;
        characterController.enabled = true;
    }

    
}
