#include "LobbyManager.h"
#include "SaveManager.h"
#include "SaveMessages.h"
#include "Constants.h"
#include <iostream>
#include <fstream>
#include <cmath>
#include <cstdio>

using namespace boost::asio;
using ip::udp;

LobbyManager::LobbyManager(udp::socket& s) : sock(s) {
    // 恢复持久化的房间
    auto metas = SaveManager::GetAllRoomMetas();
    for (auto& meta : metas) {
        auto room = std::make_unique<Room>(meta.roomId, meta.roomName, 4);
        room->SetOwner(meta.hostPlayerId);
        rooms[meta.roomId] = std::move(room);
        LoadObstacleGrid(rooms[meta.roomId].get());
        if (meta.roomId >= nextRoomId)
            nextRoomId = meta.roomId + 1;
        std::cout << "[Lobby] Loaded persistent room " << meta.roomId
                  << " '" << meta.roomName << "' (host=" << meta.hostPlayerId << ")" << std::endl;
    }

    LoadAccounts();
    std::cout << "[Lobby] Loaded " << accounts.size() << " accounts" << std::endl;
}

// ─── Client management ───

int LobbyManager::AddClient(const udp::endpoint& ep) {
    int id = nextPlayerId++;
    clients[id] = ep;
    lastSeen[id] = std::chrono::steady_clock::now();
    return id;
}

void LobbyManager::RemoveClient(int id) {
    LeaveRoom(id);
    clients.erase(id);
    lastSeen.erase(id);
    playerAccounts.erase(id);
}

void LobbyManager::TouchClient(int id) {
    lastSeen[id] = std::chrono::steady_clock::now();
}

std::vector<int> LobbyManager::GetTimeoutClients() {
    std::vector<int> timeoutIds;
    auto now = std::chrono::steady_clock::now();
    for (auto& p : lastSeen) {
        if (now - p.second > CLIENT_TIMEOUT)
            timeoutIds.push_back(p.first);
    }
    return timeoutIds;
}

const udp::endpoint* LobbyManager::GetEndpoint(int playerId) const {
    auto it = clients.find(playerId);
    return it != clients.end() ? &it->second : nullptr;
}

bool LobbyManager::HasClient(int id) const {
    return clients.count(id) > 0;
}

// ─── Send helpers ───

void LobbyManager::SendTo(int playerId, const game::GameMessage& msg) {
    auto it = clients.find(playerId);
    if (it == clients.end()) return;
    std::string data;
    msg.SerializeToString(&data);
    boost::system::error_code ec;
    sock.send_to(buffer(data), it->second, 0, ec);
}

void LobbyManager::BroadcastToRoom(int roomId, const game::GameMessage& msg) {
    auto* room = GetRoom(roomId);
    if (!room) return;
    std::string data;
    msg.SerializeToString(&data);
    boost::system::error_code ec;
    for (int pid : room->GetPlayerIds()) {
        auto it = clients.find(pid);
        if (it != clients.end())
            sock.send_to(buffer(data), it->second, 0, ec);
    }
}

// ─── Room management ───

int LobbyManager::CreateRoom(const std::string& name, int creatorId) {
    // Check for duplicate room name
    for (auto& pair : rooms) {
        if (pair.second->GetName() == name)
            return -1; // Duplicate name
    }

    // Remove player from current room first
    LeaveRoom(creatorId);

    int id = nextRoomId++;
    auto room = std::make_unique<Room>(id, name, 4);
    rooms[id] = std::move(room);
    LoadObstacleGrid(rooms[id].get());
    rooms[id]->SetOwner(creatorId);  // Set owner BEFORE JoinRoom so HostNotify is sent
    JoinRoom(creatorId, id);

    // Save room metadata for persistence (ownership by account)
    SaveManager::RoomMeta meta;
    meta.roomId = id;
    meta.roomName = name;
    meta.hostPlayerId = creatorId;
    auto accIt = playerAccounts.find(creatorId);
    if (accIt != playerAccounts.end())
        meta.hostAccount = accIt->second;
    SaveManager::SaveRoomMeta(meta);

    // Ensure client knows it's the host
    game::GameMessage hostMsg;
    hostMsg.mutable_host_notify()->set_hostid(creatorId);
    SendTo(creatorId, hostMsg);

    std::cout << "[Lobby] Room " << id << " '" << name << "' created by player " << creatorId << std::endl;
    return id;
}

std::vector<Room*> LobbyManager::GetRoomList() {
    std::vector<Room*> list;
    for (auto& pair : rooms)
        list.push_back(pair.second.get());
    return list;
}

Room* LobbyManager::GetRoom(int roomId) {
    auto it = rooms.find(roomId);
    return it != rooms.end() ? it->second.get() : nullptr;
}

bool LobbyManager::RemoveRoom(int roomId) {
    auto it = rooms.find(roomId);
    if (it == rooms.end()) return false;
    // Remove all players from room
    auto playerIds = it->second->GetPlayerIds();
    for (int pid : playerIds) {
        playerRoomMap.erase(pid);
    }
    rooms.erase(it);

    // Reset ID counter when no rooms remain
    if (rooms.empty())
        nextRoomId = 1;

    return true;
}

// ─── Player ↔ Room ───

bool LobbyManager::JoinRoom(int playerId, int roomId) {
    auto* room = GetRoom(roomId);
    if (!room) return false;

    if (room->IsInGame()) {
        if (room->GetPlayerCount() == 0) {
            // Empty room stuck in game: allow anyone to reset (timeout)
            room->EndGame();
            std::cout << "[Lobby] Room " << roomId << " game reset (was stuck, empty)" << std::endl;
        } else if (room->GetPlayerCount() >= room->GetMaxPlayers()) {
            // Room is full: reject
            return false;
        }
        // else: mid-game join allowed (fall through)
    }

    LeaveRoom(playerId);

    if (!room->AddPlayer(playerId)) return false;
    playerRoomMap[playerId] = roomId;

    // Restore ownership if this player is the original host (matched by account)
    bool ownershipRestored = false;
    if (room->GetPlayerCount() == 1) {
        SaveManager::RoomMeta meta;
        if (SaveManager::LoadRoomMeta(roomId, meta) && !meta.hostAccount.empty()) {
            auto accIt = playerAccounts.find(playerId);
            if (accIt != playerAccounts.end() && accIt->second == meta.hostAccount) {
                room->SetOwner(playerId);
                ownershipRestored = true;
                std::cout << "[Lobby] Room " << roomId
                          << " ownership restored to player " << playerId
                          << " (account: " << meta.hostAccount << ")" << std::endl;
            }
        }
    }

    // Notify client if they became the host
    if (ownershipRestored || room->GetOwnerId() == playerId) {
        game::GameMessage hostMsg;
        hostMsg.mutable_host_notify()->set_hostid(playerId);
        SendTo(playerId, hostMsg);
    }

    // Add player to the room's GameState with the player's global ID
    room->GetGameState().AddClientWithId(playerId, clients[playerId]);

    auto* pd = room->GetGameState().GetPlayerData(playerId);
    if (pd) {
        // Try to load saved player data
        auto accIt = playerAccounts.find(playerId);
        std::string playerPath = (accIt != playerAccounts.end())
            ? SaveManager::GetPlayerPathByAccount(roomId, accIt->second)
            : SaveManager::GetPlayerPath(roomId, playerId);
#ifdef _WIN32
        for (auto& c : playerPath) if (c == '/') c = '\\';
#endif
        std::ifstream pf(playerPath, std::ios::binary);
        bool loaded = false;
        if (pf.is_open()) {
            std::vector<uint8_t> pBytes((std::istreambuf_iterator<char>(pf)),
                                         std::istreambuf_iterator<char>());
            pf.close();
            if (!pBytes.empty()) {
                savemsg::PlayerSaveData ps;
                if (ps.ParseFromBytes(pBytes)) {
                    pd->posX = ps.posX;
                    pd->posY = ps.posY;
                    pd->posZ = ps.posZ;
                    pd->hp = ps.hp;
                    pd->maxHp = ps.maxHp;
                    pd->food = ps.food;
                    pd->water = ps.water;
                    loaded = true;
                }
            }
        }
        if (!loaded) {
            // Random spawn position for new players
            float angle = static_cast<float>(std::rand()) / RAND_MAX * 6.28318f;
            float radius = static_cast<float>(std::rand()) / RAND_MAX * 3.0f;
            pd->posX = std::cos(angle) * radius;
            pd->posZ = std::sin(angle) * radius;
            pd->posY = 0;
        }
    }

    std::cout << "[Lobby] Player " << playerId << " joined room " << roomId
              << (room->IsInGame() ? " (mid-game)" : "") << std::endl;
    return true;
}

bool LobbyManager::LeaveRoom(int playerId) {
    auto it = playerRoomMap.find(playerId);
    if (it == playerRoomMap.end()) return false;

    int roomId = it->second;
    auto* room = GetRoom(roomId);
    if (!room) {
        playerRoomMap.erase(it);
        return false;
    }

    bool isOwner = (room->GetOwnerId() == playerId);

    if (isOwner && !room->IsInGame()) {
        // Owner leaving: kick all players but KEEP the room (persistent world)
        auto playerIds = room->GetPlayerIds(); // copy before iterating
        for (int pid : playerIds) {
            room->RemovePlayer(pid);
            playerRoomMap.erase(pid);
            game::GameMessage kickMsg;
            kickMsg.mutable_leave_room_response()->set_success(true);
            SendTo(pid, kickMsg);
        }

        // Broadcast updated room list to ALL clients
        for (auto& c : clients) {
            HandleRoomListRequest(c.first);
        }
        return true;
    }

    // Normal leave (non-owner, or in-game)
    room->RemovePlayer(playerId);
    playerRoomMap.erase(it);

    // If room is empty but still marked in-game, reset it (player crashed/quit)
    if (room->GetPlayerCount() == 0 && room->IsInGame()) {
        AutoSaveWorld(roomId); // Save authoritative world state before ending
        room->EndGame();
        std::cout << "[Lobby] Room " << roomId << " game ended (all players left)" << std::endl;
    }

    // Broadcast updated room list to ALL clients
    for (auto& c : clients) {
        HandleRoomListRequest(c.first);
    }
    return true;
}

int LobbyManager::GetPlayerRoom(int playerId) const {
    auto it = playerRoomMap.find(playerId);
    return it != playerRoomMap.end() ? it->second : -1;
}

bool LobbyManager::StartGame(int roomId, int requesterId) {
    auto* room = GetRoom(roomId);
    if (!room) return false;
    if (room->GetOwnerId() != requesterId) return false;
    if (room->GetPlayerCount() < 1) return false;
    if (room->IsInGame()) return false;

    // Try to load world save
    std::string worldPath = SaveManager::GetWorldPath(roomId);
#ifdef _WIN32
    for (auto& c : worldPath) if (c == '/') c = '\\';
#endif
    std::vector<savemsg::SaveEnemyData> savedEnemies;
    std::vector<savemsg::SaveChestData> savedChests;
    std::ifstream worldFile(worldPath, std::ios::binary);
    if (worldFile.is_open()) {
        std::vector<uint8_t> worldBytes((std::istreambuf_iterator<char>(worldFile)),
                                         std::istreambuf_iterator<char>());
        worldFile.close();

        savemsg::WorldSaveData worldSave;
        if (worldSave.ParseFromBytes(worldBytes)) {
            savedEnemies = std::move(worldSave.enemies);
            savedChests = std::move(worldSave.chests);
            std::cout << "[Lobby] World restored from save for room " << roomId
                      << " (" << savedEnemies.size() << " enemies, "
                      << savedChests.size() << " chests)" << std::endl;
        }
    }

    room->StartGame(); // Sets inGame=true + timers
    if (!savedEnemies.empty()) {
        // Overwrite default spawn with saved enemies
        room->GetGameState().InitFromSave(savedEnemies.data(), (int)savedEnemies.size());
    }

    // Load saved data for all players in the room (use account-based path)
    for (int pid : room->GetPlayerIds()) {
        auto accIt = playerAccounts.find(pid);
        std::string playerPath = (accIt != playerAccounts.end())
            ? SaveManager::GetPlayerPathByAccount(roomId, accIt->second)
            : SaveManager::GetPlayerPath(roomId, pid);
#ifdef _WIN32
        for (auto& c : playerPath) if (c == '/') c = '\\';
#endif

        std::ifstream playerFile(playerPath, std::ios::binary);
        if (playerFile.is_open()) {
            std::vector<uint8_t> playerBytes((std::istreambuf_iterator<char>(playerFile)),
                                              std::istreambuf_iterator<char>());
            playerFile.close();

            if (!playerBytes.empty()) {
                savemsg::PlayerSaveData playerSave;
                if (playerSave.ParseFromBytes(playerBytes)) {
                    auto* pd = room->GetGameState().GetPlayerData(pid);
                    if (pd) {
                        pd->posX = playerSave.posX;
                        pd->posY = playerSave.posY;
                        pd->posZ = playerSave.posZ;
                        pd->hp = playerSave.hp;
                        pd->maxHp = playerSave.maxHp;
                        pd->food = playerSave.food;
                        pd->water = playerSave.water;
                    }
                    std::cout << "[Lobby] Player " << pid << " data restored" << std::endl;
                }
            }
        }

        // Send saved player data back to player (for backpack/equipment/ammo restore on client)
        // Re-read from same account-based path
        std::ifstream pf2(playerPath, std::ios::binary);
        if (pf2.is_open()) {
            std::vector<uint8_t> pBytes((std::istreambuf_iterator<char>(pf2)),
                                         std::istreambuf_iterator<char>());
            pf2.close();
            if (!pBytes.empty()) {
                uint32_t tag = (45 << 3) | 2;
                uint8_t tagBuf[4];
                int tagPos = 0;
                uint32_t t = tag;
                while (t > 0x7F) { tagBuf[tagPos++] = (uint8_t)((t & 0x7F) | 0x80); t >>= 7; }
                tagBuf[tagPos++] = (uint8_t)t;

                std::vector<uint8_t> loadMsg;
                loadMsg.insert(loadMsg.end(), tagBuf, tagBuf + tagPos);
                uint32_t dataLen2 = (uint32_t)pBytes.size();
                uint32_t dl = dataLen2;
                while (dl > 0x7F) { loadMsg.push_back((uint8_t)((dl & 0x7F) | 0x80)); dl >>= 7; }
                loadMsg.push_back((uint8_t)dl);
                loadMsg.insert(loadMsg.end(), pBytes.begin(), pBytes.end());

                auto epIt = clients.find(pid);
                if (epIt != clients.end())
                    sock.send_to(boost::asio::buffer(loadMsg.data(), loadMsg.size()), epIt->second);
            }
        }
    }

    // Restore chest contents from save
    for (auto& chest : savedChests) {
        // Update server's authoritative chest state
        auto& state = chestStates_[chest.chestId];
        state.clear();
        for (auto& item : chest.items) {
            game::ChestItemState cis;
            cis.set_itemid(item.itemId);
            cis.set_amount(item.amount);
            cis.set_x(item.x);
            cis.set_y(item.y);
            cis.set_gridtype(item.gridType);
            cis.set_gridindex(item.gridIndex);
            cis.set_rotated(item.rotated);
            state.push_back(cis);
        }

        // Broadcast restored chest state to all clients
        game::GameMessage chestMsg;
        auto* sync = chestMsg.mutable_chest_state_sync();
        sync->set_chestid(chest.chestId);
        sync->set_fromplayerid(0);
        for (auto& cis : state)
            *sync->add_items() = cis;
        BroadcastToRoom(roomId, chestMsg);
        std::cout << "[Lobby] Chest " << chest.chestId << " restored ("
                  << chest.items.size() << " items)" << std::endl;
    }

    // Notify all players in room
    game::GameMessage notify;
    notify.mutable_game_start_notify()->set_roomid(roomId);
    BroadcastToRoom(roomId, notify);

    std::cout << "[Lobby] Game started in room " << roomId << std::endl;
    return true;
}

// ─── Obstacle grid ───

void LobbyManager::LoadObstacleGrid(Room* room) {
    std::ifstream gridFile("map_grid.bin", std::ios::binary);
    if (!gridFile.is_open()) {
        std::cout << "[Room " << room->GetId() << "] Warning: map_grid.bin not found" << std::endl;
        return;
    }
    int gridW, gridH;
    gridFile.read(reinterpret_cast<char*>(&gridW), 4);
    gridFile.read(reinterpret_cast<char*>(&gridH), 4);
    for (int y = 0; y < gridH; y++) {
        for (int x = 0; x < gridW; x++) {
            uint8_t walkable;
            gridFile.read(reinterpret_cast<char*>(&walkable), 1);
            if (!walkable)
                room->GetPathfinding().SetWalkable(x, y, false);
        }
    }
    gridFile.close();
}

// ─── Message processing ───

void LobbyManager::ProcessMessage(const uint8_t* data, size_t len,
                                    const udp::endpoint& sender) {
    // Detect save messages by raw tag before GameMessage parsing
    // Tags: 33=PlayerSaveSubmit 35=WorldSaveSubmit 37=DeleteRoomRequest
    if (len >= 2) {
        // Varint decoding for first field tag
        uint32_t tag = 0;
        int shift = 0;
        size_t pos = 0;
        while (pos < len && pos < 5) {
            uint8_t byte = data[pos++];
            tag |= (uint32_t)(byte & 0x7F) << shift;
            if ((byte & 0x80) == 0) break;
            shift += 7;
        }
        int fieldNumber = tag >> 3;

        // Find player ID from endpoint
        int playerId = -1;
        for (auto& c : clients) {
            if (c.second == sender) { playerId = c.first; break; }
        }

        if (fieldNumber == 33 && playerId != -1) {
            HandlePlayerSaveSubmit(playerId, data, len);
            return;
        }
        if (fieldNumber == 35 && playerId != -1) {
            HandleWorldSaveSubmit(playerId, data, len);
            return;
        }
        if (fieldNumber == 37 && playerId != -1) {
            HandleDeleteRoomRequest(playerId, data, len);
            return;
        }
        if (fieldNumber == 42 && playerId != -1) {
            HandleClearAllSaves(playerId);
            return;
        }
        if (fieldNumber == 43) {
            HandleClearAccounts();
            return;
        }
    }

    game::GameMessage msg;
    if (!msg.ParseFromArray(data, (int)len)) return;

    // Find player ID from endpoint
    int playerId = -1;
    for (auto& c : clients) {
        if (c.second == sender) { playerId = c.first; break; }
    }

    // Handle login/register requests (no player ID needed)
    if (msg.has_login_request()) {
        HandleLoginRequest(msg, sender);
        return;
    }
    if (msg.has_register_request()) {
        HandleRegisterRequest(msg, sender);
        return;
    }

    // Handle join request (no player ID needed)
    if (msg.has_join_request()) {
        HandleJoinRequest(msg, sender);
        return;
    }

    if (playerId == -1) return; // Unknown sender
    TouchClient(playerId);

    // Lobby messages (handled here)
    if (msg.has_room_list_request()) {
        HandleRoomListRequest(playerId);
        return;
    }
    if (msg.has_create_room_request()) {
        HandleCreateRoom(playerId, msg);
        return;
    }
    if (msg.has_join_room_request()) {
        HandleJoinRoom(playerId, msg);
        return;
    }
    if (msg.has_leave_room_request()) {
        HandleLeaveRoom(playerId);
        return;
    }
    if (msg.has_start_game_request()) {
        HandleStartGame(playerId);
        return;
    }

    // Game messages — route to player's room
    RouteToRoom(playerId, msg, sender);
}

void LobbyManager::HandleJoinRequest(const game::GameMessage& msg,
                                       const udp::endpoint& sender) {
    // Check if sender is already registered
    for (auto& c : clients) {
        if (c.second == sender) {
            // Already connected, re-send AssignId
            game::GameMessage rep;
            rep.mutable_assign_id()->set_id(c.first);
            rep.mutable_assign_id()->set_ishost(false);
            SendTo(c.first, rep);
            return;
        }
    }

    int id = AddClient(sender);
    game::GameMessage rep;
    rep.mutable_assign_id()->set_id(id);
    rep.mutable_assign_id()->set_ishost(false);
    SendTo(id, rep);

    std::cout << "[Lobby] Player " << id << " connected" << std::endl;

    // Send current room list
    HandleRoomListRequest(id);
}

void LobbyManager::HandleRoomListRequest(int playerId) {
    game::GameMessage rep;
    auto* list = rep.mutable_room_list_response();
    for (auto* room : GetRoomList()) {
        int playerCount = room->GetPlayerCount();

        // Visibility rules:
        // - Empty rooms: only visible to the original host (matched by account)
        // - Non-empty rooms: visible to everyone
        if (playerCount == 0) {
            SaveManager::RoomMeta meta;
            bool isHost = false;
            if (SaveManager::LoadRoomMeta(room->GetId(), meta) && !meta.hostAccount.empty()) {
                auto it = playerAccounts.find(playerId);
                if (it != playerAccounts.end())
                    isHost = (meta.hostAccount == it->second);
            }
            if (!isHost) continue;
        }

        auto* info = list->add_rooms();
        info->set_roomid(room->GetId());
        info->set_roomname(room->GetName());
        info->set_playercount(playerCount);
        info->set_maxplayers(room->GetMaxPlayers());
        info->set_ingame(room->IsInGame());
    }
    SendTo(playerId, rep);
}

void LobbyManager::HandleCreateRoom(int playerId, const game::GameMessage& msg) {
    std::string name = msg.create_room_request().roomname();
    if (name.empty()) name = "Room";

    int roomId = CreateRoom(name, playerId);

    game::GameMessage rep;
    auto* resp = rep.mutable_create_room_response();
    if (roomId < 0) {
        resp->set_success(false);
        resp->set_roomid(0);
        resp->set_error("DUPLICATE");
        SendTo(playerId, rep);
        return;
    }

    resp->set_success(true);
    resp->set_roomid(roomId);
    SendTo(playerId, rep);

    // Broadcast updated room list to ALL clients (including room members)
    for (auto& c : clients) {
        HandleRoomListRequest(c.first);
    }
}

void LobbyManager::HandleJoinRoom(int playerId, const game::GameMessage& msg) {
    int roomId = msg.join_room_request().roomid();

    game::GameMessage rep;
    auto* resp = rep.mutable_join_room_response();

    if (JoinRoom(playerId, roomId)) {
        resp->set_success(true);
        resp->set_roomid(roomId);

        auto* room = GetRoom(roomId);
        // If room is in-game, notify the late joiner to load the game scene
        if (room && room->IsInGame()) {
            SendTo(playerId, rep); // Send join response first

            // Send saved player data if exists
            auto accIt = playerAccounts.find(playerId);
            std::string playerPath = (accIt != playerAccounts.end())
                ? SaveManager::GetPlayerPathByAccount(roomId, accIt->second)
                : SaveManager::GetPlayerPath(roomId, playerId);
#ifdef _WIN32
            for (auto& c : playerPath) if (c == '/') c = '\\';
#endif
            std::ifstream pf(playerPath, std::ios::binary);
            if (pf.is_open()) {
                std::vector<uint8_t> pBytes((std::istreambuf_iterator<char>(pf)),
                                             std::istreambuf_iterator<char>());
                pf.close();
                if (!pBytes.empty()) {
                    uint32_t tag = (45 << 3) | 2;
                    uint8_t tagBuf[4]; int tp = 0; uint32_t t = tag;
                    while (t > 0x7F) { tagBuf[tp++] = (uint8_t)((t & 0x7F) | 0x80); t >>= 7; }
                    tagBuf[tp++] = (uint8_t)t;
                    std::vector<uint8_t> lm(tagBuf, tagBuf + tp);
                    uint32_t dl = (uint32_t)pBytes.size();
                    while (dl > 0x7F) { lm.push_back((uint8_t)((dl & 0x7F) | 0x80)); dl >>= 7; }
                    lm.push_back((uint8_t)dl);
                    lm.insert(lm.end(), pBytes.begin(), pBytes.end());
                    auto ep = clients.find(playerId);
                    if (ep != clients.end())
                        sock.send_to(boost::asio::buffer(lm.data(), lm.size()), ep->second);
                }
            }

            // Send GameStartNotify so the late joiner loads the game scene
            game::GameMessage notify;
            notify.mutable_game_start_notify()->set_roomid(roomId);
            SendTo(playerId, notify);
            return; // Already sent response
        }
        SendTo(playerId, rep);
    } else {
        resp->set_success(false);
        resp->set_roomid(roomId);
        resp->set_error("Room is full or doesn't exist");
        SendTo(playerId, rep);
    }

    // Broadcast updated room list to ALL clients
    for (auto& c : clients) {
        HandleRoomListRequest(c.first);
    }
}

void LobbyManager::HandleLeaveRoom(int playerId) {
    LeaveRoom(playerId);

    game::GameMessage rep;
    rep.mutable_leave_room_response()->set_success(true);
    SendTo(playerId, rep);

    // Update room list for lobby players
    for (auto& c : clients) {
        if (playerRoomMap.find(c.first) == playerRoomMap.end())
            HandleRoomListRequest(c.first);
    }
}

void LobbyManager::HandleStartGame(int playerId) {
    int roomId = GetPlayerRoom(playerId);
    if (roomId == -1) return;
    StartGame(roomId, playerId);
}

// ─── Game message routing ───

void LobbyManager::RouteToRoom(int playerId, const game::GameMessage& msg,
                                 const udp::endpoint& sender) {
    int roomId = GetPlayerRoom(playerId);
    if (roomId == -1) return;
    auto* room = GetRoom(roomId);
    if (!room || !room->IsInGame()) return;

    auto& gs = room->GetGameState();

    // Handle game messages within room context
    if (msg.has_input()) {
        auto& in = msg.input();
        gs.TouchClient(playerId);

        auto* input = gs.GetPlayerInput(playerId);
        if (input) {
            input->moveX = in.movex();
            input->moveZ = in.movez();
            input->rotY = in.roty();
            input->running = in.running();
            input->aiming = in.aiming();
        }

        // Also update PlayerData for WorldState broadcast (every 20ms, not just the 100ms sync)
        auto* pd = gs.GetPlayerData(playerId);
        if (pd) {
            pd->rotY = in.roty();
            pd->isRunning = in.running();
            pd->isAiming = in.aiming();
        }
    }
    else if (msg.has_player_transform_sync()) {
        auto& t = msg.player_transform_sync();
        auto* ps = gs.GetPlayerData(playerId);
        if (!ps) return;
        gs.TouchClient(playerId);

        // Apply client-reported position with anti-cheat validation
        float newX = t.posx();
        float newY = t.posy();
        float newZ = t.posz();

        // Anti-cheat: reject unreasonable position jumps (teleport)
        float pdx = newX - ps->posX;
        float pdz = newZ - ps->posZ;
        float jumpDist = std::sqrt(pdx * pdx + pdz * pdz);
        if (jumpDist <= MAX_TELEPORT_DIST) {
            // Clamp to world bounds
            if (newX < WORLD_MIN_X) newX = WORLD_MIN_X;
            if (newX > WORLD_MAX_X) newX = WORLD_MAX_X;
            if (newZ < WORLD_MIN_Z) newZ = WORLD_MIN_Z;
            if (newZ > WORLD_MAX_Z) newZ = WORLD_MAX_Z;

            ps->posX = newX;
            ps->posY = newY;
            ps->posZ = newZ;
        }

        ps->rotY = t.roty(); ps->speed = t.speed();
        ps->isRunning = t.running(); ps->isAiming = t.aiming();
        ps->isArmed = t.armed();
        ps->lookDirX = t.lookdirx(); ps->lookDirY = t.lookdiry(); ps->lookDirZ = t.lookdirz();
    }
    else if (msg.has_shoot_request()) {
        auto& sr = msg.shoot_request();
        int shooterId = sr.shooterid();
        gs.TouchClient(shooterId);

        // Server-authoritative hit scan
        float fx = sr.fireposx(), fy = sr.fireposy(), fz = sr.fireposz();
        float dx = sr.dirx(), dy = sr.diry(), dz = sr.dirz();
        float dirLen = std::sqrt(dx*dx + dy*dy + dz*dz);
        if (dirLen < 0.001f) return;
        dx /= dirLen; dy /= dirLen; dz /= dirLen;

        float bestDist = 50.0f;
        int hitEnemyId = -1;
        for (auto& ep : gs.GetAllEnemies()) {
            auto& e = ep.second;
            if (e.isDead) continue;
            float edx = e.posX - fx, edz = e.posZ - fz;
            float t = edx*dx + edz*dz;
            if (t < 0) continue;
            float px = fx + dx*t, pz = fz + dz*t;
            float pdx = px - e.posX, pdz = pz - e.posZ;
            float pdist = std::sqrt(pdx*pdx + pdz*pdz);
            if (pdist < 0.8f && t < bestDist) {
                bestDist = t;
                hitEnemyId = e.enemyId;
            }
        }

        if (hitEnemyId >= 0) {
            const float DAMAGE = 15.0f;
            gs.ApplyEnemyDamage(hitEnemyId, DAMAGE);
            auto* es = gs.GetEnemy(hitEnemyId);

            game::GameMessage hitMsg;
            auto* hr = hitMsg.mutable_hit_result();
            hr->set_targettype(0);
            hr->set_targetid(hitEnemyId);
            hr->set_damage(DAMAGE);
            hr->set_remaininghp(es ? es->hp : 0);
            hr->set_attackerid(shooterId);
            BroadcastToRoom(roomId, hitMsg);
            // Enemy death animation plays via WorldState (isDead=true).
            // Actual despawn happens after deathTimer expires via Tick().
        }

        // Broadcast shoot event for visual FX
        game::GameMessage shootEvent;
        auto* se = shootEvent.mutable_shoot_event();
        se->set_shooterid(shooterId);
        se->set_fireposx(fx); se->set_fireposy(fy); se->set_fireposz(fz);
        se->set_dirx(dx); se->set_diry(dy); se->set_dirz(dz);
        BroadcastToRoom(roomId, shootEvent);
    }
    else if (msg.has_leave_request()) {
        // Player disconnecting
        LeaveRoom(playerId);
        RemoveClient(playerId);
    }
    else if (msg.has_player_respawn()) {
        auto& r = msg.player_respawn();
        auto* ps = gs.GetPlayerData(playerId);
        if (ps && ps->isDead && ps->deathTimer <= 0) {
            ps->isDead = false;
            ps->hp = ps->maxHp;
            float angle = static_cast<float>(std::rand()) / RAND_MAX * 6.28318f;
            float radius = static_cast<float>(std::rand()) / RAND_MAX * 3.0f;
            ps->posX = std::cos(angle) * radius;
            ps->posZ = std::sin(angle) * radius;
            ps->posY = 0;

            game::GameMessage respMsg;
            auto* rsp = respMsg.mutable_player_respawn();
            rsp->set_playerid(playerId);
            rsp->set_posx(ps->posX);
            rsp->set_posy(ps->posY);
            rsp->set_posz(ps->posZ);
            BroadcastToRoom(roomId, respMsg);
        }
    }
    else if (msg.has_chest_state_submit()) {
        const game::ChestStateSubmit& cs = msg.chest_state_submit();
        int chestId = cs.chestid();

        std::vector<game::ChestItemState>& state = this->chestStates_[chestId];
        state.clear();
        for (int i = 0; i < cs.items_size(); ++i) {
            state.push_back(cs.items(i));
        }

        game::GameMessage syncMsg;
        game::ChestStateSync* sync = syncMsg.mutable_chest_state_sync();
        sync->set_chestid(chestId);
        sync->set_fromplayerid(playerId);
        for (size_t i = 0; i < state.size(); ++i) {
            game::ChestItemState* item = sync->add_items();
            item->CopyFrom(state[i]);
        }
        BroadcastToRoom(roomId, syncMsg);

        std::cout << "[Room " << roomId << "] Chest " << chestId
                  << " state updated by player " << playerId
                  << " (" << state.size() << " items)" << std::endl;
    }
    else if (msg.has_chest_state_request()) {
        const game::ChestStateRequest& cr = msg.chest_state_request();
        int chestId = cr.chestid();

        game::GameMessage syncMsg;
        game::ChestStateSync* sync = syncMsg.mutable_chest_state_sync();
        sync->set_chestid(chestId);
        sync->set_fromplayerid(0);

        std::map<int, std::vector<game::ChestItemState>>::iterator it = this->chestStates_.find(chestId);
        if (it != this->chestStates_.end()) {
            for (size_t i = 0; i < it->second.size(); ++i) {
                game::ChestItemState* item = sync->add_items();
                item->CopyFrom(it->second[i]);
            }
        }

        SendTo(playerId, syncMsg);

        std::cout << "[Room " << roomId << "] Chest " << chestId
                  << " state requested by player " << playerId
                  << " (" << (it != this->chestStates_.end() ? (int)it->second.size() : 0) << " items)" << std::endl;
    }
}

// ─── Tick ───

void LobbyManager::Tick() {
    for (auto& pair : rooms) {
        auto* room = pair.second.get();
        if (!room->NeedsTick()) continue;

        auto now = std::chrono::steady_clock::now();
        float dt = 0.033f;

        // Physics tick (33ms)
        while (now - room->lastPhysTick >= DT_PHYS) {
            room->lastPhysTick += DT_PHYS;
            room->PhysicsTick(dt);
        }

        // Stats tick (500ms)
        if (now - room->lastStatsTick >= DT_STATS) {
            room->lastStatsTick = now;
            auto& gs = room->GetGameState();

            // Step 1: detect AI-caused deaths (justDied set by EnemyAI)
            // Must happen BEFORE PlayerStatsTick which resets justDied
            for (auto& p : gs.GetAllPlayerDatasMutable()) {
                if (p.second.justDied) {
                    game::GameMessage deathMsg;
                    deathMsg.mutable_player_death()->set_playerid(p.first);
                    BroadcastToRoom(room->GetId(), deathMsg);
                    p.second.justDied = false;
                }
            }

            // Step 2: run starvation/regen tick (may set new justDied)
            gs.PlayerStatsTick(0.5f);

            // Step 3: handle starvation deaths from PlayerStatsTick
            for (auto& p : gs.GetAllPlayerDatasMutable()) {
                if (p.second.justDied) {
                    game::GameMessage deathMsg;
                    deathMsg.mutable_player_death()->set_playerid(p.first);
                    BroadcastToRoom(room->GetId(), deathMsg);
                    p.second.justDied = false;
                }
            }

            // Step 4: broadcast all player stats
            for (auto& p : gs.GetAllPlayerDatas()) {
                game::GameMessage statsMsg;
                auto* stats = statsMsg.mutable_player_stats_sync();
                stats->set_id(p.first);
                stats->set_hp(p.second.hp);
                stats->set_maxhp(p.second.maxHp);
                stats->set_food(p.second.food);
                stats->set_water(p.second.water);
                stats->set_isdead(p.second.isDead);
                BroadcastToRoom(room->GetId(), statsMsg);
            }
        }

        // Broadcast world state (50ms)
        if (now - room->lastSendTick >= DT_SEND) {
            room->lastSendTick = now;
            auto& gs = room->GetGameState();

            game::GameMessage wsMsg;
            auto* ws = wsMsg.mutable_world_state();

            for (auto& p : gs.GetAllPlayerDatas()) {
                auto* ps = ws->add_players();
                ps->set_id(p.first);
                ps->set_posx(p.second.posX);
                ps->set_posy(p.second.posY);
                ps->set_posz(p.second.posZ);
                ps->set_roty(p.second.rotY);
                ps->set_speed(p.second.speed);
                ps->set_isrunning(p.second.isRunning);
                ps->set_isaiming(p.second.isAiming);
			ps->set_isarmed(p.second.isArmed);
                ps->set_lookdirx(p.second.lookDirX);
                ps->set_lookdiry(p.second.lookDirY);
                ps->set_lookdirz(p.second.lookDirZ);
            }

            for (auto& e : gs.GetAllEnemies()) {
                auto* es = ws->add_enemies();
                es->set_enemyid(e.first);
                es->set_posx(e.second.posX);
                es->set_posy(e.second.posY);
                es->set_posz(e.second.posZ);
                es->set_roty(e.second.rotY);
                es->set_speed(e.second.speed);
                es->set_state(e.second.state);
                es->set_isdead(e.second.isDead);
                es->set_isattack(e.second.isAttack);
            }

            BroadcastToRoom(room->GetId(), wsMsg);

            // Broadcast newly spawned enemies
            auto newEnemies = gs.GetNewEnemyIds();
            for (int enemyId : newEnemies) {
                auto* enemy = gs.GetEnemy(enemyId);
                if (enemy) {
                    game::GameMessage spawnMsg;
                    auto* spawn = spawnMsg.mutable_enemy_spawn();
                    spawn->set_enemyid(enemyId);
                    spawn->set_enemytype(enemy->enemyType);
                    spawn->set_posx(enemy->posX);
                    spawn->set_posy(enemy->posY);
                    spawn->set_posz(enemy->posZ);
                    spawn->set_roty(enemy->rotY);
                    BroadcastToRoom(room->GetId(), spawnMsg);
                }
            }

            // Broadcast enemy despawns
            auto despawns = gs.GetPendingDespawns();
            for (int enemyId : despawns) {
                game::GameMessage despawnMsg;
                despawnMsg.mutable_enemy_despawn()->set_enemyid(enemyId);
                despawnMsg.mutable_enemy_despawn()->set_reason(0);
                BroadcastToRoom(room->GetId(), despawnMsg);
            }
        }
    }

    // Handle timeouts
    auto timeoutIds = GetTimeoutClients();
    for (int id : timeoutIds) {
        LeaveRoom(id);
        RemoveClient(id);
        std::cout << "[Lobby] Player " << id << " timed out" << std::endl;
    }
}

// ─── Save message handlers ───

// Helper: read a length-delimited inner message from a tagged outer buffer
static uint8_t* ReadInnerBytes(const uint8_t* data, size_t totalLen, size_t& outLen) {
    size_t pos = 0;
    while (pos < totalLen && (data[pos] & 0x80)) pos++;
    if (pos < totalLen) pos++;

    uint32_t length = 0;
    int shift = 0;
    while (pos < totalLen) {
        uint8_t byte = data[pos++];
        length |= (uint32_t)(byte & 0x7F) << shift;
        if ((byte & 0x80) == 0) break;
        shift += 7;
    }

    if (pos + length > totalLen) return nullptr;
    outLen = length;
    uint8_t* inner = new uint8_t[length];
    memcpy(inner, data + pos, length);
    return inner;
}

// Build and send a simple save ack/response message as raw bytes
// tag = (fieldNumber<<3)|2, inner = [0x08, 0x00/0x01] (field 1 bool)
static void SendSaveResponse(LobbyManager* mgr, int playerId, int fieldNumber, bool success) {
    // Inner: field 1 bool
    uint8_t inner[2] = { 0x08, (uint8_t)(success ? 0x01 : 0x00) };
    // Outer tag varint
    uint32_t tag = (fieldNumber << 3) | 2;
    uint8_t buf[16];
    int pos = 0;
    // Write tag varint
    while (tag > 0x7F) { buf[pos++] = (uint8_t)((tag & 0x7F) | 0x80); tag >>= 7; }
    buf[pos++] = (uint8_t)tag;
    // Write inner length varint (2)
    buf[pos++] = 0x02;
    // Write inner data
    buf[pos++] = inner[0];
    buf[pos++] = inner[1];

    boost::asio::ip::udp::socket& sock = mgr->GetSocket();
    const auto* ep = mgr->GetEndpoint(playerId);
    if (ep) sock.send_to(boost::asio::buffer(buf, pos), *ep);
}

void LobbyManager::HandlePlayerSaveSubmit(int playerId, const uint8_t* data, size_t len) {
    size_t innerLen = 0;
    uint8_t* inner = ReadInnerBytes(data, len, innerLen);
    if (!inner) return;

    savemsg::PlayerSaveSubmit submit;
    if (!submit.ParseFromArray(inner, (int)innerLen)) {
        delete[] inner;
        return;
    }
    delete[] inner;

    int roomId = submit.room_id();
    // Build path and ensure directory exists
    auto accIt = playerAccounts.find(playerId);
    std::string dir = SaveManager::GetRoomDir(roomId) + "/players";
    std::string path = (accIt != playerAccounts.end())
        ? SaveManager::GetPlayerPathByAccount(roomId, accIt->second)
        : SaveManager::GetPlayerPath(roomId, playerId);
#ifdef _WIN32
    for (auto& c : dir) if (c == '/') c = '\\';
    for (auto& c : path) if (c == '/') c = '\\';
#endif
    SaveManager::EnsureDir(dir);
    auto bytes = submit.save_data().SerializeToBytes();
    std::ofstream ofs(path, std::ios::binary | std::ios::trunc);
    bool ok = ofs.is_open();
    if (ok) { ofs.write((const char*)bytes.data(), bytes.size()); ofs.close(); }
    else { std::cout << "[Save] Cannot open: " << path << std::endl; }

    std::cout << "[Save] Player " << playerId << " saved to room " << roomId
              << (ok ? " OK" : " FAIL") << std::endl;

    SendSaveResponse(this, playerId, 34, ok);
}

void LobbyManager::HandleWorldSaveSubmit(int playerId, const uint8_t* data, size_t len) {
    size_t innerLen = 0;
    uint8_t* inner = ReadInnerBytes(data, len, innerLen);
    if (!inner) return;

    savemsg::WorldSaveSubmit submit;
    if (!submit.ParseFromArray(inner, (int)innerLen)) {
        delete[] inner;
        return;
    }
    delete[] inner;

    int roomId = submit.room_id();
    auto* room = GetRoom(roomId);
    bool canSave = false;
    if (room) {
        if (room->GetOwnerId() == playerId) {
            canSave = true;
        } else {
            SaveManager::RoomMeta meta;
            if (SaveManager::LoadRoomMeta(roomId, meta) && !meta.hostAccount.empty()) {
                auto accIt = playerAccounts.find(playerId);
                if (accIt != playerAccounts.end() && accIt->second == meta.hostAccount)
                    canSave = true;
            }
        }
    }
    if (!canSave) {
        SendSaveResponse(this, playerId, 36, false);
        return;
    }

    std::string dir = SaveManager::GetRoomDir(roomId);
    std::string path = SaveManager::GetWorldPath(roomId);
#ifdef _WIN32
    for (auto& c : dir) if (c == '/') c = '\\';
    for (auto& c : path) if (c == '/') c = '\\';
#endif
    SaveManager::EnsureDir(dir);
    auto bytes = submit.save_data().SerializeToBytes();
    std::ofstream ofs(path, std::ios::binary | std::ios::trunc);
    bool ok = ofs.is_open();
    if (ok) { ofs.write((const char*)bytes.data(), bytes.size()); ofs.close(); }
    else { std::cout << "[Save] Cannot open: " << path << std::endl; }

    std::cout << "[Save] World saved for room " << roomId
              << (ok ? " OK" : " FAIL") << std::endl;

    SendSaveResponse(this, playerId, 36, ok);
}

void LobbyManager::HandleDeleteRoomRequest(int playerId, const uint8_t* data, size_t len) {
    size_t innerLen = 0;
    uint8_t* inner = ReadInnerBytes(data, len, innerLen);
    if (!inner) return;

    savemsg::DeleteRoomRequest req;
    if (!req.ParseFromArray(inner, (int)innerLen)) {
        delete[] inner;
        return;
    }
    delete[] inner;

    int roomId = req.room_id();
    auto* room = GetRoom(roomId);

    // Check ownership: either direct ownerId match, or account match (empty room re-login case)
    bool canDelete = false;
    if (room) {
        if (room->GetOwnerId() == playerId) {
            canDelete = true;
        } else {
            // Account-based fallback: player is the original host
            SaveManager::RoomMeta meta;
            if (SaveManager::LoadRoomMeta(roomId, meta) && !meta.hostAccount.empty()) {
                auto accIt = playerAccounts.find(playerId);
                if (accIt != playerAccounts.end() && accIt->second == meta.hostAccount)
                    canDelete = true;
            }
        }
    }

    if (!canDelete) {
        std::cout << "[Save] DeleteRoom FAILED: room=" << roomId
                  << " player=" << playerId
                  << " ownerId=" << (room ? room->GetOwnerId() : -999) << std::endl;
        SendSaveResponse(this, playerId, 38, false);
        return;
    }

    RemoveRoom(roomId);
    SaveManager::DeleteRoom(roomId);
    SendSaveResponse(this, playerId, 38, true); // DeleteRoomResponse = field 38

    std::cout << "[Save] Room " << roomId << " deleted by player " << playerId << std::endl;

    for (auto& c : clients)
        HandleRoomListRequest(c.first);
}

void LobbyManager::AutoSaveWorld(int roomId) {
    auto* room = GetRoom(roomId);
    if (!room) return;

    savemsg::WorldSaveData save;
    save.roomId = roomId;

    // Collect enemy data from server's authoritative GameState
    for (auto& pair : room->GetGameState().GetAllEnemies()) {
        auto& e = pair.second;
        savemsg::SaveEnemyData ed;
        ed.enemyId = e.enemyId;
        ed.posX = e.posX;
        ed.posY = e.posY;
        ed.posZ = e.posZ;
        ed.hp = e.hp;           // Server-authoritative HP
        ed.state = e.state;     // Server-authoritative state
        ed.isDead = e.isDead;   // Server-authoritative death
        save.enemies.push_back(ed);
    }

    // Collect chest data from server's authoritative chestStates_
    for (auto& pair : chestStates_) {
        int chestId = pair.first;
        // Only save chests for this room (simplified: all chests)
        savemsg::SaveChestData cd;
        cd.chestId = chestId;
        for (auto& cis : pair.second) {
            savemsg::SaveInventoryItem item;
            item.itemId = cis.itemid();
            item.amount = cis.amount();
            item.x = cis.x();
            item.y = cis.y();
            item.gridType = cis.gridtype();
            item.gridIndex = cis.gridindex();
            item.rotated = cis.rotated();
            cd.items.push_back(item);
        }
        save.chests.push_back(cd);
    }

    SaveManager::EnsureDir(SaveManager::GetRoomDir(roomId));
    std::string path = SaveManager::GetWorldPath(roomId);
#ifdef _WIN32
    for (auto& c : path) if (c == '/') c = '\\';
#endif
    auto bytes = save.SerializeToBytes();
    std::ofstream ofs(path, std::ios::binary | std::ios::trunc);
    if (ofs.is_open()) {
        ofs.write((const char*)bytes.data(), bytes.size());
        ofs.close();
        std::cout << "[Save] World auto-saved for room " << roomId
                  << " (" << save.enemies.size() << " enemies, "
                  << save.chests.size() << " chests)" << std::endl;
    }
}

void LobbyManager::HandleClearAllSaves(int playerId) {
    // Remove all rooms from memory
    std::vector<int> toRemove;
    for (auto& pair : rooms)
        toRemove.push_back(pair.first);
    for (int id : toRemove)
        RemoveRoom(id);

    // Delete all save files
    SaveManager::DeleteAllRooms();

    std::cout << "[Save] All rooms cleared by player " << playerId << std::endl;

    // Broadcast empty room list
    for (auto& c : clients)
        HandleRoomListRequest(c.first);
}

void LobbyManager::HandleClearAccounts() {
    accounts.clear();
    // Delete accounts file
    std::remove(ACCOUNTS_FILE);
    std::cout << "[Save] All accounts cleared" << std::endl;
}

// ─── Auth handlers ───

void LobbyManager::HandleLoginRequest(const game::GameMessage& msg,
                                       const udp::endpoint& sender) {
    std::string account = msg.login_request().account();
    std::string password = msg.login_request().password();

    game::GameMessage rep;
    auto* resp = rep.mutable_login_response();

    // Check if sender is already a registered client (re-login)
    for (auto& c : clients) {
        if (c.second == sender) {
            playerAccounts[c.first] = account;
            resp->set_success(true);
            resp->set_player_id(c.first);
            std::string data;
            rep.SerializeToString(&data);
            sock.send_to(boost::asio::buffer(data), sender);
            std::cout << "[Lobby] Player " << c.first
                      << " re-authenticated as '" << account << "'" << std::endl;
            return;
        }
    }

    auto it = accounts.find(account);
    if (it == accounts.end()) {
        resp->set_success(false);
        resp->set_error("账号不存在，请注册账号！");
        std::string data;
        rep.SerializeToString(&data);
        sock.send_to(boost::asio::buffer(data), sender);
        return;
    }

    if (it->second != password) {
        resp->set_success(false);
        resp->set_error("密码错误，请重新输入！");
        std::string data;
        rep.SerializeToString(&data);
        sock.send_to(boost::asio::buffer(data), sender);
        return;
    }

    // Check if account is already logged in by another client
    for (auto& pa : playerAccounts) {
        if (pa.second == account) {
            // Verify the old client is still connected
            auto oldIt = clients.find(pa.first);
            if (oldIt != clients.end() && oldIt->second != sender) {
                resp->set_success(false);
                resp->set_error("账号已被登录！");
                std::string data;
                rep.SerializeToString(&data);
                sock.send_to(boost::asio::buffer(data), sender);
                return;
            }
        }
    }

    int id = AddClient(sender);
    playerAccounts[id] = account;
    resp->set_success(true);
    resp->set_player_id(id);

    std::string data;
    rep.SerializeToString(&data);
    sock.send_to(boost::asio::buffer(data), sender);

    std::cout << "[Lobby] Player " << id << " logged in as '" << account << "'" << std::endl;

    HandleRoomListRequest(id);
}

void LobbyManager::HandleRegisterRequest(const game::GameMessage& msg,
                                          const udp::endpoint& sender) {
    std::string account = msg.register_request().account();
    std::string password = msg.register_request().password();

    game::GameMessage rep;
    auto* resp = rep.mutable_login_response();

    auto it = accounts.find(account);
    if (it != accounts.end()) {
        resp->set_success(false);
        resp->set_error("账号已存在，请登录！");
    } else {
        accounts[account] = password;
        SaveAccounts();
        resp->set_success(false);
        resp->set_error("注册成功，请登录！");
    }

    std::string data;
    rep.SerializeToString(&data);
    sock.send_to(boost::asio::buffer(data), sender);

    std::cout << "[Lobby] Register attempt for '" << account
              << "': " << (it != accounts.end() ? "already exists" : "success") << std::endl;
}

// ─── Account persistence ───

void LobbyManager::LoadAccounts() {
    std::ifstream ifs(ACCOUNTS_FILE, std::ios::binary);
    if (!ifs.is_open()) return;

    int count = 0;
    ifs.read(reinterpret_cast<char*>(&count), sizeof(count));
    for (int i = 0; i < count; i++) {
        int nameLen = 0;
        ifs.read(reinterpret_cast<char*>(&nameLen), sizeof(nameLen));
        if (nameLen <= 0 || nameLen > 256) break;
        std::string account(nameLen, '\0');
        ifs.read(&account[0], nameLen);

        int passLen = 0;
        ifs.read(reinterpret_cast<char*>(&passLen), sizeof(passLen));
        if (passLen <= 0 || passLen > 256) break;
        std::string password(passLen, '\0');
        ifs.read(&password[0], passLen);

        accounts[account] = password;
    }
    ifs.close();
}

void LobbyManager::SaveAccounts() {
    SaveManager::EnsureDir("saves");
    std::ofstream ofs(ACCOUNTS_FILE, std::ios::binary | std::ios::trunc);
    if (!ofs.is_open()) {
        std::cerr << "[Save] Failed to write accounts file" << std::endl;
        return;
    }

    int count = static_cast<int>(accounts.size());
    ofs.write(reinterpret_cast<const char*>(&count), sizeof(count));
    for (const auto& pair : accounts) {
        int nameLen = static_cast<int>(pair.first.size());
        ofs.write(reinterpret_cast<const char*>(&nameLen), sizeof(nameLen));
        ofs.write(pair.first.data(), nameLen);

        int passLen = static_cast<int>(pair.second.size());
        ofs.write(reinterpret_cast<const char*>(&passLen), sizeof(passLen));
        ofs.write(pair.second.data(), passLen);
    }
    ofs.close();
}
