using System.Collections;
using UnityEngine;
using System.Collections.Generic; // 必须加，用于遍历字典

public class BulletController : MonoBehaviour
{
    public BulletData bulletData;
    private Rigidbody rb;
    float damage;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void InitBullet(Vector3 dir, float damage)
    {
        rb.velocity = dir * bulletData.speed;
        this.damage = damage;
        StartCoroutine(DestroyAfter(bulletData.lifeTime));
    }

    IEnumerator DestroyAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gameObject != null)
            Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
    }
}