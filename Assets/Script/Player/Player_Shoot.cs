using Cinemachine;
using UnityEngine;

public enum BulletHoleType
{
    Building,
    Enemy
}
public class Player_Shoot : MonoBehaviour
{
    Player_Getcomponent playerGetcomponent;
    Player_Animator playerAnimator;
    Player player;

    GunController currentGun;
    Transform crosshair;
    CinemachineVirtualCamera virtualCamera;
    Cinemachine3rdPersonFollow thirdPersonCamera;
    CinemachineComposer composer;

    [HideInInspector]
    public bool isAiming;

    float smoothSpeed = 5f; // 瞄准时灵敏度

    [HideInInspector]
    public bool isReloading;

    // 枪弹数据
    Transform firePoint => currentGun.firePoint;
    ParticleSystem fireEffect => currentGun.fireEffect;

    // 射线
    int maxRayDistance = 500;
    
    float lastShootTime;
    float shootRate => currentGun.gunData.fireRate;
    [HideInInspector]
    public int leftBullet;
    [HideInInspector]
    public int rightBullet;
    GameObject bulletPrefab => currentGun.gunData.bulletPrefab;

    void Awake()
    {
        playerGetcomponent = GetComponent<Player_Getcomponent>();
    }

    void Start()
    {
        playerAnimator = playerGetcomponent.playerAnimatorCS;
        player = playerGetcomponent.playerCS;
        
        crosshair = playerGetcomponent.crosshair;
        virtualCamera = playerGetcomponent.virtualCamera.GetComponent<CinemachineVirtualCamera>();
        thirdPersonCamera = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        composer = virtualCamera.GetCinemachineComponent<CinemachineComposer>();
    }

    public void SetCurrentGun(GunController gun) // 枪第一次OnEnble时自动注册
    {
        currentGun = gun;
        leftBullet = currentGun.gunData.shootMagazineSize;
        rightBullet = currentGun.gunData.allMagazineSize;
        UpdateBulletUI();
    }

    void Update()
    {
        if (currentGun == null) return;

        // 左右弹夹不为0、正在瞄准、不在射击冷却、不是换弹时可射击
        if (isAiming && Input.GetMouseButton(0) && Time.time >= lastShootTime + shootRate && leftBullet > 0 && !isReloading)
        {
            Ray aimRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            Vector3 shootDir = aimRay.direction;

            Vector3 fireShootDir = Camera.main.transform.forward;
            fireEffect.Play();
            
            // 本地生成预测子弹
            LocalBullet(firePoint.position, fireShootDir);
            
            // 直接执行射击逻辑（原CmdShoot内容）
            Shoot(firePoint.position, fireShootDir);
            
            // 直接执行射线检测（原CmdRay内容）
            ShootRay(shootDir);
            
            lastShootTime = Time.time;
        }
        else if (Input.GetMouseButton(0) && leftBullet <= 0) // 子弹为0时射击播放空壳音效
        {
            // 音效
        }
        else if (Input.GetMouseButtonUp(0) && leftBullet <= 0 && rightBullet > 0) // 弹夹为0时松开左键自动换弹
        {
            Reload();
        }

        // 换弹
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && rightBullet > 0 && leftBullet < currentGun.gunData.shootMagazineSize) Reload();

        Aim();
    }

    void LocalBullet(Vector3 position, Vector3 direction) // 本地生成预测子弹
    {
        GameObject visualBullet = Instantiate(bulletPrefab, position, Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0));
        visualBullet.GetComponent<Rigidbody>().velocity = direction * visualBullet.GetComponent<BulletController>().bulletData.speed;
        Destroy(visualBullet, 0.1f);
    }

    void Shoot(Vector3 firePos, Vector3 fireShootDir)
    {
        GameObject fireBullet = Instantiate(bulletPrefab, firePos, Quaternion.LookRotation(fireShootDir) * Quaternion.Euler(90, 0, 0));
        fireBullet.GetComponent<BulletController>().InitBullet(fireShootDir, currentGun.gunData.damage);
        
        leftBullet--;
        UpdateBulletUI();
    }

    void ShootRay(Vector3 shootDir)
    {
        int layerMask = LayerMask.GetMask("Enemy", "Ground", "Obstacle");
        Ray ray = new Ray(Camera.main.transform.position, shootDir);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, layerMask))
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                Debug.Log("攻击到了敌人");
                Enemy_Controller enemy = hit.collider.GetComponentInParent<Enemy_Controller>();
                enemy.TakeDamage(currentGun.gunData.damage);
            }

            SpawnBulletHole(hit.point, hit.normal, hit.collider.CompareTag("Enemy"));
        }
    }

    void SpawnBulletHole(Vector3 position, Vector3 normal, bool isEnemyHit)
    {
        if (currentGun == null) return;

        BulletHoleType holeType = isEnemyHit ? BulletHoleType.Enemy : BulletHoleType.Building;

        GameObject prefab;
        if (holeType == BulletHoleType.Enemy)
            prefab = currentGun.gunData.holeEnemy;
        else
            prefab = currentGun.gunData.holeBulliding;

        if (prefab == null) return;

        Quaternion rot = Quaternion.LookRotation(-normal);
        GameObject hole = Instantiate(prefab, position, rot);

        if (isEnemyHit)
        {
            Transform enemyParent = null;
            if (normal != Vector3.zero)
            {
                Collider hitCollider = Physics.OverlapSphere(position, 0.01f, LayerMask.GetMask("Enemy"))[0];
                if (hitCollider != null)
                {
                    enemyParent = hitCollider.transform;
                }
            }

            if (enemyParent != null)
            {
                hole.transform.SetParent(enemyParent);
            }
        }

        if (isEnemyHit) hole.transform.position += normal * 0.00005f;
        else hole.transform.position += normal * 0.002f;

        Destroy(hole, 8f);
    }

    // 瞄准时视角变化
    void Aim()
    {
        if (player.isArmed && Input.GetMouseButton(1) && !isReloading)
        {
            playerAnimator.PlayAim(true);
            isAiming = true;
        }
        else if (!player.isArmed || !Input.GetMouseButton(1) || isReloading)
        {
            playerAnimator.PlayAim(false);
            isAiming = false;
        }

        if (isAiming)
        {
            crosshair.gameObject.SetActive(true);
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 40f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.Damping = new Vector3(0, 0, 0);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.45f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.25f, smoothSpeed * Time.deltaTime);
        }
        else
        {
            crosshair.gameObject.SetActive(false);
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 60f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.Damping = new Vector3(0.25f, 0.25f, 0.25f);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2.75f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.3f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.4f, smoothSpeed * Time.deltaTime);
        }
    }

    public void ShowHideBulletUI()
    {
        BulletAmoutInstance.instance.All.SetActive(player.isArmed);
    }

    void UpdateBulletUI()
    {
        BulletAmoutInstance.instance.UpdateBulletAmount(leftBullet, rightBullet);
    }

    public void Reload()
    {
        isReloading = true;
        playerAnimator.PlayReload();
    }

    // 结束换弹(换弹动画结束时调用的帧事件)
    void FinishReload()
    {
        int needBullet = currentGun.gunData.shootMagazineSize - leftBullet;
        int realReload = Mathf.Max(0, Mathf.Min(needBullet, rightBullet));

        rightBullet -= realReload;
        leftBullet = Mathf.Min(leftBullet + realReload, currentGun.gunData.shootMagazineSize);

        UpdateBulletUI();
        isReloading = false;
    }
}