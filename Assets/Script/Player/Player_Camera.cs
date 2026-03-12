using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("目标设置")]
    public Transform player;        // 玩家Transform
    public Vector3 offset = new Vector3(0, 2, -5);  // 相对于玩家的偏移量
    
    [Header("平滑跟随设置")]
    public float smoothSpeed = 5f;   // 平滑跟随速度
    
    void LateUpdate()
    {
        if (player == null) return;
        
        // 计算期望的摄像机位置（玩家位置 + 偏移量）
        Vector3 desiredPosition = player.position + player.TransformDirection(offset);
        
        // 平滑移动到目标位置
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // 始终看向玩家
        transform.LookAt(player);
    }
}