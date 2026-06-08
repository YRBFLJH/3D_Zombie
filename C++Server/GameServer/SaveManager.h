#pragma once
#include <string>
#include <vector>

class SaveManager {
public:
    struct RoomMeta {
        int roomId = 0;
        std::string roomName;
        int hostPlayerId = 0;
        std::string hostIP;
        std::string hostAccount;
    };

    static bool SaveRoomMeta(const RoomMeta& meta);
    static bool LoadRoomMeta(int roomId, RoomMeta& outMeta);
    static std::vector<RoomMeta> GetAllRoomMetas();
    static bool DeleteRoom(int roomId);
    static void DeleteAllRooms();

    // Path helpers (public for use by LobbyManager save handlers)
    static std::string GetWorldPath(int roomId);
    static std::string GetPlayerPath(int roomId, int playerId);
    static std::string GetPlayerPathByAccount(int roomId, const std::string& account);
    static std::string GetPlayerDir(int roomId);
    static std::string GetRoomDir(int roomId);
    static bool EnsureDir(const std::string& path);

private:
    static std::string GetBaseDir();
    static std::string GetMetaPath(int roomId);
};
