using UnityEngine;

public class GunController : MonoBehaviour
{
    [HideInInspector]
    public Player ownPlayer;

    public GunData gunData;
    public Transform firePoint;
    public ParticleSystem fireEffect;

    private Player_Shoot playerShoot;
    private Player_Animator playerAnimator;

    void Start()
    {
        if (ownPlayer != null)
        {
            Setup(ownPlayer);
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
        if (ownPlayer == null) return;
        ownPlayer.isArmed = gameObject.activeSelf;
        playerAnimator.PlayArmed(gameObject.activeSelf);
    }
}