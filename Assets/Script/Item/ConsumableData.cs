using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ConsumableType // 消耗品类型
{
    Health,  // 恢复生命
    Stamina, //恢复体力
}

[CreateAssetMenu(fileName = "Consumable", menuName = "CreateAssetMenu/Item/Consumable")]
public class ConsumableData : ItemData
{
    public float restoreValue; // 恢复数值

    public override void Use(Player user) // 使用消耗品(重铸父类方法)
    {
        base.Use(user);
    }
}
