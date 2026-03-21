using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentType
{
    Head,
    Body,
    Weapon1, //主武器
    Weapon2, //副武器
    Weapon3, //刀
}

[CreateAssetMenu(fileName = "New Equipment", menuName = "CreateAssetMenu/EquipmentData")]
public class EquipmentData : ItemData
{
    [Header("装备属性")]
    public EquipmentType equipmentType;
    public int attackOrdefense; //攻击力或者防御力
}
