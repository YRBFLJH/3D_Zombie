using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自动存档触发器：玩家退出/断开时自动发送存档到服务端。
/// 个人数据：每个玩家各自保存
/// 世界数据：仅房主保存（AI、箱子、波次）
/// </summary>
public class AutoSaveHandler : MonoBehaviour
{
    public static int savedLeftBullet = -1;
    public static int savedRightBullet;
    public static bool hasSavedAmmo;

    public static void ResetSavedAmmo()
    {
        hasSavedAmmo = false;
        savedLeftBullet = -1;
    }

    Player localPlayer;
    Player_State playerState;
    PlayerBackpack playerBackpack;
    Player_Shoot playerShoot;

    void Awake()
    {
        localPlayer = GetComponent<Player>();
        playerState = GetComponent<Player_State>();
        playerBackpack = GetComponent<PlayerBackpack>();
        playerShoot = GetComponent<Player_Shoot>();
        // Reset saved ammo on new player spawn
        hasSavedAmmo = false;
        savedLeftBullet = -1;
    }

    void Start()
    {
        if (NetworkManager.instance != null)
        {
            NetworkManager.instance.OnPlayerLoadData += OnPlayerLoadData;
            if (NetworkManager.instance.pendingLoadData != null)
            {
                var data = NetworkManager.instance.pendingLoadData;
                NetworkManager.instance.pendingLoadData = null;
                // Delay by 1 frame so weapon init finishes before we apply saved values
                StartCoroutine(DelayedApplyLoad(data));
            }
        }
    }

    IEnumerator DelayedApplyLoad(SavePlayerData data)
    {
        yield return null; // Wait 1 frame for all Start() to finish
        ApplyLoadedData(data);
    }

    void OnDestroy()
    {
        if (NetworkManager.instance != null)
        {
            NetworkManager.instance.OnPlayerLoadData -= OnPlayerLoadData;
            NetworkManager.instance.pendingLoadData = null;
        }
        SendAutoSave();
    }

    void OnApplicationQuit()
    {
        SendAutoSave();
    }

    public void SendAutoSave()
    {
        if (NetworkManager.instance == null || !NetworkManager.instance.IsConnected)
            return;

        // 玩家不在房间中则跳过
        if (NetworkManager.instance.CurrentRoomId < 0)
            return;

        int roomId = NetworkManager.instance.CurrentRoomId;

        // 发送个人存档
        var playerData = CollectPlayerData();
        if (playerData != null)
        {
            NetworkManager.instance.SendPlayerSave(roomId, playerData);
            Debug.Log("[存档] 已发送个人存档");
        }

        // 房主额外发送世界存档
        if (NetworkManager.instance.isHost)
        {
            Debug.Log("[存档] isHost=true, 开始收集世界数据...");
            try
            {
                var worldData = CollectWorldData(roomId);
                Debug.Log($"[存档] 世界数据: 敌人={worldData.enemies.Count}, 箱子={worldData.chests.Count}");
                NetworkManager.instance.SendWorldSave(roomId, worldData);
                Debug.Log("[存档] 已发送世界存档（房主）");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[存档] 世界存档发送失败: {ex}");
            }
        }
        else
        {
            Debug.Log("[存档] isHost=false, 跳过世界存档");
        }
    }

    SavePlayerData CollectPlayerData()
    {
        if (localPlayer == null || playerState == null) return null;

        var data = new SavePlayerData
        {
            playerId = NetworkManager.instance.playerId,
            posX = transform.position.x,
            posY = transform.position.y,
            posZ = transform.position.z,
            hp = playerState.health,
            maxHp = playerState.maxHealth,
            food = playerState.satiety,
            water = playerState.thirst,
            level = localPlayer.level,
            gold = localPlayer.gold,
            equippedHeadId = localPlayer.equippedHead != null ? localPlayer.equippedHead.id : 0,
            equippedBodyId = localPlayer.equippedBody != null ? localPlayer.equippedBody.id : 0,
            equippedWeapon1Id = localPlayer.equippedWeapon1 != null ? localPlayer.equippedWeapon1.id : 0,
            equippedWeapon2Id = localPlayer.equippedWeapon2 != null ? localPlayer.equippedWeapon2.id : 0,
            equippedWeapon3Id = localPlayer.equippedWeapon3 != null ? localPlayer.equippedWeapon3.id : 0,
            respawnCount = playerState.respawnCount,
            totalKills = GameManager.Instance != null ? GameManager.Instance.totalKills : 0,
            leftBullet = playerShoot != null ? playerShoot.leftBullet : 0,
            rightBullet = playerShoot != null ? playerShoot.rightBullet : 0,
        };
        Debug.Log($"[存档] CollectPlayerData: leftBullet={data.leftBullet}, rightBullet={data.rightBullet}");

        // 背包物品
        if (playerBackpack != null)
        {
            foreach (var item in playerBackpack.items)
            {
                if (item.item == null) continue;
                data.inventoryItems.Add(new SaveInventoryItem
                {
                    itemId = item.itemId,
                    amount = item.amount,
                    x = item.x,
                    y = item.y,
                    gridType = item.gridType,
                    gridIndex = item.gridIndex,
                    rotated = item.isRotated,
                });
            }
        }

        return data;
    }

    SaveWorldData CollectWorldData(int roomId)
    {
        var data = new SaveWorldData { roomId = roomId };

        // AI enemies (save all)
        var enemyList = FindObjectsOfType<Enemy_Controller>(true); // true = include inactive
        Debug.Log($"[存档] CollectWorldData: 找到 {enemyList.Length} 个敌人");
        foreach (var ai in enemyList)
        {
            float hp = ai.isDead ? 0f : ai.health;

            data.enemies.Add(new SaveEnemyData
            {
                enemyId = ai.enemyId,
                posX = ai.transform.position.x,
                posY = ai.transform.position.y,
                posZ = ai.transform.position.z,
                hp = hp,
                state = ai.isDead ? 4 : 1,  // 4=Dead, 1=Walk
                isDead = ai.isDead,
            });
        }

        // 箱子
        var chestMap = ChestNetworkRegistry.GetAll();
        Debug.Log($"[存档] CollectWorldData: 找到 {chestMap.Count} 个箱子");
        foreach (var kvp in chestMap)
        {
            var chestData = new SaveChestData { chestId = kvp.Key };
            var inv = kvp.Value;
            if (inv != null)
            {
                foreach (var item in inv.items)
                {
                    if (item.item == null) continue;
                    chestData.items.Add(new SaveInventoryItem
                    {
                        itemId = item.itemId,
                        amount = item.amount,
                        x = item.x,
                        y = item.y,
                        gridType = item.gridType,
                        gridIndex = item.gridIndex,
                        rotated = item.isRotated,
                    });
                }
            }
            data.chests.Add(chestData);
        }

        return data;
    }

    // ─── Load (restore) ───

    void OnPlayerLoadData(SavePlayerData data)
    {
        StartCoroutine(DelayedApplyLoad(data));
    }

    public void ApplyLoadedData(SavePlayerData data)
    {
        if (localPlayer == null || playerState == null) return;

        // Apply stats
        playerState.health = data.hp;
        playerState.maxHealth = data.maxHp;
        playerState.satiety = data.food;
        playerState.thirst = data.water;
        localPlayer.level = data.level;
        localPlayer.gold = data.gold;

        // Apply equipment
        localPlayer.equippedHead = BackpackManage.GetItemData(data.equippedHeadId) as EquipmentData;
        localPlayer.equippedBody = BackpackManage.GetItemData(data.equippedBodyId) as EquipmentData;
        localPlayer.equippedWeapon1 = BackpackManage.GetItemData(data.equippedWeapon1Id) as EquipmentData;
        localPlayer.equippedWeapon2 = BackpackManage.GetItemData(data.equippedWeapon2Id) as EquipmentData;
        localPlayer.equippedWeapon3 = BackpackManage.GetItemData(data.equippedWeapon3Id) as EquipmentData;

        // Apply ammo — store in static fields so SetCurrentGun can read them
        // hasSavedAmmo is set here because PlayerLoadData only arrives when save file EXISTS
        // (= not first entry). Even 0 ammo should be restored as-is.
        savedLeftBullet = data.leftBullet;
        savedRightBullet = data.rightBullet;
        hasSavedAmmo = true;
        if (playerShoot != null && playerShoot.currentGun != null)
        {
            playerShoot.leftBullet = data.leftBullet;
            playerShoot.rightBullet = data.rightBullet;
            hasSavedAmmo = false; // consumed immediately
        }
        if (BulletAmoutInstance.instance != null)
            BulletAmoutInstance.instance.UpdateBulletAmount(data.leftBullet, data.rightBullet);

        // Apply backpack items
        if (playerBackpack != null)
        {
            playerBackpack.items.Clear();
            foreach (var item in data.inventoryItems)
            {
                playerBackpack.items.Add(new InventoryItem(item.itemId, item.amount, item.x, item.y, item.gridType ?? "", item.gridIndex, item.rotated));
            }
            playerBackpack.InvokeInventoryChanged();
        }

        Debug.Log($"[存档] 玩家数据已加载: HP={data.hp}, 背包物品数={data.inventoryItems.Count}");
    }
}
