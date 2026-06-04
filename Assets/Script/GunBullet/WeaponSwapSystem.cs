using System.Collections.Generic;
using UnityEngine;

public class WeaponSwapSystem : MonoBehaviour
{
    [Header("武器槽")]
    public List<GunData> weaponSlots = new List<GunData>(); // 最多4个武器槽

    [HideInInspector] public int currentWeaponIndex = -1;
    [HideInInspector] public GunController currentGun;

    private Player_ChangeHandItem changeHandItem;
    private Player_Shoot playerShoot;

    void Start()
    {
        changeHandItem = GetComponent<Player_ChangeHandItem>();
        playerShoot = GetComponent<Player_Shoot>();
    }

    void Update()
    {
        if (!GetComponent<Player>().isLocalPlayer) return;

        // 数字键切换武器
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwapToWeapon(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SwapToWeapon(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SwapToWeapon(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SwapToWeapon(3);

        // 滚轮切换
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.1f)
        {
            int next = currentWeaponIndex + 1;
            if (next >= weaponSlots.Count) next = 0;
            SwapToWeapon(next);
        }
        else if (scroll < -0.1f)
        {
            int prev = currentWeaponIndex - 1;
            if (prev < 0) prev = weaponSlots.Count - 1;
            SwapToWeapon(prev);
        }
    }

    public void AddWeapon(GunData gunData)
    {
        if (gunData == null || weaponSlots.Contains(gunData)) return;
        weaponSlots.Add(gunData);
        if (currentWeaponIndex < 0)
            SwapToWeapon(0);
    }

    void SwapToWeapon(int index)
    {
        if (index < 0 || index >= weaponSlots.Count) return;
        if (index == currentWeaponIndex) return;

        currentWeaponIndex = index;

        // 销毁当前枪
        if (currentGun != null)
        {
            Destroy(currentGun.gameObject);
            currentGun = null;
        }

        // 生成新枪
        GunData gunData = weaponSlots[index];
        changeHandItem.SpawnGun(gunData);
    }
}
