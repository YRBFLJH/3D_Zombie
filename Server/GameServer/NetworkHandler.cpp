#include "NetworkHandler.h"
#include "HitValidation.h"
#include <iostream>
#include <string>

using namespace boost::asio;
using ip::udp;

NetworkHandler::NetworkHandler(GameState& gs, udp::socket& s)
    : state(gs), sock(s) {}

// ---------- 消息分发 ----------

bool NetworkHandler::ProcessMessage(const uint8_t* data, size_t len, const udp::endpoint& sender) {
    game::GameMessage msg;
    if (!msg.ParseFromArray(data, static_cast<int>(len))) return false;

    switch (msg.payload_case()) {
    case game::GameMessage::kJoinRequest:
        HandleJoinRequest(sender);
        break;
    case game::GameMessage::kLeaveRequest:
        HandleLeaveRequest(sender);
        break;
    case game::GameMessage::kInput:
        HandleInput(msg, sender);
        break;
    case game::GameMessage::kEnemyStateSyncBatch:
        HandleEnemyStateSyncBatch(msg);
        break;
    case game::GameMessage::kEnemyStateSync:
        HandleEnemyStateSync(msg);
        break;
    case game::GameMessage::kPlayerTransformSync:
        HandlePlayerTransformSync(msg);
        break;
    case game::GameMessage::kShootRequest:
        HandleShootRequest(msg);
        break;
    case game::GameMessage::kPlayerRespawn:
        HandlePlayerRespawn(msg);
        break;
    default:
        return false;
    }
    return true;
}

// ---------- 单播 ----------

void NetworkHandler::SendTo(const udp::endpoint& target, const game::GameMessage& msg) {
    std::string data;
    msg.SerializeToString(&data);
    sock.send_to(buffer(data), target, 0, ec);
}

void NetworkHandler::SendAssignId(const udp::endpoint& target, int id, bool isHost) {
    game::GameMessage rep;
    rep.mutable_assign_id()->set_id(id);
    rep.mutable_assign_id()->set_ishost(isHost);
    SendTo(target, rep);
}

// ---------- 广播 ----------

void NetworkHandler::BroadcastToAll(const game::GameMessage& msg, int excludeId) {
    std::string data;
    msg.SerializeToString(&data);
    for (auto& c : state.GetClients()) {
        if (c.first != excludeId) {
            sock.send_to(buffer(data), c.second, 0, ec);
        }
    }
}

void NetworkHandler::BroadcastWorldState() {
    game::GameMessage msg;
    auto worldState = msg.mutable_world_state();

    for (auto& p : state.GetAllPlayerDatas()) {
        int id = p.first;
        auto& s = p.second;
        auto ps = worldState->add_players();
        ps->set_id(id);
        ps->set_posx(s.posX);
        ps->set_posy(s.posY);
        ps->set_posz(s.posZ);
        ps->set_roty(s.rotY);
        ps->set_speed(s.speed);
        ps->set_isrunning(s.isRunning);
        ps->set_isaiming(s.isAiming);
    }

    for (auto& e : state.GetAllEnemies()) {
        auto& enemy = e.second;
        auto es = worldState->add_enemies();
        es->set_enemyid(enemy.enemyId);
        es->set_posx(enemy.posX);
        es->set_posy(enemy.posY);
        es->set_posz(enemy.posZ);
        es->set_roty(enemy.rotY);
        es->set_speed(enemy.speed);
        es->set_state(enemy.state);
        es->set_isdead(enemy.isDead);
        es->set_isattack(enemy.isAttack);
    }

    BroadcastToAll(msg);
}

void NetworkHandler::BroadcastPlayerStats() {
    game::GameMessage msg;
    for (auto& p : state.GetAllPlayerDatas()) {
        int id = p.first;
        auto& s = p.second;
        auto stats = msg.mutable_player_stats_sync();
        stats->set_id(id);
        stats->set_hp(s.hp);
        stats->set_maxhp(s.maxHp);
        stats->set_food(s.food);
        stats->set_water(s.water);
        stats->set_isdead(s.isDead);
        std::string data;
        msg.SerializeToString(&data);
        auto it = state.GetClients().find(id);
        if (it != state.GetClients().end()) {
            sock.send_to(buffer(data), it->second, 0, ec);
        }
        msg.Clear();
    }
}

void NetworkHandler::BroadcastHitResult(const game::HitResult& hit) {
    game::GameMessage msg;
    *msg.mutable_hit_result() = hit;
    BroadcastToAll(msg);
}

void NetworkHandler::BroadcastEnemySpawn(int enemyId, int enemyType, float x, float y, float z, float rotY) {
    game::GameMessage msg;
    auto es = msg.mutable_enemy_spawn();
    es->set_enemyid(enemyId);
    es->set_enemytype(enemyType);
    es->set_posx(x);
    es->set_posy(y);
    es->set_posz(z);
    es->set_roty(rotY);
    BroadcastToAll(msg);
}

void NetworkHandler::BroadcastEnemyDespawn(int enemyId, int reason) {
    game::GameMessage msg;
    auto ed = msg.mutable_enemy_despawn();
    ed->set_enemyid(enemyId);
    ed->set_reason(reason);
    BroadcastToAll(msg);
}

void NetworkHandler::BroadcastPlayerDeath(int playerId) {
    game::GameMessage msg;
    msg.mutable_player_death()->set_playerid(playerId);
    BroadcastToAll(msg);
}

void NetworkHandler::BroadcastPlayerRespawn(int playerId, float x, float y, float z) {
    game::GameMessage msg;
    auto pr = msg.mutable_player_respawn();
    pr->set_playerid(playerId);
    pr->set_posx(x);
    pr->set_posy(y);
    pr->set_posz(z);
    BroadcastToAll(msg);
}

void NetworkHandler::BroadcastHostNotify(int hostId) {
    game::GameMessage msg;
    msg.mutable_host_notify()->set_hostid(hostId);
    BroadcastToAll(msg);
}

// ---------- 消息处理 ----------

void NetworkHandler::HandleJoinRequest(const udp::endpoint& sender) {
    int id = state.AddClient(sender);
    SendAssignId(sender, id, state.GetHostId() == id);
    std::cout << "客户端加入: " << id << std::endl;
}

void NetworkHandler::HandleLeaveRequest(const udp::endpoint& sender) {
    int rid = -1;
    for (auto& p : state.GetClients()) {
        if (p.second == sender) { rid = p.first; break; }
    }
    if (rid == -1) return;

    state.UpdateHostAfterRemove(rid);
    state.RemoveClient(rid);

    if (state.HasHost()) {
        BroadcastHostNotify(state.GetHostId());
    }
    std::cout << "客户端离开: " << rid << std::endl;
}

void NetworkHandler::HandleInput(const game::GameMessage& msg, const udp::endpoint& sender) {
    auto& i = msg.input();
    int id = i.id();
    if (!state.HasClient(id)) return;

    state.TouchClient(id);
    auto* input = state.GetPlayerInput(id);
    auto* ps = state.GetPlayerData(id);
    if (!input || !ps || ps->isDead) return;

    // Clamp input to valid range
    float mx = i.movex();
    float mz = i.movez();
    if (mx < -1.0f) mx = -1.0f; else if (mx > 1.0f) mx = 1.0f;
    if (mz < -1.0f) mz = -1.0f; else if (mz > 1.0f) mz = 1.0f;

    input->moveX = mx;
    input->moveZ = mz;
    input->rotY = i.roty();
    input->running = i.running();
    input->aiming = i.aiming();

    ps->rotY = i.roty();
    ps->isRunning = i.running();
    ps->isAiming = i.aiming();
}

void NetworkHandler::HandleEnemyStateSyncBatch(const game::GameMessage& msg) {
    auto& batch = msg.enemy_state_sync_batch();
    for (auto& syncEnemy : batch.enemy_states()) {
        int enemyId = syncEnemy.enemyid();
        auto* es = state.GetEnemy(enemyId);
        if (!es) continue;
        es->posX = syncEnemy.posx();
        es->posY = syncEnemy.posy();
        es->posZ = syncEnemy.posz();
        es->rotY = syncEnemy.roty();
        es->speed = syncEnemy.speed();
        es->state = syncEnemy.state();
        es->isDead = syncEnemy.isdead();
        es->isAttack = syncEnemy.isattack();
    }
}

void NetworkHandler::HandleEnemyStateSync(const game::GameMessage& msg) {
    auto& syncEnemy = msg.enemy_state_sync();
    int enemyId = syncEnemy.enemyid();
    auto* es = state.GetEnemy(enemyId);
    if (!es) return;
    es->posX = syncEnemy.posx();
    es->posY = syncEnemy.posy();
    es->posZ = syncEnemy.posz();
    es->rotY = syncEnemy.roty();
    es->speed = syncEnemy.speed();
    es->state = syncEnemy.state();
    es->isDead = syncEnemy.isdead();
    es->isAttack = syncEnemy.isattack();
}

void NetworkHandler::HandlePlayerTransformSync(const game::GameMessage& msg) {
    auto& t = msg.player_transform_sync();
    int id = t.id();
    if (!state.HasClient(id)) return;
    state.TouchClient(id);
    auto* ps = state.GetPlayerData(id);
    if (!ps) return;

    float newX = t.posx();
    float newY = t.posy();
    float newZ = t.posz();

    // Teleport detection: reject unreasonable position jumps
    float dx = newX - ps->posX;
    float dz = newZ - ps->posZ;
    float jumpDist = std::sqrt(dx * dx + dz * dz);
    if (jumpDist > MAX_TELEPORT_DIST) {
        std::cout << "Teleport rejected: player=" << id << " dist=" << jumpDist << std::endl;
        return;
    }

    // Clamp to world bounds
    if (newX < WORLD_MIN_X) newX = WORLD_MIN_X;
    if (newX > WORLD_MAX_X) newX = WORLD_MAX_X;
    if (newZ < WORLD_MIN_Z) newZ = WORLD_MIN_Z;
    if (newZ > WORLD_MAX_Z) newZ = WORLD_MAX_Z;

    ps->posX = newX;
    ps->posY = newY;
    ps->posZ = newZ;
    ps->rotY = t.roty();
    ps->speed = t.speed();
    ps->isRunning = t.running();
    ps->isAiming = t.aiming();
}

void NetworkHandler::HandleShootRequest(const game::GameMessage& msg) {
    auto& sr = msg.shoot_request();
    int shooterId = sr.shooterid();

    // Rate limiting
    auto now = std::chrono::steady_clock::now();
    auto it = lastShootTime.find(shooterId);
    if (it != lastShootTime.end()) {
        float elapsed = std::chrono::duration<float>(now - it->second).count();
        if (elapsed < SHOOT_COOLDOWN) {
            std::cout << "Shoot rate limited: player=" << shooterId << std::endl;
            return;
        }
    }
    lastShootTime[shooterId] = now;

    std::cout << "Shoot request: player=" << shooterId << std::endl;

    // 构建敌人数据列表用于命中检测
    std::vector<EnemyHitData> enemyList;
    for (auto& e : state.GetAllEnemies()) {
        auto& enemy = e.second;
        EnemyHitData ed;
        ed.enemyId = enemy.enemyId;
        ed.posX = enemy.posX;
        ed.posY = enemy.posY;
        ed.posZ = enemy.posZ;
        ed.radius = 1.0f;  // 敌人碰撞半径
        ed.isDead = enemy.isDead;
        enemyList.push_back(ed);
    }

    // 射线命中检测
    HitValidation hv;
    float dirX = sr.dirx();
    float dirY = sr.diry();
    float dirZ = sr.dirz();
    // 归一化射线方向
    float dirLen = std::sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ);
    if (dirLen > 0.001f) {
        dirX /= dirLen;
        dirY /= dirLen;
        dirZ /= dirLen;
    }

    HitCheckResult result = hv.CheckShot(
        sr.fireposx(), sr.fireposy(), sr.fireposz(),
        dirX, dirY, dirZ,
        200.0f,  // 最大射程
        enemyList
    );

    // 广播命中结果
    game::HitResult hitResult;
    hitResult.set_attackerid(shooterId);

    if (result.hit) {
        float damage = 25.0f;  // 基础伤害
        state.ApplyEnemyDamage(result.enemyId, damage);

        auto* enemy = state.GetEnemy(result.enemyId);
        float remainingHP = enemy ? enemy->hp : 0;

        hitResult.set_targettype(0);  // 敌人
        hitResult.set_targetid(result.enemyId);
        hitResult.set_damage(damage);
        hitResult.set_remaininghp(remainingHP);

        std::cout << "命中敌人: " << result.enemyId
                  << " 伤害: " << damage
                  << " 剩余HP: " << remainingHP << std::endl;
    } else {
        hitResult.set_targettype(0);
        hitResult.set_targetid(-1);
        hitResult.set_damage(0);
        hitResult.set_remaininghp(0);
        std::cout << "未命中" << std::endl;
    }

    BroadcastHitResult(hitResult);

    // 同时转发 ShootEvent 给其他客户端播放开火特效
    game::GameMessage shootEvent;
    auto se = shootEvent.mutable_shoot_event();
    se->set_shooterid(shooterId);
    se->set_fireposx(sr.fireposx());
    se->set_fireposy(sr.fireposy());
    se->set_fireposz(sr.fireposz());
    se->set_dirx(sr.dirx());
    se->set_diry(sr.diry());
    se->set_dirz(sr.dirz());
    BroadcastToAll(shootEvent, shooterId);  // 排除射击者本人
}

void NetworkHandler::HandlePlayerRespawn(const game::GameMessage& msg) {
    auto& pr = msg.player_respawn();
    int playerId = pr.playerid();
    auto* ps = state.GetPlayerData(playerId);
    if (!ps || !ps->isDead) return;

    ps->hp = ps->maxHp;
    ps->food = DEFAULT_MAX_FOOD;
    ps->water = DEFAULT_MAX_WATER;
    ps->isDead = false;
    ps->deathTimer = 0;

    BroadcastPlayerRespawn(playerId, pr.posx(), pr.posy(), pr.posz());
    std::cout << "玩家重生: " << playerId << std::endl;
}
