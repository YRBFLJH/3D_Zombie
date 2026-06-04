#include "Room.h"
#include "Constants.h"
#include <iostream>
#include <cmath>
#include <algorithm>

Room::Room(int id, const std::string& name, int maxPlayers)
    : roomId(id), roomName(name), maxPlayers(maxPlayers) {
    pathfinding.Init(200, 200, 1.0f);
    enemyAI.Init(&pathfinding);

    auto now = std::chrono::steady_clock::now();
    lastPhysTick = now;
    lastSendTick = now;
    lastStatsTick = now;
}

bool Room::AddPlayer(int playerId) {
    if ((int)playerIds.size() >= maxPlayers) return false;
    if (HasPlayer(playerId)) return false;
    playerIds.push_back(playerId);
    if (ownerId == -1) ownerId = playerId;
    return true;
}

bool Room::RemovePlayer(int playerId) {
    auto it = std::find(playerIds.begin(), playerIds.end(), playerId);
    if (it == playerIds.end()) return false;
    playerIds.erase(it);

    gameState.RemoveClient(playerId);

    if (playerId == ownerId) {
        gameState.UpdateHostAfterRemove(playerId);
        ownerId = playerIds.empty() ? -1 : playerIds[0];
    }

    // If room is empty after removal, it should be cleaned up
    return true;
}

bool Room::HasPlayer(int playerId) const {
    return std::find(playerIds.begin(), playerIds.end(), playerId) != playerIds.end();
}

void Room::StartGame() {
    if (inGame) return;
    inGame = true;
    gameState.Init();
    auto now = std::chrono::steady_clock::now();
    lastPhysTick = now;
    lastSendTick = now;
    lastStatsTick = now;
    std::cout << "[Room " << roomId << "] Game started with " << playerIds.size() << " players" << std::endl;
}

void Room::PhysicsTick(float dt) {
    gameState.PhysicsTick(dt, &pathfinding);
    enemyAI.Update(dt, gameState, gameState.HasPlayers());
    // PlayerStatsTick is called separately after death detection in LobbyManager::Tick
}
