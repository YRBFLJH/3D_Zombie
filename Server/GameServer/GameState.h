#pragma once
#include <map>
#include <vector>
#include <chrono>
#include <utility>
#include <boost/asio.hpp>
#include "Constants.h"
#include "Pathfinding.h"
#include "SaveMessages.h"

// Player state
struct PlayerData {
    float posX = 0, posY = 1.0f, posZ = 0;
    float rotY = 0;
    float speed = 0;
    bool isRunning = false;
    bool isAiming = false;
    bool isArmed = false;
    float lookDirX = 0, lookDirY = 0, lookDirZ = 1.0f;
    float hp = DEFAULT_MAX_HP;
    float maxHp = DEFAULT_MAX_HP;
    float food = DEFAULT_MAX_FOOD;
    float water = DEFAULT_MAX_WATER;
    bool isDead = false;
    float deathTimer = 0;
    bool justDied = false;
};

struct PlayerInput {
    float moveX = 0, moveZ = 0;
    float rotY = 0;
    bool running = false;
    bool aiming = false;
};

struct EnemyData {
    int enemyId = 0;
    int enemyType = 0;
    float posX = 0, posY = 0, posZ = 0;
    float rotY = 0;
    float speed = 0;
    int state = 0;         // 0=Idle,1=Walk,2=Run,3=Attack,4=Dead
    bool isDead = false;
    bool isAttack = false;
    std::pair<float, float> originalPosition;
    float hp = DEFAULT_ENEMY_HP;
    float maxHp = DEFAULT_ENEMY_HP;
    float attackTimer = 0;
    float deathTimer = 0;
    bool pendingDespawn = false;
    int pathIndex = 0;
    std::vector<Float2> currentPath;
    float lastRepathTargetX = 0;
    float lastRepathTargetZ = 0;
    float patrolTimer = 0;
    float repathCooldown = 0;
    int targetPlayerId = -1;   // locked target: once acquired, don't lose until dead
    bool hit1Done = false;
    bool hit2Done = false;
};

class GameState {
public:
    GameState();
    void Init();
    void InitFromSave(savemsg::SaveEnemyData* enemiesData, int enemyCount);

    int AddClient(const boost::asio::ip::udp::endpoint& ep);
    void AddClientWithId(int id, const boost::asio::ip::udp::endpoint& ep);
    void RemoveClient(int id);
    void TouchClient(int id);
    std::vector<int> GetTimeoutClients();

    void UpdateHostAfterRemove(int removedId);
    int GetHostId() const { return hostClientId; }
    bool HasHost() const { return hostClientId != -1; }

    PlayerData* GetPlayerData(int id);
    PlayerInput* GetPlayerInput(int id);
    const std::map<int, PlayerData>& GetAllPlayerDatas() const { return states; }
    std::map<int, PlayerData>& GetAllPlayerDatasMutable() { return states; }
    const std::map<int, PlayerInput>& GetAllPlayerInputs() const { return inputs; }
    const std::map<int, boost::asio::ip::udp::endpoint>& GetClients() const { return clients; }
    const std::vector<int>& GetClientIds() const { return clientIds; }
    bool HasClient(int id) const;

    void PhysicsTick(float dt, class Pathfinding* pf = nullptr);
    void PlayerStatsTick(float dt);

    int SpawnEnemy(int enemyType, float x, float y, float z);
    void DespawnEnemy(int enemyId);
    EnemyData* GetEnemy(int enemyId);
    const std::map<int, EnemyData>& GetAllEnemies() const { return enemies; }
    std::map<int, EnemyData>& GetAllEnemiesMutable() { return enemies; }
    void ApplyEnemyDamage(int enemyId, float damage);
    bool IsEnemyDead(int enemyId) const;

    std::vector<int> GetNewEnemyIds();
    std::vector<int> GetPendingDespawns();
    bool HasPlayers() const { return !clients.empty(); }

private:
    std::map<int, boost::asio::ip::udp::endpoint> clients;
    std::map<int, PlayerData> states;
    std::map<int, PlayerInput> inputs;
    std::map<int, std::chrono::steady_clock::time_point> last_seen;
    int nextId = 1;

    int hostClientId = -1;
    std::vector<int> clientIds;

    std::map<int, EnemyData> enemies;
    int nextEnemyId = 1000;
    std::vector<int> newEnemyIds;
};
