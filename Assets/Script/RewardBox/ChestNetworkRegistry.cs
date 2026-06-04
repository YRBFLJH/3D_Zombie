using System.Collections.Generic;
using UnityEngine;

/// <summary>按 chestId 查找场景中的 ChestInventory（需在箱子上配置唯一 chestId）。</summary>
public static class ChestNetworkRegistry
{
    static readonly Dictionary<int, ChestInventory> Map = new Dictionary<int, ChestInventory>();

    public static void Register(ChestInventory chest)
    {
        if (chest == null) return;
        int id = chest.chestId;
        if (id <= 0) return;
        Map[id] = chest;
    }

    public static void Unregister(ChestInventory chest)
    {
        if (chest == null) return;
        int id = chest.chestId;
        if (Map.TryGetValue(id, out var v) && v == chest)
            Map.Remove(id);
    }

    public static bool TryGet(int chestId, out ChestInventory chest)
    {
        return Map.TryGetValue(chestId, out chest) && chest != null;
    }

    public static Dictionary<int, ChestInventory> GetAll() => new Dictionary<int, ChestInventory>(Map);
}
