#include "HitValidation.h"
#include <cmath>

bool HitValidation::RaySphereIntersect(float ox, float oy, float oz,
                                        float dx, float dy, float dz,
                                        float cx, float cy, float cz,
                                        float r, float& t) {
    float ocx = ox - cx;
    float ocy = oy - cy;
    float ocz = oz - cz;

    float b = 2.0f * (dx * ocx + dy * ocy + dz * ocz);
    float c = ocx * ocx + ocy * ocy + ocz * ocz - r * r;

    float discriminant = b * b - 4.0f * c;
    if (discriminant < 0) return false;

    float sqrtD = std::sqrt(discriminant);
    float t0 = (-b - sqrtD) * 0.5f;
    float t1 = (-b + sqrtD) * 0.5f;

    if (t0 >= 0) { t = t0; return true; }
    if (t1 >= 0) { t = t1; return true; }
    return false;
}

HitCheckResult HitValidation::CheckShot(float ox, float oy, float oz,
                                          float dx, float dy, float dz,
                                          float maxRange,
                                          const std::vector<EnemyHitData>& enemies) {
    HitCheckResult best;
    best.distance = maxRange;

    for (auto& e : enemies) {
        if (e.isDead) continue;
        float t;
        if (RaySphereIntersect(ox, oy, oz, dx, dy, dz,
                                e.posX, e.posY, e.posZ, e.radius, t)) {
            if (t < best.distance) {
                best.hit = true;
                best.enemyId = e.enemyId;
                best.distance = t;
            }
        }
    }
    return best;
}
