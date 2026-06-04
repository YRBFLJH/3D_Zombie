using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LootEntry
{
    public ItemData item;
    [Range(0f, 1f)] public float dropChance = 0.5f;
    public int minAmount = 1;
    public int maxAmount = 1;
}

[CreateAssetMenu(fileName = "LootTable", menuName = "ZombieGame/LootTable")]
public class LootTable : ScriptableObject
{
    public List<LootEntry> entries = new List<LootEntry>();
}
