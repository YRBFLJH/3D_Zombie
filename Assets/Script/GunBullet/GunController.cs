using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    public GunData gunData;

    public Transform firePoint;

    private int currentBulletsInMag; //弹夹剩余子弹
    private bool isReloading;
    private GameObject bulletPrefab;
    private GameObject bullet;


    void Start()
    {
        currentBulletsInMag = gunData.shootMagazineSize;
        bulletPrefab = gunData.bulletPrefab;
        isReloading = false;
    }

    void Update()
    {
        if(Input.GetMouseButtonDown(0) && !isReloading) //左键射击
        {
            if(currentBulletsInMag > 0)
            {
                Shoot();
            }
            // else
            // {
            //     //播放空弹音效
            //     AudioSource.PlayClipAtPoint(gunData.emptySound, transform.position);
            // }
        }

        if(Input.GetKeyDown(KeyCode.R) && !isReloading) //按R换弹
        {
            if(currentBulletsInMag < gunData.shootMagazineSize)
            {
                Reload();
            }
        }
    }

    void Shoot()
    {
        SpawnBullet();

        //减少弹夹中的子弹数量
        currentBulletsInMag--;
    }

    void Reload()
    {
        isReloading = true;
        //播放换弹音效
        // AudioSource.PlayClipAtPoint(gunData.reloadSound, transform.position);
        // //等待换弹时间
        // Invoke("FinishReloading", gunData.reloadTime);
    }

    void SpawnBullet()
    {
        bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
    }
}
