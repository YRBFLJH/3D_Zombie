using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BulletData", menuName = "CreateAssetMenu/BulletData")]

public class BulletData : ScriptableObject
{
   [Header("子弹物理属性")]
   public float speed; //速度
   public float gravityScale; //重力影响
   public float lifeTime; //生命周期

   [Header("子弹表现属性")]
   public Sprite bulletIcon; //图标
   public AudioClip hitSound; //击中音效
}
