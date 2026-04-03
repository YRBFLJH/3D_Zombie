using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A*算法寻找路径
public class Astar_FindPath
{
    public static List<Vector3> FindPath(Astar_Node startPos, Astar_Node endPos)
    {
        if (!startPos.walkable || !endPos.walkable) return null; 

        Astar_GridMap.instance.ResetNodes();

        // 0.将起点加入开放列表计算，因只有一个点即跳过
        // 1.将相邻节点放入开放列表中 
        // 2.遍历计算找出符合公式的那个节点
        // 3.将符合公式的节点移除开放列表、放入关闭列表中 
        // 4.再以此节点为起点循环
        List<Astar_Node> openList = new List<Astar_Node>(); // 开放列表（时间复杂度O(n)）
        HashSet<Astar_Node> closeList = new HashSet<Astar_Node>(); // 关闭列表(用HashSet存储,时间复杂度O(1), 快速查找，性能更佳)

        openList.Add(startPos);

        while (openList.Count > 0)
        {
            // 1.遍历计算找出符合公式的那个节点
            Astar_Node node = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].F < node.F || (openList[i].F == node.F && openList[i].H < node.H))
                {
                    node = openList[i];
                }
            }

            // 2.将符合公式的节点移除开放列表、加入关闭列表中
            openList.Remove(node);
            closeList.Add(node);

            // 3.如果是终点，则返回路径
            if (node == endPos)
            {
                List<Vector3> path = new List<Vector3>();
                Astar_Node temp = node;

                while (temp != startPos)
                {
                    path.Add(temp.worldPos);
                    temp = temp.parent;
                }
                path.Reverse(); // 反转
                return path;  // 返回路径退出循环
            }

            // 4.遍历相邻节点加入开放列表
            foreach (Astar_Node neighbor in Astar_GridMap.instance.GetNeighbors(node))
            {
                if (!neighbor.walkable || closeList.Contains(neighbor)) continue;

                int tempG = node.G + GetGDistance(node,neighbor);
                
                if (!openList.Contains(neighbor) || tempG < neighbor.G)
                {
                    neighbor.G = tempG;
                    neighbor.H = DiagonalDistance(neighbor, endPos);
                    neighbor.parent = node;

                    if (!openList.Contains(neighbor)) openList.Add(neighbor);
                }
            }
        }
        return null;
    }

    // 计算G的距离（代价）
    static int GetGDistance(Astar_Node a, Astar_Node b)
    {
        int dx = Mathf.Abs(a.gridX - b.gridX);
        int dy = Mathf.Abs(a.gridY - b.gridY);
        if (dx > dy)
            return 14 * dy + 10 * (dx - dy);
        return 14 * dx + 10 * (dy - dx);
    }


    #region 启发式函数
    // 曼哈顿距离（四个方向直走【2D】）
    static int ManhattanDistance(Astar_Node a, Astar_Node b)
    {
        int dx = Mathf.Abs(a.gridX - b.gridX);
        int dy = Mathf.Abs(a.gridY - b.gridY);
        return dx + dy;
    }

    // 欧几里得距离
    static int EuclideanDistance(Astar_Node a, Astar_Node b)
    {
        int dx = Mathf.Abs(a.gridX - b.gridX);
        int dy = Mathf.Abs(a.gridY - b.gridY);
        return Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dy * dy) * 10);
    }

    // 对角线距离
    static int DiagonalDistance(Astar_Node a, Astar_Node b)
    {
        int dx = Mathf.Abs(a.gridX - b.gridX);
        int dy = Mathf.Abs(a.gridY - b.gridY);
        return Mathf.Min(dx, dy) * 14 + Mathf.Abs(dx - dy) * 10;
    }

    //切比雪夫距离
    static int ChebyshevDistance(Astar_Node a, Astar_Node b)
    {
        int dx = Mathf.Abs(a.gridX - b.gridX);
        int dy = Mathf.Abs(a.gridY - b.gridY);
        return Mathf.Max(dx, dy);
    }
    #endregion
}
