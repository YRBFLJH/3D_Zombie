using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Enemy_Controller : NetworkBehaviour
{
    [HideInInspector]
    public Transform target;
    public NetworkAnimator animator;
    [HideInInspector]
    public Animator anim;


    // 状态机
    [HideInInspector]
    [SyncVar] public bool isDead = false;
    [HideInInspector]
    public Enemy_State state;
    [HideInInspector]
    public AIStateMachine stateMachine;
    [HideInInspector]
    public IdleState idleState;
    [HideInInspector]
    public WalkState walkState;
    [HideInInspector]
    public RunState runState;
    [HideInInspector]
    public AttackState attackState;
    [HideInInspector]
    public DeadState deadState;

    [HideInInspector]
    public float rotationSpeed = 320f;
    Vector3 originalPosition; // 记录原始位置（以此格子为中心进行随机走动、拉拖区域）

    // 随机移动
    float lastRandomMoveTime;
    float randomMoveTime = 5f;

    // 寻路
    List<Vector3> path;
    int pathIndex;
    [HideInInspector]
    public Vector3 endPosition; // 寻路终点（在玩家后面一些，与AI的直线上）
    Vector3 lastTargetPos;
    float repathDistance = 1.5f; // 目标移动大于于这个值时重新寻路

    // 探测玩家
    float viewDistance = 8f;
    float viewAngle = 50f; // 探测扇形区域的角度
    public LayerMask playerLayer; // 玩家所在层次
    public LayerMask obstacleLayer; // 障碍物所在层次

    [HideInInspector]
    [SyncVar] public float speed;
    [HideInInspector]
    [SyncVar] public bool animRun;
    [HideInInspector]
    [SyncVar] public bool animWalk;
    [HideInInspector]
    [SyncVar] public bool animIdle;

    [SyncVar] float health;
    [SyncVar] float maxHealth;


    void Awake()
    {
        originalPosition = transform.position;

        // 状态机绑定
        stateMachine = new AIStateMachine(this);
        idleState = new IdleState(stateMachine);
        walkState = new WalkState(stateMachine);
        runState = new RunState(stateMachine);
        attackState = new AttackState(stateMachine);
        deadState = new DeadState(stateMachine);
    }

    void Start()
    {
        anim = animator.animator;

        stateMachine.ChangeState(idleState); // 初始状态为站立

        health = maxHealth = 100f;
    }


    void Update()
    {
        if (isServer)
        {
            //让状态机实时更新状态
            stateMachine.UpdateState();

            // 实时探测是否发现玩家
            AutoFindPlayer();

            // 如果玩家在探测范围内，开始寻路移动
            if (target != null) StartMoveByFindPlayer();
            // 否则定时随机移动（巡逻）
            else
            {
                if (Time.time >= lastRandomMoveTime + randomMoveTime)
                {
                    RandomMovePath();
                    lastRandomMoveTime = Time.time;
                }
                MoveAlongPath();
            }
            
            // 通过速度更新状态
            UpdateStateBySpeed();
        }
        if (isDead)
        {
            anim.SetBool("isRun", false);
            anim.SetBool("isWalk", false);
            anim.SetBool("isIdle", false);
            return;
        }
        anim.SetBool("isRun", animRun);
        anim.SetBool("isWalk", animWalk);
        anim.SetBool("isIdle", animIdle);





         //Debug可视化路径、终点（测试用）
            Debug.DrawLine(transform.position + Vector3.up, endPosition + Vector3.up, Color.cyan, 0.5f);
            Debug.DrawLine(endPosition + Vector3.up * 0.5f + Vector3.left * 0.3f, endPosition + Vector3.up * 0.5f + Vector3.right * 0.3f, Color.magenta, 0.5f);
            Debug.DrawLine(endPosition + Vector3.up * 0.5f + Vector3.back * 0.3f, endPosition + Vector3.up * 0.5f + Vector3.forward * 0.3f, Color.magenta, 0.5f);

    }


    // 开始寻路移动
    void StartMoveByFindPlayer()
    {
        // 路径空了、走完了、玩家新移动超过阈值时重新寻路
        if (path == null || pathIndex >= path.Count || Vector3.Distance(target.position, lastTargetPos) > repathDistance)
        {
            // 起点：自身
            Astar_Node starNode = Astar_GridMap.instance.WorldToWalkableNode(transform.position);

            // 终点：目标（玩家）与自身的直线方位后面一些【防止寻路终点为玩家自身会不符合游戏体验】
            Vector3 dirFromAI = (target.position - transform.position).normalized; // AI和目标之间的向量
            endPosition = target.position - dirFromAI * 0.75f;
            Astar_Node endNode = Astar_GridMap.instance.WorldToWalkableNode(endPosition);

            // 算出路径
            List<Vector3> newPath = Astar_FindPath.FindPath(starNode, endNode);
            if (newPath != null && newPath.Count > 0)
            {
                path = SmoothPath(newPath); // 经过平滑处理的路径
                pathIndex = 0;
                lastTargetPos = endPosition;
            }
            else
            {
                path = null;
                return;
            }
        }

        // 沿着路径移动
        MoveAlongPath();
    }

    // 人物旋转
    void Rotation(Vector3 moveDirection)
    {
        if (moveDirection != Vector3.zero)
        {
            Vector3 offsetDir = Quaternion.Euler(0, -13.5f, 0) * moveDirection;
            Quaternion targetRotation = Quaternion.LookRotation(offsetDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    // 绘制探测范围(可视化) 
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 forward = transform.forward * viewDistance;
        Vector3 rightDir = Quaternion.Euler(0, viewAngle, 0) * forward;
        Vector3 leftDir = Quaternion.Euler(0, -viewAngle, 0) * forward;
        
        Gizmos.DrawLine(transform.position, transform.position + forward);
        Gizmos.DrawLine(transform.position, transform.position + rightDir);
        Gizmos.DrawLine(transform.position, transform.position + leftDir);
    }

    // 自动探测寻找玩家（多玩家时只锁最近的，不主动丢失目标）
void AutoFindPlayer()
{
    // 已经有目标了，就不重新找了 —— 完全保留你原来逻辑
    if (target != null) return;

    Collider[] hitColliders = Physics.OverlapSphere(transform.position, viewDistance, playerLayer);

    Transform closestPlayer = null;
    float closestDist = Mathf.Infinity;

    foreach (var col in hitColliders)
    {
        Vector3 dirToPlayer = (col.transform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        if (angle <= viewAngle)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            // 只记录最近的
            if (dist < closestDist)
            {
                closestDist = dist;
                closestPlayer = col.transform;
            }
        }
    }

    // 找到最近的才绑定，其他逻辑不动
    if (closestPlayer != null)
    {
        target = closestPlayer;
        lastTargetPos = target.position;
    }
}



    // 简单路径平滑：从当前位置的点开始，能直线看见的点(中间没有障碍物)就合并成一个【极大减少了中途的小拐弯】
    List<Vector3> SmoothPath(List<Vector3> rawPath)
    {
        if (rawPath == null || rawPath.Count == 0) return rawPath;

        List<Vector3> result = new List<Vector3>();
        Vector3 currentPos = transform.position;

        int index = 0;
        while (index < rawPath.Count)
        {
            Vector3 furthest = rawPath[index];
            int furthestIndex = index;

            // 尝试尽可能往后看，找到一个最远、但中间没有障碍的点
            for (int i = index + 1; i < rawPath.Count; i++)
            {
                Vector3 candidate = rawPath[i];
                Vector3 from = currentPos + Vector3.up * 0.2f;   // 稍微抬高一点避免地面碰撞
                Vector3 to = candidate + Vector3.up * 0.2f;

                // 如果中间没有障碍，就可以直接走直线到这个更远的点
                if (!Physics.Linecast(from, to, obstacleLayer))
                {
                    furthest = candidate;
                    furthestIndex = i;
                }
                else
                {
                    // 再往后肯定也被挡住了，可以 break
                    break;
                }
            }

            result.Add(furthest);
            currentPos = furthest;
            index = furthestIndex + 1;
        }

        return result;
    }


    // 在半径radius范围内随机一个移动的格子
    Astar_Node RandomMoveNode(int radius = 5)
    {
        Astar_Node centerNode = Astar_GridMap.instance.WorldToWalkableNode(originalPosition);
        List<Astar_Node> validNodes = new List<Astar_Node>();

        // 遍历中心节点周围半径范围内的所有格子
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int checkX = centerNode.gridX + x;
                int checkY = centerNode.gridY + y;

                // 检查是否在地图边界内
                if (checkX >= 0 && checkX < Astar_GridMap.instance.mapWidth &&
                    checkY >= 0 && checkY < Astar_GridMap.instance.mapHeight)
                {
                    Astar_Node node = Astar_GridMap.instance.gridMap[checkX, checkY];

                    if (node.walkable)
                    {
                        validNodes.Add(node);
                    }
                }
            }
        }

        // 随机返回一个有效格子
        if (validNodes.Count > 0)
        {
            return validNodes[UnityEngine.Random.Range(0, validNodes.Count)];
        }

        return null; // 找不到就返回空
    }

    // 寻找随机移动的路径
    void RandomMovePath()
    {
        if (path == null || pathIndex >= path.Count)
        {
            Astar_Node starNode = Astar_GridMap.instance.WorldToWalkableNode(transform.position);
            Astar_Node endNode = RandomMoveNode();
            List<Vector3> newPath = Astar_FindPath.FindPath(starNode, endNode);
            if (newPath != null && newPath.Count > 0)
            {
                path = SmoothPath(newPath);
                pathIndex = 0;
            }
            else
            {
                path = null;
                return;
            }
        }
    }

    // 移动（随机移动、追随玩家通用）
    void MoveAlongPath()
    {
        if (path != null && pathIndex < path.Count)
        {
            // 判断是随机移动还是跟随玩家（target有无）来区分奔跑、行走动作
            if (!isDead && stateMachine.currentState != attackState && stateMachine.currentState != deadState)
            {
                if (target != null)
                    stateMachine.ChangeState(runState);
                else
                    stateMachine.ChangeState(walkState);
            }

            Vector3 targetPos = path[pathIndex];
            targetPos.y = transform.position.y;

            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            if (!state.isAttack && !isDead) // 非攻击状态 朝向路径点
            {
                Vector3 moveDirection = (targetPos - transform.position).normalized;
                Rotation(moveDirection);
            }
            else if(state.isAttack && !state.isStopRotate) // 攻击状态、动画开始播放了（锁定不能旋转） 攻击时朝向玩家
            {
                Vector3 moveDirection = (target.position - transform.position).normalized;
                Rotation(moveDirection);
            }
            

            // 几乎到达路径点时，可当作已到达此路径点
            if (Vector3.Distance(transform.position, targetPos) < 0.25f)
            {
                pathIndex++;
            }
        }
        else
        {
            speed = 0;
        }
    }

    // 根据速度更新状态（）
    void UpdateStateBySpeed()
    {
        if (stateMachine.currentState == attackState || stateMachine.currentState == deadState)
            return;

        // Mathf.Approximately用于比较两个浮点数是否近似相等（约等于）    速度的数值由状态机管理(进入状态时设置)
        if (Mathf.Approximately(speed, 3f))
            stateMachine.ChangeState(walkState);
        else if (Mathf.Approximately(speed, 5.5f))
            stateMachine.ChangeState(runState);
        else if (Mathf.Approximately(speed, 0f))
            stateMachine.ChangeState(idleState);
    }

    // 判断是否攻击
    public bool CanAttack()
    {
        if (target == null) return false;

        if (Vector3.Distance(transform.position, endPosition) < 0.85f)
            return true;
        else return false;
    }

    // 状态脚本调用，让所有客户端调用（SetTrigger不能[SyncVar]同步）
    [ClientRpc]
    public void RpcPlayAttackTrigger()
    {
        anim.SetTrigger("isAttack");
    }

    [ClientRpc]
    public void RpcPlayDeadTrigger()
    {
        anim.SetTrigger("isDead");
    }

    [Server]
    public void TakeDamage(float damage)
    {
        if (health <= 0)
        {
            isDead = true;
            path = null;
            target = null;
            return;
        }
        health = Mathf.Max(0, health - damage);
    }
}