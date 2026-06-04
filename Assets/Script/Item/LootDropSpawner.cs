using UnityEngine;

public static class LootDropSpawner
{
    public static void RollLoot(LootTable table, Vector3 position)
    {
        if (table == null || table.entries == null) return;

        foreach (var entry in table.entries)
        {
            if (entry.item == null) continue;
            if (Random.value > entry.dropChance) continue;

            int amount = Random.Range(entry.minAmount, entry.maxAmount + 1);
            if (amount <= 0) continue;

            // 在周围随机位置生成
            Vector3 spawnPos = position + Random.insideUnitSphere * 0.5f;
            spawnPos.y = position.y + 0.3f;

            GameObject pickupObj = new GameObject("Pickup_" + entry.item.itemName);
            pickupObj.transform.position = spawnPos;

            WorldPickupItem pickup = pickupObj.AddComponent<WorldPickupItem>();
            pickup.Init(entry.item, amount);
        }
    }
}
