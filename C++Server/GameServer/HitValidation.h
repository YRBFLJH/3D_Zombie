#pragma once
#include <vector>
#include "Constants.h"

struct EnemyHitData {
    int enemyId;
    float posX, posY, posZ;
    float radius;
    bool isDead;
};

struct HitCheckResult {
    bool hit = false;
    int enemyId = -1;
    float distance = 0;
};

class HitValidation {
public:
    // 射线 vs 所有敌人球体检测
    HitCheckResult CheckShot(float originX, float originY, float originZ,
                             float dirX, float dirY, float dirZ,
                             float maxRange,
                             const std::vector<EnemyHitData>& enemies);

private:
    // 射线 vs 球体 相交测试
    bool RaySphereIntersect(float rayOriginX, float rayOriginY, float rayOriginZ,
                            float rayDirX, float rayDirY, float rayDirZ,
                            float sphereCenterX, float sphereCenterY, float sphereCenterZ,
                            float sphereRadius, float& t);
};
