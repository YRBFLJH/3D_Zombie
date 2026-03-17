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

    private Transform crosshair;

    private Player_Shoot playerShoot;


    private float lastShootTime;
    private float shootRate => gunData.fireRate;


    void  Awake()
    {
        playerShoot = Player.instance.GetComponent<Player_Shoot>();
    }


    void Start()
    {
        currentBulletsInMag = gunData.shootMagazineSize;
        bulletPrefab = gunData.bulletPrefab;
        isReloading = false;

        crosshair = Player.instance.GetComponent<Player_Shoot>().crosshair;

    }

    void Update()
    {
        if(Input.GetMouseButton(0) && !isReloading && Time.time >= lastShootTime + shootRate) //左键射击
        {
            if(currentBulletsInMag > 0)
            {
                Shoot();
                lastShootTime = Time.time;

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
        Ray ray = Camera.main.ScreenPointToRay(crosshair.position);

        SpawnBullet();
        
        bullet.GetComponent<BulletController>().SetShootDirection(ray.direction);

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

    void OnEnable() 
    {
        playerShoot.canShoot = true;
        lastShootTime = -shootRate; // 允许立即射击
    }

    void OnDisable() 
    {
        playerShoot.canShoot = false;
    }
}
