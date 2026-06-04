using UnityEngine;

public abstract class InteractableBase : MonoBehaviour
{
    [Header("交互提示")]
    public string promptText = "按 E 交互";
    public float interactDuration;
    public bool requireHold;

    protected float currentTime;
    protected bool isInteracting;

    public abstract void OnInteract(Player player);
    public virtual bool CanInteract(Player player) { return true; }

    public void StartInteraction(Player player)
    {
        if (isInteracting) return;
        isInteracting = true;
        currentTime = interactDuration;
    }

    public void CancelInteraction()
    {
        isInteracting = false;
    }

    protected virtual void Update()
    {
        if (!isInteracting) return;

        if (requireHold && interactDuration > 0)
        {
            currentTime -= Time.deltaTime;
            if (currentTime <= 0)
            {
                isInteracting = false;
                // 需要外部传入player引用
            }
        }
    }
}
