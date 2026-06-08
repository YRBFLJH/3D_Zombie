#pragma once
#include "GameState.h"
#include "Pathfinding.h"
#include "EnemyAI.h"
#include <string>
#include <vector>
#include <chrono>
#include <memory>

class Room {
public:
    Room(int id, const std::string& name, int maxPlayers);

    int GetId() const { return roomId; }
    const std::string& GetName() const { return roomName; }
    int GetPlayerCount() const { return (int)playerIds.size(); }
    int GetMaxPlayers() const { return maxPlayers; }
    bool IsInGame() const { return inGame; }
    const std::vector<int>& GetPlayerIds() const { return playerIds; }
    int GetOwnerId() const { return ownerId; }

    bool AddPlayer(int playerId);
    bool RemovePlayer(int playerId);
    bool HasPlayer(int playerId) const;
    void SetOwner(int playerId) { ownerId = playerId; }

    void StartGame();
    void EndGame() { inGame = false; }
    bool NeedsTick() const { return inGame; }
    void PhysicsTick(float dt);
    void BroadcastWorldState(class LobbyManager* lobby);

    GameState& GetGameState() { return gameState; }
    Pathfinding& GetPathfinding() { return pathfinding; }
    EnemyAI& GetEnemyAI() { return enemyAI; }

    // Per-room timing
    std::chrono::steady_clock::time_point lastPhysTick;
    std::chrono::steady_clock::time_point lastSendTick;
    std::chrono::steady_clock::time_point lastStatsTick;

    // Enemy spawn/despawn tracking
    void ProcessEnemyEvents(class LobbyManager* lobby);

private:
    int roomId;
    std::string roomName;
    int maxPlayers;
    bool inGame = false;
    int ownerId = -1;
    std::vector<int> playerIds;

    GameState gameState;
    Pathfinding pathfinding;
    EnemyAI enemyAI;
};
