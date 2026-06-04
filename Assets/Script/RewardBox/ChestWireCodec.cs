using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Google.Protobuf;

/// <summary>
/// 与 NetworkMessage.proto 中 chest 字段（12/13/14）一致的线格式编解码。
/// 读路径使用 byte[] 手动解析，兼容无 ReadRawByte / SkipRawBytes 的旧版 Google.Protobuf。
/// </summary>
public static class ChestWireCodec
{
    const uint TagChestStateSync = (14u << 3) | 2; // 114

    public static bool TryReadChestStateSyncFromGameMessageBuffer(byte[] buffer, out ChestStateSyncPayload payload)
    {
        payload = new ChestStateSyncPayload { Items = new List<InventoryItem>() };
        if (buffer == null || buffer.Length == 0) return false;

        int pos = 0;
        while (pos < buffer.Length)
        {
            uint tag = ReadRawVarint32(buffer, ref pos);
            if (tag == 0) break;
            if (tag == TagChestStateSync)
            {
                int len = (int)ReadRawVarint32(buffer, ref pos);
                if (len < 0) return false;
                int rem = buffer.Length - pos;
                if (len > rem) len = rem;
                byte[] inner = SubArray(buffer, pos, len);
                pos += len;
                payload = ParseChestStateSync(inner);
                return true;
            }
            SkipFieldManual(buffer, ref pos, tag);
        }
        return false;
    }

    public static byte[] BuildGameMessageWithChestStateRequest(int playerId, int chestId)
    {
        byte[] inner = EncodeChestStateRequest(playerId, chestId);
        using var ms = new MemoryStream();
        WriteLengthDelimitedOuter(ms, (12u << 3) | 2, inner);
        return ms.ToArray();
    }

    public static byte[] BuildGameMessageWithChestStateSubmit(int playerId, int chestId, List<InventoryItem> items)
    {
        byte[] inner = EncodeChestStateSubmit(playerId, chestId, items);
        using var ms = new MemoryStream();
        WriteLengthDelimitedOuter(ms, (13u << 3) | 2, inner);
        return ms.ToArray();
    }

    static void WriteLengthDelimitedOuter(MemoryStream ms, uint fieldTag, byte[] payload)
    {
        var cos = new CodedOutputStream(ms);
        cos.WriteTag(fieldTag);
        cos.WriteLength(payload.Length);
        cos.Flush();
        ms.Write(payload, 0, payload.Length);
    }

    static byte[] EncodeChestStateRequest(int playerId, int chestId)
    {
        using var ms = new MemoryStream();
        var cos = new CodedOutputStream(ms);
        cos.WriteTag(8);
        cos.WriteInt32(playerId);
        cos.WriteTag(16);
        cos.WriteInt32(chestId);
        cos.Flush();
        return ms.ToArray();
    }

    static byte[] EncodeChestStateSubmit(int playerId, int chestId, List<InventoryItem> items)
    {
        using var ms = new MemoryStream();
        var cos = new CodedOutputStream(ms);
        cos.WriteTag(8);
        cos.WriteInt32(playerId);
        cos.WriteTag(16);
        cos.WriteInt32(chestId);
        if (items != null)
        {
            foreach (var it in items)
            {
                cos.WriteTag(26);
                byte[] one = EncodeChestItemStateBytes(it);
                cos.WriteLength(one.Length);
                cos.Flush();
                ms.Write(one, 0, one.Length);
            }
        }
        cos.Flush();
        return ms.ToArray();
    }

    static byte[] EncodeChestItemStateBytes(InventoryItem it)
    {
        using var ms = new MemoryStream();
        var cos = new CodedOutputStream(ms);
        WriteChestItemStateFields(cos, it);
        cos.Flush();
        return ms.ToArray();
    }

    static void WriteChestItemStateFields(CodedOutputStream o, InventoryItem it)
    {
        o.WriteTag(8);
        o.WriteInt32(it.itemId);
        o.WriteTag(16);
        o.WriteInt32(it.amount);
        o.WriteTag(24);
        o.WriteInt32(it.x);
        o.WriteTag(32);
        o.WriteInt32(it.y);
        o.WriteTag(42);
        o.WriteString(it.gridType ?? "Large");
        o.WriteTag(48);
        o.WriteInt32(it.gridIndex);
        o.WriteTag(56);
        o.WriteBool(it.isRotated);
    }

    static ChestStateSyncPayload ParseChestStateSync(byte[] buf)
    {
        var p = new ChestStateSyncPayload { Items = new List<InventoryItem>() };
        int pos = 0;
        while (pos < buf.Length)
        {
            uint tag = ReadRawVarint32(buf, ref pos);
            if (tag == 0) break;
            switch (tag)
            {
                case 8:
                    p.ChestId = (int)ReadRawVarint64(buf, ref pos);
                    break;
                case 18:
                    {
                        int len = (int)ReadRawVarint32(buf, ref pos);
                        int rem = buf.Length - pos;
                        if (len > rem) len = rem;
                        byte[] sub = SubArray(buf, pos, len);
                        pos += len;
                        p.Items.Add(ParseChestItemState(sub));
                        break;
                    }
                case 24:
                    p.FromPlayerId = (int)ReadRawVarint64(buf, ref pos);
                    break;
                default:
                    SkipFieldManual(buf, ref pos, tag);
                    break;
            }
        }
        return p;
    }

    static InventoryItem ParseChestItemState(byte[] buf)
    {
        int itemId = 0, amount = 0, x = 0, y = 0, gridIndex = 0;
        string gridType = "Large";
        bool rotated = false;
        int pos = 0;
        while (pos < buf.Length)
        {
            uint tag = ReadRawVarint32(buf, ref pos);
            if (tag == 0) break;
            switch (tag)
            {
                case 8: itemId = (int)ReadRawVarint64(buf, ref pos); break;
                case 16: amount = (int)ReadRawVarint64(buf, ref pos); break;
                case 24: x = (int)ReadRawVarint64(buf, ref pos); break;
                case 32: y = (int)ReadRawVarint64(buf, ref pos); break;
                case 42:
                    {
                        int slen = (int)ReadRawVarint32(buf, ref pos);
                        int rem = buf.Length - pos;
                        if (slen > rem) slen = rem;
                        gridType = Encoding.UTF8.GetString(buf, pos, slen);
                        pos += slen;
                        break;
                    }
                case 48: gridIndex = (int)ReadRawVarint64(buf, ref pos); break;
                case 56: rotated = ReadRawVarint64(buf, ref pos) != 0; break;
                default: SkipFieldManual(buf, ref pos, tag); break;
            }
        }
        return new InventoryItem(itemId, amount, x, y, gridType, gridIndex, rotated);
    }

    static byte[] SubArray(byte[] src, int offset, int len)
    {
        if (len <= 0) return Array.Empty<byte>();
        byte[] d = new byte[len];
        Buffer.BlockCopy(src, offset, d, 0, len);
        return d;
    }

    static uint ReadRawVarint32(byte[] buffer, ref int pos)
    {
        return (uint)ReadRawVarint64(buffer, ref pos);
    }

    static ulong ReadRawVarint64(byte[] buffer, ref int pos)
    {
        ulong result = 0;
        int shift = 0;
        while (shift < 64 && pos < buffer.Length)
        {
            byte b = buffer[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
        }
        return result;
    }

    static void SkipFieldManual(byte[] buffer, ref int pos, uint tag)
    {
        switch (tag & 7)
        {
            case 0:
                ReadRawVarint64(buffer, ref pos);
                break;
            case 1:
                pos += 8;
                break;
            case 2:
                {
                    int len = (int)ReadRawVarint32(buffer, ref pos);
                    if (len < 0) return;
                    pos += len;
                    break;
                }
            case 5:
                pos += 4;
                break;
            default:
                break;
        }
    }
}

public struct ChestStateSyncPayload
{
    public int ChestId;
    public int FromPlayerId;
    public List<InventoryItem> Items;
}
