using Cinemachine;
using Mirror;
using UnityEngine;

public class Player_Shoot : NetworkBehaviour                                 
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
    float lastShootTime;
    float shootRate => currentGun.gunData.fireRate;
    [HideInInspector]
    [SyncVar(hook = nameof(OnLeftBulletChanged))] public int leftBullet;
    [HideInInspector]
    [SyncVar(hook = nameof(OnRightBulletChanged))] public int rightBullet;
    GameObject bulletPrefab => currentGun.gunData.bulletPrefab;

    void Awake()
    {
        playerGetcomponent = GetComponent<Player_Getcomponent>();
    }

    void Start()
    {
        playerAnimator = playerGetcomponent.playerAnimatorCS;
        player = playerGetcomponent.playerCS;
        if (isLocalPlayer)
        {
            crosshair = playerGetcomponent.crosshair;

            virtualCamera = playerGetcomponent.virtualCamera.GetComponent<CinemachineVirtualCamera>();
            thirdPersonCamera = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            composer = virtualCamera.GetCinemachineComponent<CinemachineComposer>();
        }
    }

    public void SetCurrentGun(GunController gun) // 枪第一次OnEnble时自动注册
    {
        currentGun = gun;
        leftBullet = currentGun.gunData.shootMagazineSize;
        rightBullet = currentGun.gunData.allMagazineSize;
    }

    void Update()
    {
        if (currentGun == null ||!isLocalPlayer) return; // 在调用任何Command/Rpc 前，都要判断主体 if (!isLocalPlayer) return;

        // 左右弹夹不为0、正在瞄准、不在射击冷却、不是换弹时可射击
        if (isAiming && Input.GetMouseButton(0) && Time.time >= lastShootTime + shootRate && rightBullet > 0 && leftBullet > 0 &&!isReloading)
        {
            Vector3 fireShootDir = Camera.main.transform.forward;
            LocalBullet(firePoint.position,fireShootDir);
            CmdShoot(firePoint.position,fireShootDir);
            lastShootTime = Time.time;
        }
        else if (Input.GetMouseButton(0) && leftBullet <= 0) // 子弹为0时射击播放空壳音效
        {
            // 音效
        }
        else if (Input.GetMouseButtonUp(0) && leftBullet <= 0) // 弹夹为0时松开左键自动换弹
        {
            Reload();
        }

        // 换弹
        if (Input.GetKeyDown(KeyCode.R) &&!isReloading && rightBullet > 0 && leftBullet < currentGun.gunData.shootMagazineSize) Reload();

        Aim();
    }

    void LocalBullet(Vector3 position,Vector3 direction) // 本地生成预测子弹，服务器子弹生成后自动同步此预测子弹位置（防延迟导致的子弹位置上视觉偏差）
    {
        GameObject visualBullet = Instantiate(bulletPrefab, position, Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0));

        // 移除网络身份组件，防止被同步
        NetworkIdentity netId = visualBullet.GetComponent<NetworkIdentity>();
        if (netId != null) Destroy(netId);

        // 子弹运动
        visualBullet.GetComponent<Rigidbody>().velocity = direction * visualBullet.GetComponent<BulletController>().bulletData.speed;

        // 自动销毁,服务器子弹生成后，预测子弹就可销毁了
        Destroy(visualBullet, 0.1f);
    }



    // 瞄准时视角变化
    void Aim()
    {
        if (player.isArmed && Input.GetMouseButton(1) && !isReloading)
        {
            playerAnimator.PlayAim(true);
            isAiming = true;
        }
        else if(!player.isArmed || !Input.GetMouseButton(1) || isReloading)
        {
            playerAnimator.PlayAim(false);
            isAiming = false;
        }

        if (isAiming)
        {
            crosshair.gameObject.SetActive(true);
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 40f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.45f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.25f, smoothSpeed * Time.deltaTime);
        }
        else
        {
            crosshair.gameObject.SetActive(false);
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 60f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2.75f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.3f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.4f, smoothSpeed * Time.deltaTime);
        }
    }

    public void ShowHideBulletUI()
    {
        if (!isLocalPlayer) return;

        BulletAmoutInstance.instance.All.SetActive(player.isArmed);
    }

    [Command]
    public void CmdShoot(Vector3 clientFirePos,Vector3 fireShootDir)
    {
        GameObject fireBullet = Instantiate(bulletPrefab, clientFirePos, Quaternion.LookRotation(fireShootDir) * Quaternion.Euler(90, 0, 0));
        fireBullet.GetComponent<BulletController>().InitBullet(fireShootDir,currentGun.gunData.damage);
        NetworkServer.Spawn(fireBullet, connectionToClient);
        TargetHideBulletVisual(connectionToClient,fireBullet);

        leftBullet--;
    }

    // 弹夹数量变化时自动更新UI（所有客户端通用）
    void OnLeftBulletChanged(int oldVal, int newVal)
    {
        if (!isLocalPlayer) return;
        BulletAmoutInstance.instance.UpdateBulletAmount(newVal, rightBullet);
    }

    void OnRightBulletChanged(int oldVal, int newVal)
    {
        if (!isLocalPlayer) return;
        BulletAmoutInstance.instance.UpdateBulletAmount(leftBullet, newVal);
    }

    public void Reload()
    {
        isReloading = true;
        playerAnimator.PlayReload();
    }

    // 结束换弹(换弹动画结束时调用的帧事件)
    void FinishReload()
    {
        if (!isLocalPlayer) return;
        CmdFinishReload();
    }

    [Command]
    void CmdFinishReload()
    {
        int needBullet = currentGun.gunData.shootMagazineSize - leftBullet;
        int realReload = Mathf.Max(0, Mathf.Min(needBullet, rightBullet));

        rightBullet -= realReload;
        leftBullet = Mathf.Min(leftBullet + realReload, currentGun.gunData.shootMagazineSize);

        RpcOnReloadFinished();
    }

    [TargetRpc] // 在目标客户端调用                  // TargetRpc: 目标客户端调用(单一)，修改自身状态、只有自己看得到的UI
    void RpcOnReloadFinished()                     //  ClientRpc: 所有客户端调用(广播)，修改场景中某个已经存在的物体的信息（为了让所有人看到变化）                     
    {                                              //  ServerRpc: 服务器（绝对权威）调用，修改数据、生成或摧毁物体              
        isReloading = false;                       //  Command: 客户端向服务器发送请求执行方法
    }

    [TargetRpc] // 隐藏服务器子弹
    void TargetHideBulletVisual(NetworkConnection target, GameObject bullet)
    {
        bullet.GetComponent<MeshRenderer>().enabled = false;
    }
}