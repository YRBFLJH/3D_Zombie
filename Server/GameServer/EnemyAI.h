#pragma once
#include "GameState.h"
#include "Pathfinding.h"
#include <vector>

class EnemyAI {
public:
    void Init(Pathfinding* pathfinder);
    // Server-authoritative: always runs enemy AI movement regardless of player count.
    // Attack damage and death logic are also fully server-authoritative.
    void Update(float dt, GameState& state, bool hasPlayers);

private:
    Pathfinding* pf = nullptr;
    float attackCooldownTimer = 0;

    void UpdateEnemy(float dt, EnemyData& enemy, GameState& state, bool hasPlayers);
    PlayerData* FindPlayerInSight(EnemyData& enemy, GameState& state);
    void MoveToward(float dt, EnemyData& enemy, float targetX, float targetZ, float speed);
    void FollowPath(float dt, EnemyData& enemy, float speed);
};
