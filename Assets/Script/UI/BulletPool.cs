using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;

    public GameObject bulletPrefab;
    public int initialPoolSize = 30;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(bulletPrefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject GetBullet(Vector3 position, Quaternion rotation)
    {
        GameObject bullet;
        if (pool.Count > 0)
        {
            bullet = pool.Dequeue();
        }
        else
        {
            bullet = Instantiate(bulletPrefab);
        }

        bullet.transform.position = position;
        bullet.transform.rotation = rotation;
        bullet.SetActive(true);
        return bullet;
    }

    public void ReturnBullet(GameObject bullet, float delay = 0f)
    {
        if (delay > 0)
        {
            StartCoroutine(ReturnAfterDelay(bullet, delay));
            return;
        }
        bullet.SetActive(false);
        pool.Enqueue(bullet);
    }

    System.Collections.IEnumerator ReturnAfterDelay(GameObject bullet, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bullet != null)
        {
            bullet.SetActive(false);
            pool.Enqueue(bullet);
        }
    }
}
