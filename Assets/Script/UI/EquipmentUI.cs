using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipmentUI : MonoBehaviour
{
    public static EquipmentUI instance;

    [Header("装备槽")]
    public Image headSlotIcon;
    public Image bodySlotIcon;
    public Image weaponSlot1Icon;
    public Image weaponSlot2Icon;

    public TextMeshProUGUI defenseText;
    public TextMeshProUGUI attackText;

    private Player player;
    private PlayerBackpack backpack;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<Player>();
            backpack = playerObj.GetComponent<PlayerBackpack>();
        }
    }

    public void RefreshUI()
    {
        if (player == null) return;

        if (headSlotIcon != null && player.equippedHead != null)
            headSlotIcon.sprite = player.equippedHead.icon;
        if (bodySlotIcon != null && player.equippedBody != null)
            bodySlotIcon.sprite = player.equippedBody.icon;
        if (weaponSlot1Icon != null && player.equippedWeapon1 != null)
            weaponSlot1Icon.sprite = player.equippedWeapon1.icon;
        if (weaponSlot2Icon != null && player.equippedWeapon2 != null)
            weaponSlot2Icon.sprite = player.equippedWeapon2.icon;

        float def = 0f;
        if (player.equippedHead != null) def += player.equippedHead.attackOrdefense;
        if (player.equippedBody != null) def += player.equippedBody.attackOrdefense;
        float atk = 0f;
        if (player.equippedWeapon1 != null) atk += player.equippedWeapon1.attackOrdefense;
        if (player.equippedWeapon2 != null) atk += player.equippedWeapon2.attackOrdefense;

        if (defenseText != null) defenseText.text = "防御: " + def;
        if (attackText != null) attackText.text = "攻击: " + atk;
    }
}
