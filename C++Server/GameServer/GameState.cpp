#include "GameState.h"
#include "SaveMessages.h"
#include <cmath>
#include <algorithm>
#include <iostream>
#include <random>

using namespace boost::asio;
using ip::udp;

static std::mt19937 rng(std::random_device{}());

static float RandomFloat(float min, float max) {
    std::uniform_real_distribution<float> dist(min, max);
    return dist(rng);
}

GameState::GameState() {}

void GameState::Init() {
    // Clear any leftover enemies from previous game
    enemies.clear();
    newEnemyIds.clear();
    nextEnemyId = 1000;

    SpawnEnemy(0, 10, 0, 15);
}

void GameState::InitFromSave(savemsg::SaveEnemyData* enemiesData, int enemyCount) {
    // Clear any leftover enemies
    enemies.clear();
    newEnemyIds.clear();

    for (int i = 0; i < enemyCount; i++) {
        auto& se = enemiesData[i];
        EnemyData es;
        es.enemyId = nextEnemyId++;
        es.enemyType = 0;
        es.posX = se.posX;
        es.posY = se.posY;
        es.posZ = se.posZ;
        es.hp = se.hp;
        es.maxHp = DEFAULT_ENEMY_HP;
        es.state = se.state;
        es.isDead = se.isDead;
        es.rotY = RandomFloat(0, 360.0f);
        es.speed = 0;
        es.originalPosition = { se.posX, se.posZ };
        es.attackTimer = 0;
        es.deathTimer = se.isDead ? ENEMY_DEATH_ANIM_DURATION : 0;
        es.pendingDespawn = false;
        enemies[es.enemyId] = es;
        if (!se.isDead)
            newEnemyIds.push_back(es.enemyId);
    }

    if (enemies.empty()) {
        // Fallback: spawn a fresh enemy
        SpawnEnemy(0, 10, 0, 15);
    }
}

// ---------- 接入管理 ----------

int GameState::AddClient(const udp::endpoint& ep) {
    int id = nextId++;
    AddClientWithId(id, ep);
    if (hostClientId == -1) {
        hostClientId = id;
        std::cout << "设置初始主机: " << hostClientId << std::endl;
    }
    return id;
}

void GameState::AddClientWithId(int id, const udp::endpoint& ep) {
    clients[id] = ep;
    states[id] = PlayerData();
    inputs[id] = PlayerInput();
    last_seen[id] = std::chrono::steady_clock::now();
    clientIds.push_back(id);
    if (id >= nextId) nextId = id + 1;
}

void GameState::RemoveClient(int id) {
    clients.erase(id);
    states.erase(id);
    inputs.erase(id);
    last_seen.erase(id);
}

void GameState::TouchClient(int id) {
    last_seen[id] = std::chrono::steady_clock::now();
}

std::vector<int> GameState::GetTimeoutClients() {
    std::vector<int> timeoutIds;
    auto now = std::chrono::steady_clock::now();
    for (auto& p : last_seen) {
        if (now - p.second > CLIENT_TIMEOUT) {
            timeoutIds.push_back(p.first);
        }
    }
    return timeoutIds;
}

bool GameState::HasClient(int id) const {
    return clients.count(id) > 0;
}

void GameState::UpdateHostAfterRemove(int removedId) {
    clientIds.erase(std::remove(clientIds.begin(), clientIds.end(), removedId), clientIds.end());
    if (removedId == hostClientId) {
        if (!clientIds.empty()) {
            hostClientId = clientIds[0];
            std::cout << "主机离开，新主机: " << hostClientId << std::endl;
        } else {
            hostClientId = -1;
            std::cout << "所有客户端离开，无主机" << std::endl;
        }
    }
}

// ---------- 玩家 ----------

PlayerData* GameState::GetPlayerData(int id) {
    auto it = states.find(id);
    return it != states.end() ? &it->second : nullptr;
}

PlayerInput* GameState::GetPlayerInput(int id) {
    auto it = inputs.find(id);
    return it != inputs.end() ? &it->second : nullptr;
}

// ---------- 物理更新 ----------

void GameState::PhysicsTick(float dt, Pathfinding* pf) {
    for (auto& p : states) {
        int id = p.first;
        auto& s = p.second;
        if (s.isDead) continue;
        auto it = inputs.find(id);
        if (it == inputs.end()) continue;
        auto& i = it->second;

        bool isMoving = (std::fabs(i.moveX) > 0.1f || std::fabs(i.moveZ) > 0.1f);
        float targetSpeed = isMoving ? (i.running ? RUN_SPEED : WALK_SPEED) : 0.0f;
        s.speed = targetSpeed;

        // Position is client-authoritative via PlayerTransformSync.
        // Server no longer moves players to avoid collision desync with client.
    }
}

// ---------- 玩家属性更新 ----------

void GameState::PlayerStatsTick(float dt) {
    // 重置死亡标记
    for (auto& p : states) p.second.justDied = false;

    for (auto& p : states) {
        auto& s = p.second;
        if (s.isDead) continue;

        s.food -= FOOD_DEPLETE_PER_SEC * dt;
        s.water -= WATER_DEPLETE_PER_SEC * dt;

        if (s.food <= 0) {
            s.food = 0;
            s.hp -= STARVATION_DMG_PER_SEC * dt;
        }
        if (s.water <= 0) {
            s.water = 0;
            s.hp -= DEHYDRATION_DMG_PER_SEC * dt;
        }

        if (s.food > 20.0f && s.water > 20.0f && s.hp > 0 && s.hp < s.maxHp) {
            s.hp += HP_REGEN_PER_SEC * dt;
            if (s.hp > s.maxHp) s.hp = s.maxHp;
        }

        if (s.hp <= 0) {
            s.hp = 0;
            s.isDead = true;
            s.deathTimer = RESPAWN_COOLDOWN;
            s.justDied = true;
        }
    }

    for (auto& p : states) {
        auto& s = p.second;
        if (s.isDead && s.deathTimer > 0) {
            s.deathTimer -= dt;
        }
    }
}

// ---------- 敌人 ----------

int GameState::SpawnEnemy(int enemyType, float x, float y, float z) {
    EnemyData es;
    es.enemyId = nextEnemyId++;
    es.enemyType = enemyType;
    es.posX = x;
    es.posY = y;
    es.posZ = z;
    es.rotY = RandomFloat(0, 360.0f);
    es.state = 0;
    es.originalPosition = { x, z };
    es.attackTimer = 0;
    es.deathTimer = ENEMY_DEATH_ANIM_DURATION;
    es.pendingDespawn = false;
    enemies[es.enemyId] = es;
    newEnemyIds.push_back(es.enemyId);
    return es.enemyId;
}

void GameState::DespawnEnemy(int enemyId) {
    enemies.erase(enemyId);
}

EnemyData* GameState::GetEnemy(int enemyId) {
    auto it = enemies.find(enemyId);
    return it != enemies.end() ? &it->second : nullptr;
}

void GameState::ApplyEnemyDamage(int enemyId, float damage) {
    auto it = enemies.find(enemyId);
    if (it == enemies.end()) return;
    auto& es = it->second;
    if (es.isDead) return;
    es.hp -= damage;
    if (es.hp <= 0) {
        es.hp = 0;
        es.isDead = true;
        es.state = 4;
    }
}

bool GameState::IsEnemyDead(int enemyId) const {
    auto it = enemies.find(enemyId);
    return it == enemies.end() || it->second.isDead;
}

std::vector<int> GameState::GetNewEnemyIds() {
    std::vector<int> ids;
    ids.swap(newEnemyIds);
    return ids;
}

std::vector<int> GameState::GetPendingDespawns() {
    std::vector<int> ids;
    for (auto& pair : enemies) {
        if (pair.second.pendingDespawn) {
            ids.push_back(pair.first);
        }
    }
    for (int id : ids) {
        enemies.erase(id);
    }
    return ids;
}
