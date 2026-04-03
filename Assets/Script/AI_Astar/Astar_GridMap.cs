using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

// 网格地图转换的全局单例脚本
public class Astar_GridMap : MonoBehaviour
{
    public static Astar_GridMap instance;

    // 地图大小
    int cellSize = 1; 
    public int mapWidth = 200;
    public int mapHeight = 200;

    public LayerMask Obstacle; // 障碍的Layer层（手动新建，并将不可走的障碍物设为此层），只用遍历此层找障碍物，不用全场景检测，优化性能

    public Astar_Node[,] gridMap; // 以每个节组成的网格地图

    void Awake()
    {
        instance = this;
        InitGridMap();
    }

    // 初始化网格地图
    void InitGridMap()
    {
        gridMap = new Astar_Node[mapWidth, mapHeight];

        // 原点坐标（地图某个角落点），一般设置此脚本为地图原点(0,0,0)
        Vector3 startPos = transform.position - new Vector3(mapWidth * cellSize / 2, 0, mapHeight * cellSize / 2);

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Vector3 worldPos = startPos + new Vector3(x * cellSize + cellSize * 0.5f, 1f, y * cellSize + cellSize * 0.5f);

                bool walkable = !(Physics.OverlapBox(worldPos, new Vector3(cellSize * 0.4f, 1f, cellSize * 0.4f), Quaternion.identity, Obstacle).Length > 0);

                gridMap[x, y] = new Astar_Node()
                {
                    walkable = walkable,
                    worldPos = worldPos,
                    gridX = x,
                    gridY = y
                };
            }
        }
    }

    // 世界坐标转网格坐标(起点、终点),实际调用:WorldToWalkableNode
    public Astar_Node WorldToNode(Vector3 worldPos)
    {
        Vector3 localPos = worldPos - transform.position;

        float percentX = (localPos.x + mapWidth * cellSize * 0.5f) / (mapWidth * cellSize);
        float percentY = (localPos.z + mapHeight * cellSize * 0.5f) / (mapHeight * cellSize);

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.FloorToInt(percentX * mapWidth);
        int y = Mathf.FloorToInt(percentY * mapHeight);

        x = Mathf.Clamp(x, 0, mapWidth - 1);
        y = Mathf.Clamp(y, 0, mapHeight - 1);

        return gridMap[x, y];
    }

    public List<Astar_Node> GetNeighbors(Astar_Node node)
    {
        List<Astar_Node> list = new List<Astar_Node>();

        int x = node.gridX;
        int y = node.gridY;

        // 获取当前节点的相邻节点(符合边界限制的相邻节点)[4个直的、4个斜的]
        if (x + 1 < mapWidth && y + 1 < mapHeight) list.Add(gridMap[x + 1, y + 1]);
        if (x - 1 >= 0 && y - 1 >= 0) list.Add(gridMap[x - 1, y - 1]);
        if (x + 1 < mapWidth && y - 1 >= 0) list.Add(gridMap[x + 1, y - 1]);
        if (x - 1 >= 0 && y + 1 < mapHeight) list.Add(gridMap[x - 1, y + 1]);

        if (x + 1 < mapWidth) list.Add(gridMap[x + 1, y]);
        if (x - 1 >= 0) list.Add(gridMap[x - 1, y]);
        if (y + 1 < mapHeight) list.Add(gridMap[x, y + 1]);
        if (y - 1 >= 0) list.Add(gridMap[x, y - 1]);

        return list;
    }

    // 重置节点
    public void ResetNodes()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Astar_Node node = gridMap[x, y];
                node.G = 0;
                node.H = 0;
                node.parent = null;
            }
        }
    }

    // Debug绘制网格地图查看不可走、可走格子区域
    private void OnDrawGizmos()
    {
        if (gridMap == null) return;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Astar_Node node = gridMap[x, y];

                // 可走 → 绿色
                if (node.walkable)
                {
                    Gizmos.color = new Color(0, 1, 0, 0.2f); 
                }
                // 不可走 → 红色（障碍）
                else
                {
                    Gizmos.color = new Color(1, 0, 0, 0.7f);
                }

                // 画出格子（稍微小一点，不挡视线）
                Gizmos.DrawCube(node.worldPos, new Vector3(cellSize - 0.1f, 0.2f, cellSize - 0.1f));
            }
        }
    }


    // 获取当前节点的附近可走节点(当玩家在不可行走地方时，会自动找一个最近可走节点)
    public Astar_Node GetNearestWalkableNode(Astar_Node node)
    {
        if (node.walkable) return node;

        for (int radius = 1; radius < 3; radius++) // 设搜索半径为3，依次搜索周围格子
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                        continue;

                    int checkX = node.gridX + x;
                    int checkY = node.gridY + y;

                    if (checkX >= 0 && checkY >= 0 && checkX < mapWidth && checkY < mapHeight)
                    {
                        Astar_Node checkNode = gridMap[checkX, checkY];
                        if (checkNode.walkable)
                        {
                            return checkNode;
                        }
                    }
                }
            }
        }
        return null; // 实在找不到可走节点
    }
    
    public Astar_Node WorldToWalkableNode(Vector3 worldPos)
    {
        Astar_Node node = WorldToNode(worldPos);
        return GetNearestWalkableNode(node);
    }
}
