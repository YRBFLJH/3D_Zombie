using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    public GunData gunData;

    public Transform firePoint;


    private int currentBulletsInMag; //弹夹剩余子弹
    private int allBullets; //总子弹数量

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
        allBullets = gunData.allMagazineSize;


        bulletPrefab = gunData.bulletPrefab;
        isReloading = false;

        crosshair = Player.instance.GetComponent<Player_Shoot>().crosshair;

    }

    void Update()
    {
        if(Input.GetMouseButton(0) && !isReloading && Time.time >= lastShootTime + shootRate && playerShoot.isAiming) //左键射击
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

        if(Input.GetKeyDown(KeyCode.R) && !isReloading && Player.instance.isArmed) //按R换弹
        {
            if(currentBulletsInMag < gunData.shootMagazineSize)
            {
                isReloading = true;
                playerShoot.Reload();
            }
        }

        BulletAmoutInstance.instance.UpdateBulletAmount(currentBulletsInMag, allBullets);
    }

    void Shoot()
    {
        Ray ray = Camera.main.ScreenPointToRay(crosshair.position);

        SpawnBullet();
        
        bullet.GetComponent<BulletController>().SetShootDirection(ray.direction);

        //减少弹夹中的子弹数量
        currentBulletsInMag--;
    }

    void SpawnBullet()
    {
        bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
    }

    void OnEnable() 
    {
        BulletAmoutInstance.instance.All.SetActive(true);
        playerShoot.canShoot = true;
        lastShootTime = -shootRate; // 允许立即射击
        playerShoot.SetCurrentGun(this);
    }

    void OnDisable() 
    {
        if (BulletAmoutInstance.instance != null)
            BulletAmoutInstance.instance.All.SetActive(false);
        if (playerShoot != null)
        {
            playerShoot.canShoot = false;
            playerShoot.SetCurrentGun(null);
        }
    }

    public void FinishReload()
    {
        allBullets -= gunData.shootMagazineSize - currentBulletsInMag;
        currentBulletsInMag = gunData.shootMagazineSize;
        isReloading = false;
    }

}
