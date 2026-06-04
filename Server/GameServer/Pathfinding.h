#pragma once
#include <vector>

struct Int2 {
    int x, y;
    Int2() : x(0), y(0) {}
    Int2(int a, int b) : x(a), y(b) {}
    bool operator==(const Int2& o) const { return x == o.x && y == o.y; }
};

struct Float2 {
    float x, y;
    Float2() : x(0), y(0) {}
    Float2(float a, float b) : x(a), y(b) {}
};

struct PathNode {
    int gridX, gridY;
    float gCost, hCost;
    float FCost() const { return gCost + hCost; }
    PathNode* parent;
    bool walkable;
    bool inOpen;
    bool inClosed;

    PathNode() : parent(nullptr), walkable(true), inOpen(false), inClosed(false),
                 gridX(0), gridY(0), gCost(0), hCost(0) {}
};

class Pathfinding {
public:
    void Init(int gridSizeX, int gridSizeZ, float cellSize);
    std::vector<Float2> FindPath(float startX, float startZ, float endX, float endZ);
    Int2 WorldToGrid(float worldX, float worldZ) const;
    Float2 GridToWorld(int gridX, int gridY) const;
    void SetWalkable(int gridX, int gridY, bool walkable);
    Int2 FindNearestWalkable(int gridX, int gridY, int searchRadius = 5) const;
    bool IsWorldWalkable(float worldX, float worldZ) const;
    Int2 GetRandomWalkable(int centerGridX, int centerGridY, int radius);

private:
    int gridSizeX;
    int gridSizeZ;
    float cellSize;
    std::vector<PathNode> grid;

    PathNode& GetNode(int x, int y);
    const PathNode& GetNode(int x, int y) const;
    bool IsInBounds(int x, int y) const;
    float Heuristic(int x1, int y1, int x2, int y2) const;
    std::vector<Float2> SmoothPath(const std::vector<Float2>& path);
    bool LineOfSight(const Float2& from, const Float2& to);
};
