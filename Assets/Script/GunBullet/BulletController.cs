using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    public BulletData bulletData;
    private Rigidbody rb;
    private Vector3 shootDirection;

    public void SetShootDirection(Vector3 dir)
    {
        shootDirection = dir;

        transform.rotation = Quaternion.LookRotation(shootDirection) * Quaternion.Euler(90, 0, 0);
    }
   

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        Destroy(gameObject, bulletData.lifeTime);
    }

    void Update()
    {
        rb.velocity = shootDirection * bulletData.speed;
    }
}
