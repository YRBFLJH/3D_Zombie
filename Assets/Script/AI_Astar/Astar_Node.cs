using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A星寻路的网格地图中的点坐标
public class Astar_Node
{
    public bool walkable; // 当前点是否可走
    public Vector3 worldPos; // 当前点在游戏场景中的世界坐标
    public int gridX, gridY; // 当前点在网格地图中的坐标

    // A星算法中的G、H、F公式
    public int G; // 起点到目标点的距离
    public int H; // 目标点到终点的距离
    public int F => G + H;

    public Astar_Node parent; // 父节点(回溯路径)
}
