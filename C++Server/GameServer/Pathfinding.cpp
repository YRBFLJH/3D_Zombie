#include "Pathfinding.h"
#include <cmath>
#include <cstdlib>
#include <algorithm>
#include <vector>
#include <limits>

void Pathfinding::Init(int gsx, int gsz, float cs) {
    gridSizeX = gsx;
    gridSizeZ = gsz;
    cellSize = cs;
    grid.resize(gridSizeX * gridSizeZ);
    for (int z = 0; z < gridSizeZ; z++) {
        for (int x = 0; x < gridSizeX; x++) {
            auto& node = GetNode(x, z);
            node.gridX = x;
            node.gridY = z;
            node.walkable = true;
        }
    }
}

PathNode& Pathfinding::GetNode(int x, int y) {
    return grid[y * gridSizeX + x];
}

const PathNode& Pathfinding::GetNode(int x, int y) const {
    return grid[y * gridSizeX + x];
}

bool Pathfinding::IsInBounds(int x, int y) const {
    return x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeZ;
}

Int2 Pathfinding::WorldToGrid(float worldX, float worldZ) const {
    return Int2(
        static_cast<int>(worldX / cellSize + gridSizeX / 2),
        static_cast<int>(worldZ / cellSize + gridSizeZ / 2)
    );
}

Float2 Pathfinding::GridToWorld(int gridX, int gridY) const {
    return Float2(
        (gridX - gridSizeX / 2) * cellSize + cellSize * 0.5f,
        (gridY - gridSizeZ / 2) * cellSize + cellSize * 0.5f
    );
}

void Pathfinding::SetWalkable(int gridX, int gridY, bool walkable) {
    if (IsInBounds(gridX, gridY)) {
        GetNode(gridX, gridY).walkable = walkable;
    }
}

bool Pathfinding::IsWorldWalkable(float worldX, float worldZ) const {
    Int2 g = WorldToGrid(worldX, worldZ);
    return IsInBounds(g.x, g.y) && GetNode(g.x, g.y).walkable;
}

Int2 Pathfinding::FindNearestWalkable(int gridX, int gridY, int searchRadius) const {
    if (IsInBounds(gridX, gridY) && GetNode(gridX, gridY).walkable)
        return Int2(gridX, gridY);

    for (int r = 1; r <= searchRadius; r++) {
        for (int dx = -r; dx <= r; dx++) {
            for (int dy = -r; dy <= r; dy++) {
                if (std::abs(dx) != r && std::abs(dy) != r) continue;
                int nx = gridX + dx;
                int ny = gridY + dy;
                if (IsInBounds(nx, ny) && GetNode(nx, ny).walkable)
                    return Int2(nx, ny);
            }
        }
    }
    return Int2(gridX, gridY);
}

float Pathfinding::Heuristic(int x1, int y1, int x2, int y2) const {
    int dx = std::abs(x1 - x2);
    int dy = std::abs(y1 - y2);
    if (dx > dy) return 1.0f * dx + 0.414f * dy;
    return 1.0f * dy + 0.414f * dx;
}

std::vector<Float2> Pathfinding::FindPath(float startX, float startZ,
                                             float endX, float endZ) {
    Int2 startGrid = WorldToGrid(startX, startZ);
    Int2 endGrid = WorldToGrid(endX, endZ);

    for (auto& n : grid) {
        n.gCost = 0;
        n.hCost = 0;
        n.parent = nullptr;
        n.inOpen = false;
        n.inClosed = false;
    }

    if (!IsInBounds(startGrid.x, startGrid.y) || !IsInBounds(endGrid.x, endGrid.y))
        return {};

    // Snap unwalkable start/end to nearest walkable cell
    if (!GetNode(startGrid.x, startGrid.y).walkable) {
        startGrid = FindNearestWalkable(startGrid.x, startGrid.y);
        if (!GetNode(startGrid.x, startGrid.y).walkable) return {};
    }
    bool endSnapped = false;
    if (!GetNode(endGrid.x, endGrid.y).walkable) {
        endGrid = FindNearestWalkable(endGrid.x, endGrid.y);
        if (!GetNode(endGrid.x, endGrid.y).walkable) return {};
        endSnapped = true;
    }

    PathNode& startNode = GetNode(startGrid.x, startGrid.y);
    PathNode& endNode = GetNode(endGrid.x, endGrid.y);

    std::vector<PathNode*> openList;
    openList.push_back(&startNode);
    startNode.inOpen = true;
    startNode.hCost = Heuristic(startGrid.x, startGrid.y, endGrid.x, endGrid.y);

    const int dirs[8][2] = {
        {-1, 0}, {1, 0}, {0, -1}, {0, 1},
        {-1, -1}, {-1, 1}, {1, -1}, {1, 1}
    };

    while (!openList.empty()) {
        size_t bestIdx = 0;
        for (size_t i = 1; i < openList.size(); i++) {
            if (openList[i]->FCost() < openList[bestIdx]->FCost())
                bestIdx = i;
        }

        PathNode* current = openList[bestIdx];

        if (current->gridX == endGrid.x && current->gridY == endGrid.y) {
            std::vector<Float2> path;
            // Use exact end position (not cell center) when end wasn't snapped,
            // so SmoothPath visibility checks use the real target position
            if (!endSnapped) {
                path.push_back(Float2(endX, endZ));
            } else {
                path.push_back(GridToWorld(current->gridX, current->gridY));
            }
            PathNode* trace = current->parent;
            while (trace) {
                path.push_back(GridToWorld(trace->gridX, trace->gridY));
                trace = trace->parent;
            }
            std::reverse(path.begin(), path.end());
            path = SmoothPath(path);
            return path;
        }

        openList.erase(openList.begin() + bestIdx);
        current->inOpen = false;
        current->inClosed = true;

        for (int d = 0; d < 8; d++) {
            int nx = current->gridX + dirs[d][0];
            int ny = current->gridY + dirs[d][1];

            if (!IsInBounds(nx, ny)) continue;
            PathNode& neighbor = GetNode(nx, ny);
            if (!neighbor.walkable || neighbor.inClosed) continue;

            bool isDiagonal = (dirs[d][0] != 0 && dirs[d][1] != 0);
            float moveCost = isDiagonal ? 1.414f : 1.0f;
            if (isDiagonal) {
                if (!GetNode(current->gridX + dirs[d][0], current->gridY).walkable) continue;
                if (!GetNode(current->gridX, current->gridY + dirs[d][1]).walkable) continue;
            }

            float newGCost = current->gCost + moveCost;

            if (!neighbor.inOpen || newGCost < neighbor.gCost) {
                neighbor.gCost = newGCost;
                neighbor.hCost = Heuristic(nx, ny, endGrid.x, endGrid.y);
                neighbor.parent = current;

                if (!neighbor.inOpen) {
                    openList.push_back(&neighbor);
                    neighbor.inOpen = true;
                }
            }
        }
    }

    return {};
}

std::vector<Float2> Pathfinding::SmoothPath(const std::vector<Float2>& path) {
    if (path.size() <= 2) return path;

    std::vector<Float2> smoothed;
    smoothed.push_back(path[0]);

    size_t currentIdx = 0;
    while (currentIdx < path.size() - 1) {
        size_t furthest = path.size() - 1;
        while (furthest > currentIdx + 1) {
            if (LineOfSight(path[currentIdx], path[furthest]))
                break;
            furthest--;
        }
        smoothed.push_back(path[furthest]);
        currentIdx = furthest;
    }

    return smoothed;
}

bool Pathfinding::LineOfSight(const Float2& from, const Float2& to) {
    float dx = to.x - from.x;
    float dy = to.y - from.y;
    float dist = std::sqrt(dx * dx + dy * dy);
    if (dist < cellSize) return true;

    // Fine step for dense sampling along the line
    float step = cellSize * 0.2f;
    int steps = static_cast<int>(dist / step);
    if (steps < 1) steps = 1;

    // Clearance radius: the line is treated as "thick", preventing corner cutting
    float clearance = cellSize * 0.55f;

    for (int i = 1; i <= steps; i++) {
        float t = static_cast<float>(i) / static_cast<float>(steps);
        float cx = from.x + dx * t;
        float cy = from.y + dy * t;
        Int2 g = WorldToGrid(cx, cy);

        // Check a 3x3 area around the sample point for unwalkable cells within clearance
        for (int dx2 = -1; dx2 <= 1; dx2++) {
            for (int dy2 = -1; dy2 <= 1; dy2++) {
                int nx = g.x + dx2;
                int ny = g.y + dy2;
                if (!IsInBounds(nx, ny)) continue;
                if (GetNode(nx, ny).walkable) continue;

                Float2 cellCenter = GridToWorld(nx, ny);
                float cdx = cx - cellCenter.x;
                float cdy = cy - cellCenter.y;
                if (std::sqrt(cdx * cdx + cdy * cdy) < clearance)
                    return false;
            }
        }
    }
    return true;
}

Int2 Pathfinding::GetRandomWalkable(int centerGridX, int centerGridY, int radius) {
    for (int attempt = 0; attempt < 20; attempt++) {
        int dx = (std::rand() % (radius * 2 + 1)) - radius;
        int dy = (std::rand() % (radius * 2 + 1)) - radius;
        int nx = centerGridX + dx;
        int ny = centerGridY + dy;
        if (IsInBounds(nx, ny) && GetNode(nx, ny).walkable)
            return Int2(nx, ny);
    }
    return Int2(centerGridX, centerGridY);
}
