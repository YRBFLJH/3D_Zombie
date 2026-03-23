using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;


public class Player : MonoBehaviour
{
    public bool isArmed;

    public static Player instance;

    [HideInInspector]
    public CharacterController characterController;


    // 属性数值
    [HideInInspector]
    public float speed,health,maxHealth,experience,maxExperience;

    [HideInInspector]
    public int level,gold;

    [HideInInspector]
    public string playerName;

    void Awake()
    {
        if(instance == null)
            instance = this;
        else
            Destroy(gameObject);

        characterController = GetComponent<CharacterController>();
    }


    // 瞬移 (供外部调用:回档、技能)
    public void Teleportation(Vector3 endPosition)
    {
        characterController.enabled = false;
        transform.position = endPosition;
        characterController.enabled = true;
    }
}
