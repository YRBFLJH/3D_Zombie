using UnityEngine;

public class DoorInteractable : InteractableBase
{
    public Animator doorAnimator;
    public bool isOpen;
    public bool requireKey;
    public int requiredKeyItemId;

    public override void OnInteract(Player player)
    {
        if (requireKey)
        {
            PlayerBackpack backpack = player.GetComponent<PlayerBackpack>();
            if (backpack != null)
            {
                bool hasKey = false;
                foreach (var item in backpack.items)
                {
                    if (item.itemId == requiredKeyItemId)
                    {
                        hasKey = true;
                        backpack.RemoveItem(item);
                        requireKey = false;
                        break;
                    }
                }
                if (!hasKey)
                {
                    Debug.Log("需要钥匙！");
                    return;
                }
            }
        }

        isOpen = !isOpen;
        if (doorAnimator != null)
            doorAnimator.SetBool("isOpen", isOpen);

        promptText = isOpen ? "按 E 关门" : "按 E 开门";
    }

    public override bool CanInteract(Player player)
    {
        return true;
    }
}
