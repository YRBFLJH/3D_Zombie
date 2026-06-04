#pragma once
#include "Room.h"
#include "NetworkMessage.pb.h"
#include <map>
#include <memory>
#include <vector>
#include <boost/asio.hpp>

class LobbyManager {
public:
    LobbyManager(boost::asio::ip::udp::socket& sock);

    // Room management
    int CreateRoom(const std::string& name, int creatorId);
    std::vector<Room*> GetRoomList();
    Room* GetRoom(int roomId);
    bool RemoveRoom(int roomId);

    // Player ↔ Room
    bool JoinRoom(int playerId, int roomId);
    bool LeaveRoom(int playerId);
    int GetPlayerRoom(int playerId) const;
    bool StartGame(int roomId, int requesterId);

    // Client management
    int AddClient(const boost::asio::ip::udp::endpoint& ep);
    void RemoveClient(int id);
    void TouchClient(int id);
    std::vector<int> GetTimeoutClients();
    const boost::asio::ip::udp::endpoint* GetEndpoint(int playerId) const;
    bool HasClient(int id) const;

    // Message processing
    void ProcessMessage(const uint8_t* data, size_t len,
        const boost::asio::ip::udp::endpoint& sender);

    // Tick all active game rooms
    void Tick();

    // Sending
    boost::asio::ip::udp::socket& GetSocket() { return sock; }
    void SendTo(int playerId, const game::GameMessage& msg);
    void BroadcastToRoom(int roomId, const game::GameMessage& msg);

    // Access
    const std::map<int, boost::asio::ip::udp::endpoint>& GetClients() const { return clients; }

private:
    boost::asio::ip::udp::socket& sock;
    std::map<int, std::unique_ptr<Room>> rooms;
    int nextRoomId = 1;

    // Client tracking
    std::map<int, boost::asio::ip::udp::endpoint> clients;
    std::map<int, std::chrono::steady_clock::time_point> lastSeen;
    std::map<int, int> playerRoomMap; // playerId → roomId
    int nextPlayerId = 1;

    // Chest state sync: chestId -> item list (server authoritative)
    std::map<int, std::vector<game::ChestItemState>> chestStates_;

    // Account storage: account -> password
    std::map<std::string, std::string> accounts;
    // Player -> account mapping for room ownership
    std::map<int, std::string> playerAccounts;

    // Message handlers
    void HandleJoinRequest(const game::GameMessage& msg, const boost::asio::ip::udp::endpoint& sender);
    void HandleRoomListRequest(int playerId);
    void HandleCreateRoom(int playerId, const game::GameMessage& msg);
    void HandleJoinRoom(int playerId, const game::GameMessage& msg);
    void HandleLeaveRoom(int playerId);
    void HandleStartGame(int playerId);
    void AutoSaveWorld(int roomId);

    // Auth message handlers
    void HandleLoginRequest(const game::GameMessage& msg,
        const boost::asio::ip::udp::endpoint& sender);
    void HandleRegisterRequest(const game::GameMessage& msg,
        const boost::asio::ip::udp::endpoint& sender);

    // Account persistence
    void LoadAccounts();
    void SaveAccounts();

    // Save message handlers
    void HandlePlayerSaveSubmit(int playerId, const uint8_t* data, size_t len);
    void HandleWorldSaveSubmit(int playerId, const uint8_t* data, size_t len);
    void HandleDeleteRoomRequest(int playerId, const uint8_t* data, size_t len);
    void HandleClearAllSaves(int playerId);
    void HandleClearAccounts();

    // Game message routing (delegates to player's room)
    void RouteToRoom(int playerId, const game::GameMessage& msg,
        const boost::asio::ip::udp::endpoint& sender);

    // Load obstacle grid data
    void LoadObstacleGrid(Room* room);
};
