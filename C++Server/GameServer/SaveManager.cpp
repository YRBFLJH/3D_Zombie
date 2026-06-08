#include "SaveManager.h"
#include <fstream>
#include <iostream>

#ifdef _WIN32
#include <direct.h>  // _mkdir, _rmdir
#define MKDIR(path) _mkdir(path)
#define RMDIR(path) _rmdir(path)
#else
#include <sys/stat.h>
#define MKDIR(path) mkdir(path, 0755)
#define RMDIR(path) rmdir(path)
#endif

static const char* SAVE_DIR = "saves";

// ─── Path helpers ───

std::string SaveManager::GetBaseDir() {
    return SAVE_DIR;
}

std::string SaveManager::GetRoomDir(int roomId) {
    return GetBaseDir() + "/rooms/" + std::to_string(roomId);
}

std::string SaveManager::GetWorldPath(int roomId) {
    return GetRoomDir(roomId) + "/world.dat";
}

std::string SaveManager::GetPlayerPath(int roomId, int playerId) {
    return GetRoomDir(roomId) + "/players/" + std::to_string(playerId) + ".dat";
}

std::string SaveManager::GetPlayerPathByAccount(int roomId, const std::string& account) {
    return GetRoomDir(roomId) + "/players/" + account + ".dat";
}

std::string SaveManager::GetPlayerDir(int roomId) {
    return GetRoomDir(roomId) + "/players";
}

std::string SaveManager::GetMetaPath(int roomId) {
    return GetRoomDir(roomId) + "/room_meta.dat";
}

bool SaveManager::EnsureDir(const std::string& path) {
    std::string p = path;
#ifdef _WIN32
    for (auto& c : p) if (c == '/') c = '\\';
#endif
    size_t pos = 0;
    while ((pos = p.find_first_of("\\", pos + 1)) != std::string::npos) {
        std::string sub = p.substr(0, pos);
        MKDIR(sub.c_str());
    }
    MKDIR(p.c_str());
    return true;
}

// ─── Room metadata ───

bool SaveManager::SaveRoomMeta(const RoomMeta& meta) {
    std::string dir = GetRoomDir(meta.roomId);
    EnsureDir(dir);

    std::string path = GetMetaPath(meta.roomId);
    std::ofstream ofs(path, std::ios::binary | std::ios::trunc);
    if (!ofs.is_open()) {
        std::cerr << "[Save] Failed to open: " << path << std::endl;
        return false;
    }

    // Simple protobuf serialization: write roomId, roomName, hostPlayerId as a simple binary format
    // RoomMeta fields: 1=int roomId, 2=string name, 3=int hostId
    int nameLen = (int)meta.roomName.size();
    int ipLen = (int)meta.hostIP.size();
    int accountLen = (int)meta.hostAccount.size();
    ofs.write(reinterpret_cast<const char*>(&meta.roomId), sizeof(meta.roomId));
    ofs.write(reinterpret_cast<const char*>(&nameLen), sizeof(nameLen));
    ofs.write(meta.roomName.data(), nameLen);
    ofs.write(reinterpret_cast<const char*>(&meta.hostPlayerId), sizeof(meta.hostPlayerId));
    ofs.write(reinterpret_cast<const char*>(&ipLen), sizeof(ipLen));
    ofs.write(meta.hostIP.data(), ipLen);
    ofs.write(reinterpret_cast<const char*>(&accountLen), sizeof(accountLen));
    ofs.write(meta.hostAccount.data(), accountLen);
    ofs.close();
    return true;
}

bool SaveManager::LoadRoomMeta(int roomId, RoomMeta& outMeta) {
    std::string path = GetMetaPath(roomId);
    std::ifstream ifs(path, std::ios::binary);
    if (!ifs.is_open()) return false;

    int nameLen = 0, ipLen = 0, accountLen = 0;
    ifs.read(reinterpret_cast<char*>(&outMeta.roomId), sizeof(outMeta.roomId));
    ifs.read(reinterpret_cast<char*>(&nameLen), sizeof(nameLen));
    std::string name(nameLen, '\0');
    ifs.read(&name[0], nameLen);
    outMeta.roomName = name;
    ifs.read(reinterpret_cast<char*>(&outMeta.hostPlayerId), sizeof(outMeta.hostPlayerId));
    ifs.read(reinterpret_cast<char*>(&ipLen), sizeof(ipLen));
    if (ipLen > 0 && ipLen < 256) {
        std::string ip(ipLen, '\0');
        ifs.read(&ip[0], ipLen);
        outMeta.hostIP = ip;
    }
    ifs.read(reinterpret_cast<char*>(&accountLen), sizeof(accountLen));
    if (accountLen > 0 && accountLen < 256) {
        std::string account(accountLen, '\0');
        ifs.read(&account[0], accountLen);
        outMeta.hostAccount = account;
    }
    ifs.close();
    return true;
}

std::vector<SaveManager::RoomMeta> SaveManager::GetAllRoomMetas() {
    std::vector<RoomMeta> result;
    // Scan saves/rooms/ directory
    std::string roomsDir = GetBaseDir() + "/rooms";
    // For simplicity, try room IDs 1-100
    for (int id = 1; id <= 100; id++) {
        RoomMeta meta;
        if (LoadRoomMeta(id, meta)) {
            result.push_back(meta);
        }
    }
    return result;
}

// ─── Delete ───

bool SaveManager::DeleteRoom(int roomId) {
    std::string dir = GetRoomDir(roomId);
    // Simple: delete the world.dat and meta files, skip full recursive delete for now
    std::string worldPath = GetWorldPath(roomId);
    std::string metaPath = GetMetaPath(roomId);
    std::remove(worldPath.c_str());
    std::remove(metaPath.c_str());
    // Also try to clean up player saves
    for (int pid = 1; pid <= 100; pid++) {
        std::string playerPath = GetPlayerPath(roomId, pid);
        std::remove(playerPath.c_str());
    }
    // Remove directories
    std::string playerDir = GetRoomDir(roomId) + "/players";
    RMDIR(playerDir.c_str());
    RMDIR(dir.c_str());
    return true;
}

void SaveManager::DeleteAllRooms() {
    for (int id = 1; id <= 100; id++) {
        DeleteRoom(id);
    }
    RMDIR((std::string(SAVE_DIR) + "/rooms").c_str());
    RMDIR(SAVE_DIR);
}
