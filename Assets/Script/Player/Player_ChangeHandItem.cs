using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_ChangeHandItem : MonoBehaviour
{
    private Animator animator;

    public Transform gunPosition;
    public GameObject gun;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Player.instance.isArmed = !Player.instance.isArmed;
            animator.SetBool("isArmed", Player.instance.isArmed);
            GameObject Gun = Instantiate(gun);
            Gun.transform.SetParent(gunPosition,false);
            gameObject.GetComponent<Player_Animator>().SetAnimation(PlayerAnimationState.Armed);
            Debug.Log("isArmed:" + Player.instance.isArmed);
            Debug.Log("isArmedAnima:" + animator.GetBool("isArmed"));
        }
    }
}