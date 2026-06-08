#pragma once
#include <boost/asio.hpp>
#include "NetworkMessage.pb.h"
#include "GameState.h"

class NetworkHandler {
public:
    NetworkHandler(GameState& state, boost::asio::ip::udp::socket& sock);

    // 处理接收到的消息 (返回 true 表示有消息被处理)
    bool ProcessMessage(const uint8_t* data, size_t len, const boost::asio::ip::udp::endpoint& sender);

    // 广播
    void BroadcastWorldState();
    void BroadcastPlayerStats();
    void BroadcastHitResult(const game::HitResult& hit);
    void BroadcastEnemySpawn(int enemyId, int enemyType, float x, float y, float z, float rotY);
    void BroadcastEnemyDespawn(int enemyId, int reason);
    void BroadcastPlayerDeath(int playerId);
    void BroadcastPlayerRespawn(int playerId, float x, float y, float z);
    void BroadcastHostNotify(int hostId);

    // 单播
    void SendAssignId(const boost::asio::ip::udp::endpoint& target, int id, bool isHost);
    void SendTo(const boost::asio::ip::udp::endpoint& target, const game::GameMessage& msg);

private:
    GameState& state;
    boost::asio::ip::udp::socket& sock;

    void HandleJoinRequest(const boost::asio::ip::udp::endpoint& sender);
    void HandleLeaveRequest(const boost::asio::ip::udp::endpoint& sender);
    void HandleInput(const game::GameMessage& msg, const boost::asio::ip::udp::endpoint& sender);
    void HandleEnemyStateSyncBatch(const game::GameMessage& msg);
    void HandleEnemyStateSync(const game::GameMessage& msg);
    void HandlePlayerTransformSync(const game::GameMessage& msg);
    void HandleShootRequest(const game::GameMessage& msg);
    void HandlePlayerRespawn(const game::GameMessage& msg);

    void BroadcastToAll(const game::GameMessage& msg, int excludeId = -1);

    boost::system::error_code ec;
    std::map<int, std::chrono::steady_clock::time_point> lastShootTime;
};
