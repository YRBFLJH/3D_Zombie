#include "EnemyAI.h"
#include <cmath>

void EnemyAI::Init(Pathfinding* pathfinder) {
    pf = pathfinder;
}

void EnemyAI::Update(float dt, GameState& state, bool hasPlayers) {
    auto& enemies = state.GetAllEnemiesMutable();
    for (auto& pair : enemies) {
        UpdateEnemy(dt, pair.second, state, hasPlayers);
    }
}

PlayerData* EnemyAI::FindPlayerInSight(EnemyData& enemy, GameState& state) {
    // If already locked on a target, keep it until the player dies
    if (enemy.targetPlayerId >= 0) {
        auto* lockedTarget = state.GetPlayerData(enemy.targetPlayerId);
        if (lockedTarget && !lockedTarget->isDead)
            return lockedTarget;
        // Target died, clear lock and search for new target
        enemy.targetPlayerId = -1;
        enemy.currentPath.clear();
        enemy.pathIndex = 0;
    }

    // Detection parameters (matches Unity client Enemy_Controller)
    const float viewDistance = 8.0f;
    const float viewAngle = 50.0f;

    // AI forward direction from rotY (0° = +Z in Unity convention)
    float rad = enemy.rotY * 3.14159f / 180.0f;
    float forwardX = std::sin(rad);
    float forwardZ = std::cos(rad);

    int bestId = -1;
    float nearestDist = viewDistance;

    auto& players = state.GetAllPlayerDatasMutable();
    for (auto& pair : players) {
        int pid = pair.first;
        auto& player = pair.second;
        if (player.isDead) continue;

        float dx = player.posX - enemy.posX;
        float dz = player.posZ - enemy.posZ;
        float dist = std::sqrt(dx * dx + dz * dz);

        if (dist >= viewDistance) continue;

        // Check if player is within view cone
        float toX = dx / dist;
        float toZ = dz / dist;
        float dot = forwardX * toX + forwardZ * toZ;
        float angle = std::acos(dot) * 180.0f / 3.14159f;

        if (angle <= viewAngle && dist < nearestDist) {
            nearestDist = dist;
            bestId = pid;
        }
    }

    if (bestId >= 0) {
        enemy.targetPlayerId = bestId;
        return state.GetPlayerData(bestId);
    }
    return nullptr;
}

void EnemyAI::MoveToward(float dt, EnemyData& enemy, float targetX, float targetZ, float speed) {
    float dx = targetX - enemy.posX;
    float dz = targetZ - enemy.posZ;
    float dist = std::sqrt(dx * dx + dz * dz);

    if (dist < 0.2f) {
        enemy.speed = 0;
        return;
    }

    dx /= dist;
    dz /= dist;

    enemy.posX += dx * speed * dt;
    enemy.posZ += dz * speed * dt;
    enemy.speed = speed;
    enemy.rotY = std::atan2(dx, dz) * 180.0f / 3.14159f;
}

void EnemyAI::FollowPath(float dt, EnemyData& enemy, float speed) {
    auto& path = enemy.currentPath;
    if (path.empty()) return;

    if (enemy.pathIndex >= (int)path.size()) {
        enemy.speed = 0;
        return;
    }

    Float2 node = path[enemy.pathIndex];
    float dx = node.x - enemy.posX;
    float dz = node.y - enemy.posZ;
    float dist = std::sqrt(dx * dx + dz * dz);

    // Advance to next waypoint when close enough
    if (dist < 0.3f) {
        enemy.pathIndex++;
        if (enemy.pathIndex >= (int)path.size()) {
            enemy.speed = 0;
            return;
        }
        node = path[enemy.pathIndex];
        dx = node.x - enemy.posX;
        dz = node.y - enemy.posZ;
        dist = std::sqrt(dx * dx + dz * dz);
    }

    if (dist > 0.01f) {
        dx /= dist;
        dz /= dist;
        float moveAmount = speed * dt;
        if (moveAmount > dist) moveAmount = dist;
        enemy.posX += dx * moveAmount;
        enemy.posZ += dz * moveAmount;
        enemy.speed = speed;

        // Only update rotation on meaningful movement — prevents jitter when nearly still
        if (moveAmount > 0.001f) {
            float newRotY = std::atan2(dx, dz) * 180.0f / 3.14159f;
            float rotDiff = newRotY - enemy.rotY;
            while (rotDiff > 180.0f) rotDiff -= 360.0f;
            while (rotDiff < -180.0f) rotDiff += 360.0f;
            if (std::abs(rotDiff) > 3.0f) {
                enemy.rotY = newRotY;
            }
        }
    }
}

void EnemyAI::UpdateEnemy(float dt, EnemyData& enemy, GameState& state, bool hasPlayers) {
    if (enemy.pendingDespawn) return;

    if (enemy.isDead) {
        enemy.speed = 0;
        enemy.state = 4;
        enemy.isAttack = false;
        // Keep dead enemies as corpses (don't despawn — needed for save/load)
        return;
    }

    PlayerData* target = FindPlayerInSight(enemy, state);

    if (target) {
        float dx = target->posX - enemy.posX;
        float dz = target->posZ - enemy.posZ;
        float dist = std::sqrt(dx * dx + dz * dz);

        // Server-authoritative attack damage (always runs, with or without host)
        if (enemy.attackTimer > 0) {
            enemy.state = 3;
            enemy.speed = 0;
            enemy.attackTimer -= dt;
            enemy.isAttack = (enemy.attackTimer > ENEMY_ATTACK_COOLDOWN - 0.15f);
            // rotY frozen during attack — enemy commits to strike direction

            // Compute facing angle for hit validation
            float angleToPlayer = std::atan2(dx, dz) * 180.0f / 3.14159f;
            float facingDiff = std::abs(angleToPlayer - enemy.rotY);
            while (facingDiff > 180.0f) facingDiff = 360.0f - facingDiff;

            float elapsed = ENEMY_ATTACK_COOLDOWN - enemy.attackTimer;
            if (!enemy.hit1Done && elapsed >= ENEMY_ATTACK_FIRST_HIT) {
                enemy.hit1Done = true;
                if (!target->isDead && dist <= ENEMY_ATTACK_RANGE &&
                    facingDiff <= ENEMY_ATTACK_FACE_ANGLE) {
                    target->hp -= ENEMY_ATTACK_DAMAGE;
                    if (target->hp <= 0) {
                        target->hp = 0;
                        target->isDead = true;
                        target->justDied = true;
                        target->deathTimer = RESPAWN_COOLDOWN;
                    }
                }
            }
            if (!enemy.hit2Done && elapsed >= ENEMY_ATTACK_SECOND_HIT) {
                enemy.hit2Done = true;
                if (!target->isDead && dist <= ENEMY_ATTACK_RANGE &&
                    facingDiff <= ENEMY_ATTACK_FACE_ANGLE) {
                    target->hp -= ENEMY_ATTACK_DAMAGE;
                    if (target->hp <= 0) {
                        target->hp = 0;
                        target->isDead = true;
                        target->justDied = true;
                        target->deathTimer = RESPAWN_COOLDOWN;
                    }
                }
            }

            if (enemy.attackTimer < 0) enemy.attackTimer = 0;
        } else if (dist <= ENEMY_ATTACK_RANGE) {
            // Entered attack range, begin attack (server-authoritative)
            enemy.state = 3;
            enemy.speed = 0;
            enemy.attackTimer = ENEMY_ATTACK_COOLDOWN;
            enemy.isAttack = true;
            enemy.hit1Done = false;
            enemy.hit2Done = false;
            enemy.rotY = std::atan2(dx, dz) * 180.0f / 3.14159f;
            enemy.currentPath.clear();
            enemy.pathIndex = 0;
        } else {
            // Server-authoritative A* chase
            enemy.state = 2;
            enemy.isAttack = false;

            if (!pf) {
                MoveToward(dt, enemy, target->posX, target->posZ, ENEMY_CHASE_SPEED);
            } else {
                // Check if player has moved significantly since last path calculation
                float tdx = target->posX - enemy.lastRepathTargetX;
                float tdz = target->posZ - enemy.lastRepathTargetZ;
                float targetMoved = std::sqrt(tdx * tdx + tdz * tdz);

                // Repath cooldown prevents rapid recalculation flicker
                enemy.repathCooldown -= dt;
                bool canRepath = enemy.repathCooldown <= 0;

                bool needRepath = enemy.currentPath.empty() ||
                    enemy.pathIndex >= (int)enemy.currentPath.size() ||
                    (targetMoved > 2.5f && canRepath);

                if (needRepath) {
                    auto path = pf->FindPath(enemy.posX, enemy.posZ,
                        target->posX, target->posZ);
                    if (!path.empty()) {
                        enemy.currentPath = path;
                        enemy.pathIndex = 0;
                        enemy.lastRepathTargetX = target->posX;
                        enemy.lastRepathTargetZ = target->posZ;
                        enemy.repathCooldown = 0.5f;
                    } else {
                        enemy.speed = 0;
                    }
                }

                // Follow path — movement + facing direction (natural walking)
                if (!enemy.currentPath.empty() &&
                    enemy.pathIndex < (int)enemy.currentPath.size()) {
                    FollowPath(dt, enemy, ENEMY_CHASE_SPEED);
                }

                // When close to player, face them directly for precise attack alignment
                if (dist < ENEMY_ATTACK_RANGE * 1.5f) {
                    enemy.rotY = std::atan2(dx, dz) * 180.0f / 3.14159f;
                }
            }
        }
    } else {
        // No player target

        // If mid-attack, let animation finish before transitioning to patrol
        if (enemy.state == 3 && enemy.attackTimer > 0) {
            enemy.speed = 0;
            enemy.attackTimer -= dt;
            if (enemy.attackTimer <= 0) {
                enemy.attackTimer = 0;
                enemy.isAttack = false;
                enemy.currentPath.clear();
                enemy.pathIndex = 0;
                enemy.originalPosition = { enemy.posX, enemy.posZ };
                enemy.patrolTimer = 0;
                enemy.state = 0;
            }
            return;
        }

        // Normal patrol below
        enemy.isAttack = false;
        enemy.attackTimer = 0;

        // Transition from chase: reset patrol state
        if (enemy.state != 0 && enemy.state != 1) {
            enemy.currentPath.clear();
            enemy.pathIndex = 0;
            enemy.originalPosition = { enemy.posX, enemy.posZ };
            enemy.patrolTimer = 0;
        }

        bool pathConsumed = enemy.pathIndex >= (int)enemy.currentPath.size();

        // Pick new random patrol destination when path consumed or no path
        if (pf && (enemy.currentPath.empty() || pathConsumed)) {
            enemy.patrolTimer -= dt;
            if (enemy.patrolTimer <= 0) {
                Int2 center = pf->WorldToGrid(enemy.posX, enemy.posZ);
                Int2 target = pf->GetRandomWalkable(center.x, center.y, 5);
                Float2 dest = pf->GridToWorld(target.x, target.y);
                auto path = pf->FindPath(enemy.posX, enemy.posZ, dest.x, dest.y);
                if (!path.empty()) {
                    enemy.currentPath = path;
                    enemy.pathIndex = 0;
                }
                // Wait 3-6 seconds before next random move
                enemy.patrolTimer = 3.0f + static_cast<float>(std::rand() % 3000) / 1000.0f;
            }
        }

        if (!enemy.currentPath.empty() && enemy.pathIndex < (int)enemy.currentPath.size()) {
            FollowPath(dt, enemy, ENEMY_WANDER_SPEED);
            enemy.state = 1;
        } else {
            enemy.speed = 0;
            enemy.state = 0;
        }
    }
}
