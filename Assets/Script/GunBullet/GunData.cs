using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
    Pistol,
    Rifle,
    Shotgun,
    Sniper
}

[CreateAssetMenu(fileName = "GunData", menuName = "CreateAssetMenu/GunData")]

public class GunData : ScriptableObject
{
    [Header("基础信息")]
    public string gunName; //名字
    public Sprite gunIcon; //图标
    public GameObject bulletPrefab; //子弹预制体
    public bool isAutomatic; //是否自动
    public WeaponType weaponType;

    [Header("射击参数")]
    public float damage; //伤害
    public float fireRate; //射速
    public float range; //射程
    public int shootMagazineSize; //射击弹夹大小(左)
    public int allMagazineSize; //总弹夹大小(右)
    public float reloadTime; //换弹时间
    public float recoilForce; //后坐力
    public int pelletsPerShot = 1; // 散弹枪弹丸数
    public float spreadAngle; // 散布角度
    public float adsZoomFOV = 40f; // 瞄准时FOV
    public bool isBoltAction; // 是否拉栓

    public GameObject holeEnemy;
    public GameObject holeBulliding;

    [Header("音效参数")]
    public AudioClip shootSound; //射击音效
    public AudioClip reloadSound; //换弹音效
    public AudioClip emptySound; //空弹音效

}
