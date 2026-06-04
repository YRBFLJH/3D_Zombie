using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using UnityEngine;

/// <summary>
/// 存档消息的原始字节级编解码器。
/// 在 GameMessage 解析前拦截存档消息，避免修改生成的 NetworkMessage.cs。
/// 字段标签：33=PlayerSaveSubmit 34=PlayerSaveAck 35=WorldSaveSubmit 36=WorldSaveAck 37=DeleteRoomRequest 38=DeleteRoomResponse
/// </summary>

public enum SaveMessageType
{
    None = 0,
    PlayerSaveSubmit = 33,
    PlayerSaveAck = 34,
    WorldSaveSubmit = 35,
    WorldSaveAck = 36,
    DeleteRoomRequest = 37,
    DeleteRoomResponse = 38,
    ClearAllSaves = 42,
    PlayerLoadData = 45,
}

// ===== 数据模型 =====

[Serializable]
public class SavePlayerData
{
    public int playerId;
    public float posX, posY, posZ;
    public float hp, maxHp;
    public float food, water;
    public int level, gold;
    public int equippedHeadId, equippedBodyId, equippedWeapon1Id, equippedWeapon2Id, equippedWeapon3Id;
    public List<SaveInventoryItem> inventoryItems = new List<SaveInventoryItem>();
    public int respawnCount;
    public int totalKills;
    public int leftBullet;   // current magazine ammo
    public int rightBullet;  // reserve ammo
}

[Serializable]
public class SaveInventoryItem
{
    public int itemId;
    public int amount;
    public int x, y;
    public string gridType;
    public int gridIndex;
    public bool rotated;
}

[Serializable]
public class SaveWorldData
{
    public int roomId;
    public float aiPosX, aiPosY, aiPosZ;
    public float aiHp;
    public int aiState;
    public List<SaveChestData> chests = new List<SaveChestData>();
    public List<SaveEnemyData> enemies = new List<SaveEnemyData>();
}

[Serializable]
public class SaveEnemyData
{
    public int enemyId;
    public float posX, posY, posZ;
    public float hp;
    public int state;
    public bool isDead;
}

[Serializable]
public class SaveChestData
{
    public int chestId;
    public List<SaveInventoryItem> items = new List<SaveInventoryItem>();
}

[Serializable]
public class PlayerSaveSubmitMsg
{
    public int playerId;
    public int roomId;
    public SavePlayerData saveData;
}

[Serializable]
public class PlayerSaveAckMsg
{
    public bool success;
}

[Serializable]
public class WorldSaveSubmitMsg
{
    public int playerId;
    public int roomId;
    public SaveWorldData saveData;
}

[Serializable]
public class WorldSaveAckMsg
{
    public bool success;
}

[Serializable]
public class DeleteRoomRequestMsg
{
    public int playerId;
    public int roomId;
}

[Serializable]
public class DeleteRoomResponseMsg
{
    public bool success;
}

// ===== 编解码器 =====

public static class SaveWireCodec
{
    #region 探测（从原始缓冲区检测存档消息类型）
    public static bool TryDetectSaveMessage(byte[] buffer, out SaveMessageType type, out byte[] innerBytes)
    {
        type = SaveMessageType.None;
        innerBytes = null;
        if (buffer == null || buffer.Length < 2) return false;

        try
        {
            using (var stream = new MemoryStream(buffer))
            using (var input = new CodedInputStream(stream))
            {
                uint tag = input.ReadTag();
                int fieldNumber = (int)(tag >> 3);
                switch (fieldNumber)
                {
                    case 33: type = SaveMessageType.PlayerSaveSubmit; break;
                    case 34: type = SaveMessageType.PlayerSaveAck; break;
                    case 35: type = SaveMessageType.WorldSaveSubmit; break;
                    case 36: type = SaveMessageType.WorldSaveAck; break;
                    case 37: type = SaveMessageType.DeleteRoomRequest; break;
                    case 38: type = SaveMessageType.DeleteRoomResponse; break;
                    case 45: type = SaveMessageType.PlayerLoadData; break;
                    default: return false;
                }
                innerBytes = input.ReadBytes().ToByteArray();
                return true;
            }
        }
        catch { return false; }
    }
    #endregion

    #region 构建方法
    // Wire tag helpers — WriteRawTag takes byte(s), not uint
    static void WriteTag(CodedOutputStream output, int fieldNum, int wireType)
    {
        uint tag = (uint)((fieldNum << 3) | wireType);
        if (tag < 0x80)
            output.WriteRawTag((byte)tag);
        else // 2-byte varint for fields 16+
            output.WriteRawTag((byte)((tag & 0x7F) | 0x80), (byte)(tag >> 7));
    }

    public static byte[] BuildPlayerSaveSubmit(int playerId, int roomId, SavePlayerData data)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            byte[] inner = SerializePlayerSaveSubmitInner(playerId, roomId, data);
            WriteTag(output, 33, 2); // (33<<3)|2
            output.WriteBytes(ByteString.CopyFrom(inner));
            output.Flush();
            return ms.ToArray();
        }
    }

    public static byte[] BuildPlayerSaveAck(bool success) => BuildSimpleAck(34, success);
    public static byte[] BuildWorldSaveAck(bool success) => BuildSimpleAck(36, success);
    public static byte[] BuildDeleteRoomResponse(bool success) => BuildSimpleAck(38, success);

    static byte[] BuildSimpleAck(int fieldNum, bool success)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            // Inner: field 1 bool = [tag: 0x08] [value]
            byte[] inner = new byte[] { 0x08, (byte)(success ? 0x01 : 0x00) };
            WriteTag(output, fieldNum, 2);
            output.WriteBytes(ByteString.CopyFrom(inner));
            output.Flush();
            return ms.ToArray();
        }
    }

    public static byte[] BuildWorldSaveSubmit(int playerId, int roomId, SaveWorldData data)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            byte[] inner = SerializeWorldSaveSubmitInner(playerId, roomId, data);
            WriteTag(output, 35, 2);
            output.WriteBytes(ByteString.CopyFrom(inner));
            output.Flush();
            return ms.ToArray();
        }
    }

    public static byte[] BuildClearAllSaves()
    {
        // Empty message: just outer tag (42<<3)|2 = 338 = 0xD2 0x02 + length 0
        return new byte[] { 0xD2, 0x02, 0x00 };
    }

    public static byte[] BuildDeleteRoomRequest(int playerId, int roomId)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            byte[] inner = SerializeDeleteRoomRequestInner(playerId, roomId);
            WriteTag(output, 37, 2);
            output.WriteBytes(ByteString.CopyFrom(inner));
            output.Flush();
            return ms.ToArray();
        }
    }
    #endregion

    #region 内部序列化
    static byte[] SerializePlayerSaveSubmitInner(int playerId, int roomId, SavePlayerData data)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(playerId);
            WriteTag(output, 2, 0); output.WriteInt32(roomId);
            byte[] playerBytes = SerializePlayerData(data);
            WriteTag(output, 3, 2); output.WriteBytes(ByteString.CopyFrom(playerBytes));
            output.Flush();
            return ms.ToArray();
        }
    }

    static byte[] SerializeWorldSaveSubmitInner(int playerId, int roomId, SaveWorldData data)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(playerId);
            WriteTag(output, 2, 0); output.WriteInt32(roomId);
            byte[] worldBytes = SerializeWorldData(data);
            WriteTag(output, 3, 2); output.WriteBytes(ByteString.CopyFrom(worldBytes));
            output.Flush();
            return ms.ToArray();
        }
    }

    static byte[] SerializeDeleteRoomRequestInner(int playerId, int roomId)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(playerId);
            WriteTag(output, 2, 0); output.WriteInt32(roomId);
            output.Flush();
            return ms.ToArray();
        }
    }

    static byte[] SerializePlayerData(SavePlayerData data)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(data.playerId);
            WriteTag(output, 2, 5); output.WriteFloat(data.posX);
            WriteTag(output, 3, 5); output.WriteFloat(data.posY);
            WriteTag(output, 4, 5); output.WriteFloat(data.posZ);
            WriteTag(output, 5, 5); output.WriteFloat(data.hp);
            WriteTag(output, 6, 5); output.WriteFloat(data.maxHp);
            WriteTag(output, 7, 5); output.WriteFloat(data.food);
            WriteTag(output, 8, 5); output.WriteFloat(data.water);
            WriteTag(output, 9, 0); output.WriteInt32(data.level);
            WriteTag(output, 10, 0); output.WriteInt32(data.gold);
            WriteTag(output, 11, 0); output.WriteInt32(data.equippedHeadId);
            WriteTag(output, 12, 0); output.WriteInt32(data.equippedBodyId);
            WriteTag(output, 13, 0); output.WriteInt32(data.equippedWeapon1Id);
            WriteTag(output, 14, 0); output.WriteInt32(data.equippedWeapon2Id);
            WriteTag(output, 15, 0); output.WriteInt32(data.equippedWeapon3Id);
            foreach (var item in data.inventoryItems)
            {
                byte[] itemBytes = SerializeInventoryItem(item);
                WriteTag(output, 16, 2); output.WriteBytes(ByteString.CopyFrom(itemBytes));
            }
            WriteTag(output, 17, 0); output.WriteInt32(data.respawnCount);
            WriteTag(output, 18, 0); output.WriteInt32(data.totalKills);
            WriteTag(output, 19, 0); output.WriteInt32(data.leftBullet);
            WriteTag(output, 20, 0); output.WriteInt32(data.rightBullet);
            output.Flush();
            return ms.ToArray();
        }
    }

    static byte[] SerializeEnemyData(SaveEnemyData data)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(data.enemyId);
            WriteTag(output, 2, 5); output.WriteFloat(data.posX);
            WriteTag(output, 3, 5); output.WriteFloat(data.posY);
            WriteTag(output, 4, 5); output.WriteFloat(data.posZ);
            WriteTag(output, 5, 5); output.WriteFloat(data.hp);
            WriteTag(output, 6, 0); output.WriteInt32(data.state);
            WriteTag(output, 7, 0); output.WriteBool(data.isDead);
            output.Flush();
            return ms.ToArray();
        }
    }

    static byte[] SerializeInventoryItem(SaveInventoryItem item)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(item.itemId);
            WriteTag(output, 2, 0); output.WriteInt32(item.amount);
            WriteTag(output, 3, 0); output.WriteInt32(item.x);
            WriteTag(output, 4, 0); output.WriteInt32(item.y);
            WriteTag(output, 5, 2); output.WriteString(item.gridType ?? "");
            WriteTag(output, 6, 0); output.WriteInt32(item.gridIndex);
            WriteTag(output, 7, 0); output.WriteBool(item.rotated);
            output.Flush();
            return ms.ToArray();
        }
    }

    static byte[] SerializeWorldData(SaveWorldData data)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(data.roomId);
            WriteTag(output, 3, 5); output.WriteFloat(data.aiPosX);
            WriteTag(output, 4, 5); output.WriteFloat(data.aiPosY);
            WriteTag(output, 5, 5); output.WriteFloat(data.aiPosZ);
            WriteTag(output, 6, 5); output.WriteFloat(data.aiHp);
            WriteTag(output, 7, 0); output.WriteInt32(data.aiState);
            foreach (var enemy in data.enemies)
            {
                byte[] enemyBytes = SerializeEnemyData(enemy);
                WriteTag(output, 9, 2); output.WriteBytes(ByteString.CopyFrom(enemyBytes));
            }
            foreach (var chest in data.chests)
            {
                byte[] chestBytes = SerializeChestData(chest);
                WriteTag(output, 8, 2); output.WriteBytes(ByteString.CopyFrom(chestBytes));
            }
            output.Flush();
            return ms.ToArray();
        }
    }

    static byte[] SerializeChestData(SaveChestData chest)
    {
        using (var ms = new MemoryStream())
        using (var output = new CodedOutputStream(ms))
        {
            WriteTag(output, 1, 0); output.WriteInt32(chest.chestId);
            foreach (var item in chest.items)
            {
                byte[] itemBytes = SerializeInventoryItem(item);
                WriteTag(output, 2, 2); output.WriteBytes(ByteString.CopyFrom(itemBytes));
            }
            output.Flush();
            return ms.ToArray();
        }
    }
    #endregion

    #region 解析方法
    public static PlayerSaveSubmitMsg ParsePlayerSaveSubmit(byte[] data) => ParsePlayerSaveSubmitInner(data);
    public static PlayerSaveAckMsg ParsePlayerSaveAck(byte[] data) { var m = new PlayerSaveAckMsg(); ParseSimpleBool(data, v => m.success = v); return m; }
    public static WorldSaveSubmitMsg ParseWorldSaveSubmit(byte[] data) => ParseWorldSaveSubmitInner(data);
    public static WorldSaveAckMsg ParseWorldSaveAck(byte[] data) { var m = new WorldSaveAckMsg(); ParseSimpleBool(data, v => m.success = v); return m; }
    public static DeleteRoomRequestMsg ParseDeleteRoomRequest(byte[] data) => ParseDeleteRoomRequestInner(data);
    public static DeleteRoomResponseMsg ParseDeleteRoomResponse(byte[] data) { var m = new DeleteRoomResponseMsg(); ParseSimpleBool(data, v => m.success = v); return m; }

    static void ParseSimpleBool(byte[] data, Action<bool> setter)
    {
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                if ((tag >> 3) == 1) setter(input.ReadBool());
                else input.SkipLastField();
            }
        }
    }

    static PlayerSaveSubmitMsg ParsePlayerSaveSubmitInner(byte[] data)
    {
        var msg = new PlayerSaveSubmitMsg();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: msg.playerId = input.ReadInt32(); break;
                    case 2: msg.roomId = input.ReadInt32(); break;
                    case 3: msg.saveData = ParsePlayerData(input.ReadBytes().ToByteArray()); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return msg;
    }

    static WorldSaveSubmitMsg ParseWorldSaveSubmitInner(byte[] data)
    {
        var msg = new WorldSaveSubmitMsg();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: msg.playerId = input.ReadInt32(); break;
                    case 2: msg.roomId = input.ReadInt32(); break;
                    case 3: msg.saveData = ParseWorldData(input.ReadBytes().ToByteArray()); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return msg;
    }

    static DeleteRoomRequestMsg ParseDeleteRoomRequestInner(byte[] data)
    {
        var msg = new DeleteRoomRequestMsg();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: msg.playerId = input.ReadInt32(); break;
                    case 2: msg.roomId = input.ReadInt32(); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return msg;
    }

    public static SavePlayerData ParsePlayerData(byte[] data)
    {
        var r = new SavePlayerData();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: r.playerId = input.ReadInt32(); break;
                    case 2: r.posX = input.ReadFloat(); break;
                    case 3: r.posY = input.ReadFloat(); break;
                    case 4: r.posZ = input.ReadFloat(); break;
                    case 5: r.hp = input.ReadFloat(); break;
                    case 6: r.maxHp = input.ReadFloat(); break;
                    case 7: r.food = input.ReadFloat(); break;
                    case 8: r.water = input.ReadFloat(); break;
                    case 9: r.level = input.ReadInt32(); break;
                    case 10: r.gold = input.ReadInt32(); break;
                    case 11: r.equippedHeadId = input.ReadInt32(); break;
                    case 12: r.equippedBodyId = input.ReadInt32(); break;
                    case 13: r.equippedWeapon1Id = input.ReadInt32(); break;
                    case 14: r.equippedWeapon2Id = input.ReadInt32(); break;
                    case 15: r.equippedWeapon3Id = input.ReadInt32(); break;
                    case 16: r.inventoryItems.Add(ParseInventoryItem(input.ReadBytes().ToByteArray())); break;
                    case 17: r.respawnCount = input.ReadInt32(); break;
                    case 18: r.totalKills = input.ReadInt32(); break;
                    case 19: r.leftBullet = input.ReadInt32(); break;
                    case 20: r.rightBullet = input.ReadInt32(); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return r;
    }

    static SaveInventoryItem ParseInventoryItem(byte[] data)
    {
        var item = new SaveInventoryItem();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: item.itemId = input.ReadInt32(); break;
                    case 2: item.amount = input.ReadInt32(); break;
                    case 3: item.x = input.ReadInt32(); break;
                    case 4: item.y = input.ReadInt32(); break;
                    case 5: item.gridType = input.ReadString(); break;
                    case 6: item.gridIndex = input.ReadInt32(); break;
                    case 7: item.rotated = input.ReadBool(); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return item;
    }

    static SaveWorldData ParseWorldData(byte[] data)
    {
        var r = new SaveWorldData();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: r.roomId = input.ReadInt32(); break;
                    case 3: r.aiPosX = input.ReadFloat(); break;
                    case 4: r.aiPosY = input.ReadFloat(); break;
                    case 5: r.aiPosZ = input.ReadFloat(); break;
                    case 6: r.aiHp = input.ReadFloat(); break;
                    case 7: r.aiState = input.ReadInt32(); break;
                    case 8: r.chests.Add(ParseChestData(input.ReadBytes().ToByteArray())); break;
                    case 9: r.enemies.Add(ParseEnemyData(input.ReadBytes().ToByteArray())); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return r;
    }

    static SaveChestData ParseChestData(byte[] data)
    {
        var chest = new SaveChestData();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: chest.chestId = input.ReadInt32(); break;
                    case 2: chest.items.Add(ParseInventoryItem(input.ReadBytes().ToByteArray())); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return chest;
    }

    static SaveEnemyData ParseEnemyData(byte[] data)
    {
        var enemy = new SaveEnemyData();
        using (var stream = new MemoryStream(data))
        using (var input = new CodedInputStream(stream))
        {
            while (!input.IsAtEnd)
            {
                uint tag = input.ReadTag();
                switch ((int)(tag >> 3))
                {
                    case 1: enemy.enemyId = input.ReadInt32(); break;
                    case 2: enemy.posX = input.ReadFloat(); break;
                    case 3: enemy.posY = input.ReadFloat(); break;
                    case 4: enemy.posZ = input.ReadFloat(); break;
                    case 5: enemy.hp = input.ReadFloat(); break;
                    case 6: enemy.state = input.ReadInt32(); break;
                    case 7: enemy.isDead = input.ReadBool(); break;
                    default: input.SkipLastField(); break;
                }
            }
        }
        return enemy;
    }
    #endregion
}
