using UnityEngine;
using TMPro;

public class WorldPickupItem : MonoBehaviour
{
    public ItemData itemData;
    public int amount = 1;

    private TextMeshPro label;
    private float labelTimer;

    public void Init(ItemData item, int count)
    {
        itemData = item;
        amount = count;

        // 创建3D文字标签
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(transform);
        labelObj.transform.localPosition = Vector3.up * 0.5f;

        label = labelObj.AddComponent<TextMeshPro>();
        label.text = item.itemName + " x" + count;
        label.fontSize = 36;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        labelObj.transform.localScale = Vector3.one * 0.1f;

        // 简单的碰撞器用于拾取检测
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;

        // 5分钟后自动消失
        Destroy(gameObject, 300f);
    }

    void Update()
    {
        // 标签始终面向摄像机
        if (label != null && Camera.main != null)
        {
            label.transform.rotation = Camera.main.transform.rotation;
        }

        // 上下浮动效果
        transform.position += Vector3.up * Mathf.Sin(Time.time * 3f) * 0.002f;

        labelTimer += Time.deltaTime;
        if (labelTimer > 60f && label != null)
        {
            label.gameObject.SetActive(Mathf.FloorToInt(labelTimer) % 2 == 0); // 闪烁
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerBackpack backpack = other.GetComponent<PlayerBackpack>();
        if (backpack == null) return;

        Player player = other.GetComponent<Player>();
        if (player == null || !player.isLocalPlayer) return;

        backpack.AddItem(itemData, amount);
        Destroy(gameObject);
    }
}
