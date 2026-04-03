using System.Collections;
using UnityEngine;
using Mirror;
using Mirror.BouncyCastle.Crypto.Utilities;

public class BulletController : NetworkBehaviour
{
    public BulletData bulletData;
    private Rigidbody rb;

    float damage; 

    void Update()
    {
        
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 仅服务器调用：初始化子弹
    [Server]
    public void InitBullet(Vector3 dir,float damage)
    {
        rb.velocity = dir * bulletData.speed;

        this.damage = damage;
        
        // 服务器定时销毁
        StartCoroutine(DestroyAfter(bulletData.lifeTime));
    }

    IEnumerator DestroyAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gameObject != null && isServer)
            NetworkServer.Destroy(gameObject);
    }

    [ServerCallback]
    void OnTriggerEnter(Collider other)
    {
        if (other.tag != "Enemy") return;

        Enemy_Controller enemy = other.GetComponentInParent<Enemy_Controller>();
        enemy.TakeDamage(damage);
    }
}