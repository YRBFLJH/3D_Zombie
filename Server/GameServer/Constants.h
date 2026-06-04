#pragma once
#include <chrono>

// 网络与时间
constexpr int SERVER_PORT = 8888;
constexpr int MAX_BUF_SIZE = 2048;
constexpr auto DT_PHYS = std::chrono::milliseconds(33);
constexpr auto DT_SEND = std::chrono::milliseconds(50);
constexpr auto DT_STATS = std::chrono::milliseconds(500);
constexpr auto CLIENT_TIMEOUT = std::chrono::seconds(15);

// 玩家移动速度
constexpr float WALK_SPEED = 3.0f;
constexpr float RUN_SPEED = 8.5f;
constexpr float MAX_SPEED_TOLERANCE = 1.2f;  // 防加速作弊的容差系数

// 玩家属性默认值
constexpr float DEFAULT_MAX_HP = 100.0f;
constexpr float DEFAULT_MAX_FOOD = 100.0f;
constexpr float DEFAULT_MAX_WATER = 100.0f;
constexpr float HP_REGEN_PER_SEC = 0.5f;
constexpr float FOOD_DEPLETE_PER_SEC = 0.2f;
constexpr float WATER_DEPLETE_PER_SEC = 0.3f;
constexpr float STARVATION_DMG_PER_SEC = 2.0f;
constexpr float DEHYDRATION_DMG_PER_SEC = 3.0f;

// 敌人默认属性
constexpr float DEFAULT_ENEMY_HP = 100.0f;
constexpr float ENEMY_DETECTION_RANGE = 30.0f;
constexpr float ENEMY_ATTACK_RANGE = 1.2f;
constexpr float ENEMY_ATTACK_COOLDOWN = 3.5f;
constexpr float ENEMY_DEATH_ANIM_DURATION = 5.0f;
constexpr float ENEMY_ATTACK_DAMAGE = 8.0f;
constexpr float ENEMY_ATTACK_FIRST_HIT = 0.3f;
constexpr float ENEMY_ATTACK_SECOND_HIT = 1.2f;
constexpr float ENEMY_ATTACK_FACE_ANGLE = 30.0f;
constexpr float ENEMY_CHASE_SPEED = 4.0f;
constexpr float ENEMY_WANDER_SPEED = 1.5f;

// Anti-cheat
constexpr float SHOOT_COOLDOWN = 0.25f;     // Minimum seconds between shots
constexpr float MAX_TELEPORT_DIST = 5.0f;   // Max allowed position change per PlayerTransformSync

// 世界边界
constexpr float WORLD_MIN_X = -100.0f;
constexpr float WORLD_MAX_X = 100.0f;
constexpr float WORLD_MIN_Z = -100.0f;
constexpr float WORLD_MAX_Z = 100.0f;

// 重生
constexpr float RESPAWN_COOLDOWN = 5.0f;
constexpr float DEFAULT_RESPAWN_Y = 1.0f;

// 账号文件
constexpr const char* ACCOUNTS_FILE = "saves/accounts.dat";
