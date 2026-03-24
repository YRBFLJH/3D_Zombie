using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Player_ChangeHandItem : NetworkBehaviour
{
    Player localPlayer;

    private Player_Animator animator;

    public Transform gunPosition;
    public GameObject gun;
    private GameObject currentGun;

    void Awake()
    {
        animator = GetComponent<Player_Animator>();
    }

    void Start()
    {
        localPlayer = NetworkClient.localPlayer.GetComponent<Player>();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            localPlayer.isArmed = !localPlayer.isArmed;
            animator.PlayArmed(localPlayer.isArmed);
            if (currentGun == null)
            {
                currentGun = Instantiate(gun);
                currentGun.transform.SetParent(gunPosition,false);
            }
            else
            {
                currentGun.SetActive(localPlayer.isArmed);
            }
        }
    }
}