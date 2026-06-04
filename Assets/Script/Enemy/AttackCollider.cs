using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackCollider : MonoBehaviour
{
    // Disabled: damage is now server-authoritative via HitResult messages.
    // Server EnemyAI handles attack damage calculation and broadcasts results.

    void OnTriggerEnter(Collider other)
    {
        // No-op: server validates all damage
    }
}
