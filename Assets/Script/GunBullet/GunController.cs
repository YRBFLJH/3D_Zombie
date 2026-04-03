using Mirror;
using UnityEngine;

public class GunController : NetworkBehaviour
{
    [HideInInspector]
    [SyncVar] public uint ownerNetId; // uint:int的0 ~ 正整数

    [HideInInspector]
    public Player ownPlayer;

    public GunData gunData;
    public Transform firePoint;

    private Player_Shoot playerShoot;
    private Player_Animator playerAnimator;

    void Start()
    {
        if (isClient && ownPlayer == null && ownerNetId != 0)
        {
            BindToOwner();
        }

        if (isServer && ownPlayer != null)
        {
            Setup(ownPlayer);
        }

        if (isClient && transform.parent == null) BindToOwner(); // 防止客户端后进来的不同步现象
    }

    void BindToOwner()
    {
        GameObject owner = NetworkClient.spawned[ownerNetId]?.gameObject;
        if (owner != null)
        {
            ownPlayer = owner.GetComponent<Player>();
            Transform spawnPoint = owner.GetComponent<Player_Getcomponent>().ItemSpawnPoint;
            transform.SetParent(spawnPoint, false);
            Setup(ownPlayer);
        }
        else
        {
            // 可能玩家对象还未生成，延迟绑定（如刚进游戏就是拿着枪（存档））
            Invoke(nameof(BindToOwner), 0.2f);
        }
    }

    public void Setup(Player player)
    {
        ownPlayer = player;
        playerShoot = ownPlayer.GetComponent<Player_Shoot>();
        playerAnimator = ownPlayer.GetComponent<Player_Animator>();

        playerShoot.SetCurrentGun(this);
        UpdateState();
        playerShoot.ShowHideBulletUI();

        if (playerShoot.rightBullet > 0 && playerShoot.leftBullet < gunData.shootMagazineSize)
            playerShoot.Reload();
    }

    void OnEnable()
    {
        if (ownPlayer == null) return;
        UpdateState();
        playerShoot.ShowHideBulletUI();

        if (playerShoot.rightBullet > 0 && playerShoot.leftBullet < gunData.shootMagazineSize)
            playerShoot.Reload();
    }

    void OnDisable()
    {
        if (ownPlayer == null) return;
        UpdateState();
        playerShoot.ShowHideBulletUI();
    }

    void UpdateState()
    {
        if (ownPlayer == null || !ownPlayer.isLocalPlayer) return;
        ownPlayer.isArmed = gameObject.activeSelf;
        playerAnimator.PlayArmed(gameObject.activeSelf);
    }
}