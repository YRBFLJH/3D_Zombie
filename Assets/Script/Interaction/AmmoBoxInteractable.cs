using UnityEngine;

public class AmmoBoxInteractable : InteractableBase
{
    public float cooldown = 30f;
    private float lastUsedTime = -999f;

    public override void OnInteract(Player player)
    {
        if (Time.time < lastUsedTime + cooldown) return;

        Player_Shoot shoot = player.GetComponent<Player_Shoot>();
        if (shoot == null || shoot.leftBullet >= 999) return;

        // 补满子弹
        GunController gun = shoot.currentGun;
        if (gun != null)
        {
            shoot.rightBullet = gun.gunData.allMagazineSize;
            shoot.leftBullet = Mathf.Min(shoot.leftBullet, gun.gunData.shootMagazineSize);
        }

        lastUsedTime = Time.time;
        Debug.Log("弹药已补充");
    }

    public override bool CanInteract(Player player)
    {
        return Time.time >= lastUsedTime + cooldown;
    }
}
