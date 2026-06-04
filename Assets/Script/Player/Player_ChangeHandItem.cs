using UnityEngine;

public class Player_ChangeHandItem : MonoBehaviour
{
    Player Player;

    public GameObject GunPrefab;

    GameObject currentGun;          // 当前持有的枪引用

    public Transform GunSpawnPoint;

    void Awake()
    {

    }

    void Start()
    {
        Player = GetComponent<Player>();
    }

    void Update()
    {
        if (Player == null || !Player.isLocalPlayer) return;
        // 任何UI打开时（箱子或背包）禁止切换武器
        if (BackpackManage.currentOpenChest != null) return;
        if (BackpackManage.Instance != null && BackpackManage.Instance.showBackpack.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Tab))
        {   
            if (currentGun == null)
            {
                SpawnGun();
            }
            else
            {
                currentGun.SetActive(!currentGun.activeSelf);
            }
        }
    }

    void SpawnGun()
    {
        if (GunPrefab == null || GunSpawnPoint == null)
        {
            Debug.LogWarning("Player_ChangeHandItem: GunPrefab 或 GunSpawnPoint 未设置，无法生成枪。");
            return;
        }

        GameObject gun = Instantiate(GunPrefab);
        gun.transform.SetParent(GunSpawnPoint, false);
        GunController gunCtrl = gun.GetComponent<GunController>();
        if (gunCtrl != null && Player != null)
        {
            // 仅本地玩家绑定ownPlayer，远端只做表现不需要输入链路
            gunCtrl.ownPlayer = Player;
        }

        currentGun = gun;
    }

    public void SpawnGun(GunData gunData)
    {
        if (GunSpawnPoint == null)
        {
            Debug.LogWarning("Player_ChangeHandItem: GunSpawnPoint 未设置");
            return;
        }

        // 动态创建枪GameObject，挂载GunController并赋值gunData
        GameObject gun = new GameObject(gunData.gunName + "_Dynamic");
        gun.transform.SetParent(GunSpawnPoint, false);

        GunController gunCtrl = gun.AddComponent<GunController>();
        gunCtrl.gunData = gunData;
        gunCtrl.ownPlayer = Player;

        // 如果是动态创建，不需要firePoint和fireEffect从prefab获取
        // 使用GunSpawnPoint位置作为firePoint
        gunCtrl.firePoint = GunSpawnPoint;

        currentGun = gun;

        WeaponSwapSystem swapSystem = GetComponent<WeaponSwapSystem>();
        if (swapSystem != null) swapSystem.currentGun = gunCtrl;
    }

    public void SetArmedStateByNetwork(bool armed)
    {
        // 本地玩家不走网络驱动
        if (Player != null && Player.isLocalPlayer) return;
        if (armed && currentGun == null)
        {
            SpawnGun();
        }
        if (currentGun != null)
        {
            currentGun.SetActive(armed);
        }
    }

    public void PlayRemoteShootEffect(Vector3 firePos, Vector3 fireDir)
    {
        if (currentGun == null) return;
        GunController gunCtrl = currentGun.GetComponent<GunController>();
        if (gunCtrl == null) return;

        if (gunCtrl.fireEffect != null)
        {
            gunCtrl.fireEffect.Play();
        }

        if (gunCtrl.gunData != null && gunCtrl.gunData.bulletPrefab != null)
        {
            GameObject visualBullet = Instantiate(
                gunCtrl.gunData.bulletPrefab,
                firePos,
                Quaternion.LookRotation(fireDir) * Quaternion.Euler(90, 0, 0)
            );
            Rigidbody rb = visualBullet.GetComponent<Rigidbody>();
            BulletController bc = visualBullet.GetComponent<BulletController>();
            if (rb != null && bc != null && bc.bulletData != null)
            {
                rb.velocity = fireDir * bc.bulletData.speed;
            }
            Destroy(visualBullet, 0.1f);
        }

        // 远端弹孔
        if (gunCtrl.gunData != null)
        {
            GunData gd = gunCtrl.gunData;
            int layerMask = LayerMask.GetMask("Enemy", "Ground", "Obstacle");
            if (Physics.Raycast(firePos, fireDir, out RaycastHit hit, 200f, layerMask))
            {
                bool isEnemy = hit.collider.CompareTag("Enemy");
                GameObject prefab = isEnemy ? gd.holeEnemy : gd.holeBulliding;
                if (prefab != null)
                {
                    Quaternion rot = Quaternion.LookRotation(-hit.normal);
                    GameObject hole = Instantiate(prefab, hit.point, rot);
                    if (isEnemy)
                    {
                        Collider[] hits = Physics.OverlapSphere(hit.point, 0.01f, LayerMask.GetMask("Enemy"));
                        if (hits.Length > 0) hole.transform.SetParent(hits[0].transform);
                        hole.transform.position += hit.normal * 0.00005f;
                    }
                    else
                    {
                        hole.transform.position += hit.normal * 0.002f;
                    }
                    Destroy(hole, 8f);
                }
            }
        }
    }
}