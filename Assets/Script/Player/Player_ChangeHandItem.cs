using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_ChangeHandItem : MonoBehaviour
{
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
        
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Player.instance.isArmed = !Player.instance.isArmed;
            animator.PlayArmed(Player.instance.isArmed);
            if (currentGun == null)
            {
                currentGun = Instantiate(gun);
                currentGun.transform.SetParent(gunPosition,false);
            }
        }
    }
}