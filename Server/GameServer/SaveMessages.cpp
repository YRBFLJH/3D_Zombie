#include "SaveMessages.h"
#include <iostream>
#include <cstdio>

namespace savemsg {

// ─── Wire format helpers ───

static uint32_t ReadVarint(const uint8_t* data, int& pos, int maxLen) {
    uint32_t value = 0;
    int shift = 0;
    while (pos < maxLen) {
        uint8_t byte = data[pos++];
        value |= (uint32_t)(byte & 0x7F) << shift;
        if ((byte & 0x80) == 0) break;
        shift += 7;
    }
    return value;
}

static uint64_t ReadVarint64(const uint8_t* data, int& pos, int maxLen) {
    uint64_t value = 0;
    int shift = 0;
    while (pos < maxLen) {
        uint8_t byte = data[pos++];
        value |= (uint64_t)(byte & 0x7F) << shift;
        if ((byte & 0x80) == 0) break;
        shift += 7;
    }
    return value;
}

static float ReadFloat(const uint8_t* data, int& pos) {
    uint32_t raw = (uint32_t)data[pos] | ((uint32_t)data[pos+1] << 8) |
                   ((uint32_t)data[pos+2] << 16) | ((uint32_t)data[pos+3] << 24);
    pos += 4;
    float f;
    memcpy(&f, &raw, sizeof(f));
    return f;
}

static int32_t ReadInt32(const uint8_t* data, int& pos, int maxLen) {
    return (int32_t)ReadVarint(data, pos, maxLen);
}

static bool ReadBool(const uint8_t* data, int& pos, int maxLen) {
    return ReadVarint(data, pos, maxLen) != 0;
}

static std::string ReadString(const uint8_t* data, int& pos, int maxLen) {
    int len = (int)ReadVarint(data, pos, maxLen);
    std::string s((const char*)(data + pos), len);
    pos += len;
    return s;
}

static void SkipField(int wireType, int& pos, int maxLen, const uint8_t* data) {
    switch (wireType) {
    case 0: ReadVarint(data, pos, maxLen); break; // varint
    case 1: pos += 8; break; // 64-bit
    case 2: { int len = (int)ReadVarint(data, pos, maxLen); pos += len; break; } // length-delimited
    case 5: pos += 4; break; // 32-bit
    default: break;
    }
}

// ─── SaveInventoryItem ───

bool SaveInventoryItem::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: itemId = ReadInt32(data, pos, len); break;
        case 2: amount = ReadInt32(data, pos, len); break;
        case 3: x = ReadInt32(data, pos, len); break;
        case 4: y = ReadInt32(data, pos, len); break;
        case 5: gridType = ReadString(data, pos, len); break;
        case 6: gridIndex = ReadInt32(data, pos, len); break;
        case 7: rotated = ReadBool(data, pos, len); break;
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── PlayerSaveData ───

bool PlayerSaveData::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: playerId = ReadInt32(data, pos, len); break;
        case 2: posX = ReadFloat(data, pos); break;
        case 3: posY = ReadFloat(data, pos); break;
        case 4: posZ = ReadFloat(data, pos); break;
        case 5: hp = ReadFloat(data, pos); break;
        case 6: maxHp = ReadFloat(data, pos); break;
        case 7: food = ReadFloat(data, pos); break;
        case 8: water = ReadFloat(data, pos); break;
        case 9: level = ReadInt32(data, pos, len); break;
        case 10: gold = ReadInt32(data, pos, len); break;
        case 11: equippedHeadId = ReadInt32(data, pos, len); break;
        case 12: equippedBodyId = ReadInt32(data, pos, len); break;
        case 13: equippedWeapon1Id = ReadInt32(data, pos, len); break;
        case 14: equippedWeapon2Id = ReadInt32(data, pos, len); break;
        case 15: equippedWeapon3Id = ReadInt32(data, pos, len); break;
        case 16: {
            int innerLen = (int)ReadVarint(data, pos, len);
            SaveInventoryItem item;
            item.ParseFromArray(data + pos, innerLen);
            inventoryItems.push_back(item);
            pos += innerLen;
            break;
        }
        case 17: respawnCount = ReadInt32(data, pos, len); break;
        case 18: totalKills = ReadInt32(data, pos, len); break;
        case 19: leftBullet = ReadInt32(data, pos, len); break;
        case 20: rightBullet = ReadInt32(data, pos, len); break;
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── SaveEnemyData ───

bool SaveEnemyData::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: enemyId = ReadInt32(data, pos, len); break;
        case 2: posX = ReadFloat(data, pos); break;
        case 3: posY = ReadFloat(data, pos); break;
        case 4: posZ = ReadFloat(data, pos); break;
        case 5: hp = ReadFloat(data, pos); break;
        case 6: state = ReadInt32(data, pos, len); break;
        case 7: isDead = ReadBool(data, pos, len); break;
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── SaveChestData ───

bool SaveChestData::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: chestId = ReadInt32(data, pos, len); break;
        case 2: {
            int innerLen = (int)ReadVarint(data, pos, len);
            SaveInventoryItem item;
            item.ParseFromArray(data + pos, innerLen);
            items.push_back(item);
            pos += innerLen;
            break;
        }
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── WorldSaveData ───

bool WorldSaveData::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: roomId = ReadInt32(data, pos, len); break;
        case 3: aiPosX = ReadFloat(data, pos); break;
        case 4: aiPosY = ReadFloat(data, pos); break;
        case 5: aiPosZ = ReadFloat(data, pos); break;
        case 6: aiHp = ReadFloat(data, pos); break;
        case 7: aiState = ReadInt32(data, pos, len); break;
        case 8: {
            int innerLen = (int)ReadVarint(data, pos, len);
            SaveChestData chest;
            chest.ParseFromArray(data + pos, innerLen);
            chests.push_back(chest);
            pos += innerLen;
            break;
        }
        case 9: {
            int innerLen = (int)ReadVarint(data, pos, len);
            SaveEnemyData enemy;
            enemy.ParseFromArray(data + pos, innerLen);
            enemies.push_back(enemy);
            pos += innerLen;
            break;
        }
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── PlayerSaveSubmit ───

bool PlayerSaveSubmit::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: playerId = ReadInt32(data, pos, len); break;
        case 2: roomId = ReadInt32(data, pos, len); break;
        case 3: {
            int innerLen = (int)ReadVarint(data, pos, len);
            saveData.ParseFromArray(data + pos, innerLen);
            pos += innerLen;
            break;
        }
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── WorldSaveSubmit ───

bool WorldSaveSubmit::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: playerId = ReadInt32(data, pos, len); break;
        case 2: roomId = ReadInt32(data, pos, len); break;
        case 3: {
            int innerLen = (int)ReadVarint(data, pos, len);
            saveData.ParseFromArray(data + pos, innerLen);
            pos += innerLen;
            break;
        }
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── DeleteRoomRequest ───

bool DeleteRoomRequest::ParseFromArray(const uint8_t* data, int len) {
    int pos = 0;
    while (pos < len) {
        uint32_t tag = ReadVarint(data, pos, len);
        int fieldNum = tag >> 3;
        int wireType = tag & 0x7;

        switch (fieldNum) {
        case 1: playerId = ReadInt32(data, pos, len); break;
        case 2: roomId = ReadInt32(data, pos, len); break;
        default: SkipField(wireType, pos, len, data); break;
        }
    }
    return true;
}

// ─── Serialization helpers ───

struct ByteBuffer {
    std::vector<uint8_t> buf;

    void WriteVarint(uint64_t v) {
        while (v > 0x7F) { buf.push_back((uint8_t)((v & 0x7F) | 0x80)); v >>= 7; }
        buf.push_back((uint8_t)v);
    }
    void WriteTag(int fieldNum, int wireType) { WriteVarint((fieldNum << 3) | wireType); }
    void WriteInt32(int fieldNum, int32_t v) { WriteTag(fieldNum, 0); WriteVarint((uint32_t)v); }
    void WriteFloat(int fieldNum, float v) {
        WriteTag(fieldNum, 5);
        uint32_t raw; memcpy(&raw, &v, 4);
        buf.push_back((uint8_t)(raw));
        buf.push_back((uint8_t)(raw >> 8));
        buf.push_back((uint8_t)(raw >> 16));
        buf.push_back((uint8_t)(raw >> 24));
    }
    void WriteString(int fieldNum, const std::string& s) {
        WriteTag(fieldNum, 2);
        WriteVarint(s.size());
        buf.insert(buf.end(), s.begin(), s.end());
    }
    void WriteBool(int fieldNum, bool v) { WriteTag(fieldNum, 0); buf.push_back(v ? 1 : 0); }
    void WriteBytes(int fieldNum, const uint8_t* data, size_t len) {
        WriteTag(fieldNum, 2);
        WriteVarint(len);
        buf.insert(buf.end(), data, data + len);
    }
};

static void SerializeInventoryItem(ByteBuffer& bb, const savemsg::SaveInventoryItem& item) {
    ByteBuffer inner;
    inner.WriteInt32(1, item.itemId);
    inner.WriteInt32(2, item.amount);
    inner.WriteInt32(3, item.x);
    inner.WriteInt32(4, item.y);
    inner.WriteString(5, item.gridType);
    inner.WriteInt32(6, item.gridIndex);
    inner.WriteBool(7, item.rotated);
    bb.WriteBytes(1, inner.buf.data(), inner.buf.size()); // placeholder field num, caller handles
}

void SerializeInventoryItemRaw(ByteBuffer& bb, const savemsg::SaveInventoryItem& item) {
    bb.WriteInt32(1, item.itemId);
    bb.WriteInt32(2, item.amount);
    bb.WriteInt32(3, item.x);
    bb.WriteInt32(4, item.y);
    bb.WriteString(5, item.gridType);
    bb.WriteInt32(6, item.gridIndex);
    bb.WriteBool(7, item.rotated);
}

// ─── PlayerSaveData serialization ───

std::vector<uint8_t> savemsg::PlayerSaveData::SerializeToBytes() const {
    ByteBuffer bb;
    bb.WriteInt32(1, playerId);
    bb.WriteFloat(2, posX); bb.WriteFloat(3, posY); bb.WriteFloat(4, posZ);
    bb.WriteFloat(5, hp); bb.WriteFloat(6, maxHp);
    bb.WriteFloat(7, food); bb.WriteFloat(8, water);
    bb.WriteInt32(9, level); bb.WriteInt32(10, gold);
    bb.WriteInt32(11, equippedHeadId); bb.WriteInt32(12, equippedBodyId);
    bb.WriteInt32(13, equippedWeapon1Id); bb.WriteInt32(14, equippedWeapon2Id); bb.WriteInt32(15, equippedWeapon3Id);
    for (auto& item : inventoryItems) {
        ByteBuffer itemBb;
        itemBb.WriteInt32(1, item.itemId);
        itemBb.WriteInt32(2, item.amount);
        itemBb.WriteInt32(3, item.x);
        itemBb.WriteInt32(4, item.y);
        itemBb.WriteString(5, item.gridType);
        itemBb.WriteInt32(6, item.gridIndex);
        itemBb.WriteBool(7, item.rotated);
        bb.WriteBytes(16, itemBb.buf.data(), itemBb.buf.size());
    }
    bb.WriteInt32(17, respawnCount);
    bb.WriteInt32(18, totalKills);
    bb.WriteInt32(19, leftBullet);
    bb.WriteInt32(20, rightBullet);
    return bb.buf;
}

bool savemsg::PlayerSaveData::ParseFromBytes(const std::vector<uint8_t>& data) {
    return ParseFromArray(data.data(), (int)data.size());
}

// ─── WorldSaveData serialization ───

std::vector<uint8_t> savemsg::WorldSaveData::SerializeToBytes() const {
    ByteBuffer bb;
    bb.WriteInt32(1, roomId);
    bb.WriteFloat(3, aiPosX); bb.WriteFloat(4, aiPosY); bb.WriteFloat(5, aiPosZ);
    bb.WriteFloat(6, aiHp);
    bb.WriteInt32(7, aiState);
    for (auto& chest : chests) {
        ByteBuffer chestBb;
        chestBb.WriteInt32(1, chest.chestId);
        for (auto& item : chest.items) {
            ByteBuffer itemBb;
            itemBb.WriteInt32(1, item.itemId);
            itemBb.WriteInt32(2, item.amount);
            itemBb.WriteInt32(3, item.x);
            itemBb.WriteInt32(4, item.y);
            itemBb.WriteString(5, item.gridType);
            itemBb.WriteInt32(6, item.gridIndex);
            itemBb.WriteBool(7, item.rotated);
            chestBb.WriteBytes(2, itemBb.buf.data(), itemBb.buf.size());
        }
        bb.WriteBytes(8, chestBb.buf.data(), chestBb.buf.size());
    }
    for (auto& enemy : enemies) {
        auto enemyBytes = enemy.SerializeToBytes();
        bb.WriteBytes(9, enemyBytes.data(), enemyBytes.size());
    }
    return bb.buf;
}

std::vector<uint8_t> savemsg::SaveEnemyData::SerializeToBytes() const {
    ByteBuffer bb;
    bb.WriteInt32(1, enemyId);
    bb.WriteFloat(2, posX); bb.WriteFloat(3, posY); bb.WriteFloat(4, posZ);
    bb.WriteFloat(5, hp);
    bb.WriteInt32(6, state);
    bb.WriteBool(7, isDead);
    return bb.buf;
}

std::vector<uint8_t> savemsg::SaveChestData::SerializeToBytes() const {
    ByteBuffer bb;
    bb.WriteInt32(1, chestId);
    for (auto& item : items) {
        ByteBuffer itemBb;
        itemBb.WriteInt32(1, item.itemId);
        itemBb.WriteInt32(2, item.amount);
        itemBb.WriteInt32(3, item.x);
        itemBb.WriteInt32(4, item.y);
        itemBb.WriteString(5, item.gridType);
        itemBb.WriteInt32(6, item.gridIndex);
        itemBb.WriteBool(7, item.rotated);
        bb.WriteBytes(2, itemBb.buf.data(), itemBb.buf.size());
    }
    return bb.buf;
}

bool savemsg::WorldSaveData::ParseFromBytes(const std::vector<uint8_t>& data) {
    return ParseFromArray(data.data(), (int)data.size());
}

} // namespace savemsg
