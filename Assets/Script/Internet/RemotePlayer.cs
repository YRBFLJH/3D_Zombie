using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    int playerId;

    Vector3 targetPosition;
    Quaternion targetRotation;
    Vector3 moveVelocity; // for SmoothDamp
    float currentSpeed;
    Vector3 moveDirection;
    float timeSinceLastUpdate;

    Animator anim;
    Player_Animator playerAnimator;
    Player_ChangeHandItem playerChangeHandItem;

    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private float rotationSpeed = 18f;
    private const float MaxExtrapolateTime = 0.15f; // cap extrapolation to prevent overshoot

    void Awake()
    {
        anim = GetComponent<Animator>();
        playerAnimator = GetComponent<Player_Animator>();
        playerChangeHandItem = GetComponent<Player_ChangeHandItem>();

        // Ensure remote player is NOT treated as local (IK uses lookDir not camera)
        var player = GetComponent<Player>();
        if (player != null) player.isLocalPlayer = false;
    }

    public void Initialize(int id)
    {
        playerId = id;
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    public void UpdateState(Vector3 position, Quaternion rotation, float speed,
        bool running, bool aiming, bool armed, Vector3 lookDir)
    {
        targetPosition = position;
        targetRotation = rotation;
        currentSpeed = speed;
        moveDirection = rotation * Vector3.forward;
        timeSinceLastUpdate = 0f;

        bool hasMoveIntent = speed > 0.1f;

        playerAnimator.PlayArmed(armed || aiming);
        playerChangeHandItem?.SetArmedStateByNetwork(armed || aiming);
        playerAnimator.SetRemoteLookDirection(lookDir);
        playerAnimator.PlayIdle(!hasMoveIntent);
        playerAnimator.PlayMove(hasMoveIntent && !running);
        playerAnimator.PlayRun(hasMoveIntent && running);
        playerAnimator.PlayAim(aiming);
    }

    void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;

        // Extrapolate target forward based on last known velocity to bridge gaps
        // between server updates (50ms broadcast interval)
        float extrapolateTime = Mathf.Min(timeSinceLastUpdate, MaxExtrapolateTime);
        Vector3 extrapolated = targetPosition + moveDirection * (currentSpeed * extrapolateTime);

        transform.position = Vector3.SmoothDamp(
            transform.position, extrapolated,
            ref moveVelocity, smoothTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation,
            Mathf.Clamp01(Time.deltaTime * rotationSpeed));
    }
}
