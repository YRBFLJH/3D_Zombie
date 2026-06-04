using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    int playerId;

    Vector3 targetPosition;
    Quaternion targetRotation;
    Vector3 moveVelocity; // for SmoothDamp

    Animator anim;
    Player_Animator playerAnimator;
    Player_ChangeHandItem playerChangeHandItem;

    [SerializeField] private float smoothTime = 0.06f;
    [SerializeField] private float rotationSpeed = 18f;

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
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPosition,
            ref moveVelocity, smoothTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation,
            Mathf.Clamp01(Time.deltaTime * rotationSpeed));
    }
}
