using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance;
    public bool isArmed;

    [HideInInspector]
    public CharacterController characterController;

    Player_Getcomponent playerGetcomponent;


    [HideInInspector]
    public int level,gold;

    [HideInInspector]
    public string playerName;

    [HideInInspector]
    public bool isLocalPlayer;

    [HideInInspector] public EquipmentData equippedHead;
    [HideInInspector] public EquipmentData equippedBody;
    [HideInInspector] public EquipmentData equippedWeapon1;
    [HideInInspector] public EquipmentData equippedWeapon2;
    [HideInInspector] public EquipmentData equippedWeapon3;

    public float defenseValue
    {
        get
        {
            float d = 0;
            if (equippedHead != null) d += equippedHead.attackOrdefense;
            if (equippedBody != null) d += equippedBody.attackOrdefense;
            return d;
        }
    }

    public float attackBonus
    {
        get
        {
            float a = 0;
            if (equippedWeapon1 != null) a += equippedWeapon1.attackOrdefense;
            if (equippedWeapon2 != null) a += equippedWeapon2.attackOrdefense;
            return a;
        }
    }

    public void Equip(EquipmentData equip)
    {
        switch (equip.equipmentType)
        {
            case EquipmentType.Head:
                UnequipSlot(ref equippedHead); equippedHead = equip; break;
            case EquipmentType.Body:
                UnequipSlot(ref equippedBody); equippedBody = equip; break;
            case EquipmentType.Weapon1:
                UnequipSlot(ref equippedWeapon1); equippedWeapon1 = equip; break;
            case EquipmentType.Weapon2:
                UnequipSlot(ref equippedWeapon2); equippedWeapon2 = equip; break;
            case EquipmentType.Weapon3:
                UnequipSlot(ref equippedWeapon3); equippedWeapon3 = equip; break;
        }
        EquipmentUI.instance?.RefreshUI();
    }

    void UnequipSlot(ref EquipmentData slot)
    {
        if (slot != null)
        {
            // 卸下装备放回背包
            PlayerBackpack bp = GetComponent<PlayerBackpack>();
            if (bp != null) bp.AddItem(slot, 1);
        }
        slot = null;
    }

    void Awake()
    {
        Instance = this;
        playerGetcomponent = GetComponent<Player_Getcomponent>();
    }

    void Start()
    {
        characterController = playerGetcomponent.characterController;
    }

    // 瞬移 (供外部调用:回档、技能)
    public void Teleportation(Vector3 endPosition)
    {
        characterController.enabled = false;
        transform.position = endPosition;
        characterController.enabled = true;
    }

    
}
