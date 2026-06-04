#pragma once
#include <cstdint>
#include <string>
#include <vector>

// Manual protobuf-style message structs for save system.
// These match the NetworkMessage.proto definitions exactly,
// avoiding the need to regenerate .pb.h/.pb.cc for save messages.

namespace savemsg {

struct SaveInventoryItem {
    int32_t itemId = 0;
    int32_t amount = 0;
    int32_t x = 0, y = 0;
    std::string gridType;
    int32_t gridIndex = 0;
    bool rotated = false;

    bool ParseFromArray(const uint8_t* data, int len);
};

struct PlayerSaveData {
    int32_t playerId = 0;
    float posX = 0, posY = 0, posZ = 0;
    float hp = 0, maxHp = 0;
    float food = 0, water = 0;
    int32_t level = 0, gold = 0;
    int32_t equippedHeadId = 0, equippedBodyId = 0;
    int32_t equippedWeapon1Id = 0, equippedWeapon2Id = 0, equippedWeapon3Id = 0;
    std::vector<SaveInventoryItem> inventoryItems;
    int32_t respawnCount = 0;
    int32_t totalKills = 0;
    int32_t leftBullet = 0;
    int32_t rightBullet = 0;

    bool ParseFromArray(const uint8_t* data, int len);
    std::vector<uint8_t> SerializeToBytes() const;
    bool ParseFromBytes(const std::vector<uint8_t>& data);
};

struct SaveEnemyData {
    int32_t enemyId = 0;
    float posX = 0, posY = 0, posZ = 0;
    float hp = 0;
    int32_t state = 0;
    bool isDead = false;

    bool ParseFromArray(const uint8_t* data, int len);
    std::vector<uint8_t> SerializeToBytes() const;
};

struct SaveChestData {
    int32_t chestId = 0;
    std::vector<SaveInventoryItem> items;

    bool ParseFromArray(const uint8_t* data, int len);
    std::vector<uint8_t> SerializeToBytes() const;
};

struct WorldSaveData {
    int32_t roomId = 0;
    float aiPosX = 0, aiPosY = 0, aiPosZ = 0;
    float aiHp = 0;
    int32_t aiState = 0;
    std::vector<SaveChestData> chests;
    std::vector<SaveEnemyData> enemies;

    bool ParseFromArray(const uint8_t* data, int len);
    std::vector<uint8_t> SerializeToBytes() const;
    bool ParseFromBytes(const std::vector<uint8_t>& data);
};

struct PlayerSaveSubmit {
    int32_t playerId = 0;
    int32_t roomId = 0;
    PlayerSaveData saveData;

    bool ParseFromArray(const uint8_t* data, int len);

    int32_t player_id() const { return playerId; }
    int32_t room_id() const { return roomId; }
    const PlayerSaveData& save_data() const { return saveData; }
};

struct WorldSaveSubmit {
    int32_t playerId = 0;
    int32_t roomId = 0;
    WorldSaveData saveData;

    bool ParseFromArray(const uint8_t* data, int len);

    int32_t player_id() const { return playerId; }
    int32_t room_id() const { return roomId; }
    const WorldSaveData& save_data() const { return saveData; }
};

struct DeleteRoomRequest {
    int32_t playerId = 0;
    int32_t roomId = 0;

    bool ParseFromArray(const uint8_t* data, int len);

    int32_t player_id() const { return playerId; }
    int32_t room_id() const { return roomId; }
};

} // namespace game
