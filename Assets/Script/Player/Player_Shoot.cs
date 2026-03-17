using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class Player_Shoot : MonoBehaviour
{
    public CinemachineVirtualCamera virtualCamera;
    public Transform crosshair;

    private Player_Animator playerAnimator;

    private Cinemachine3rdPersonFollow thirdPersonCamera;
    private CinemachineComposer composer;

    public bool canShoot;

    public float smoothSpeed = 8f;

    // 瞄准状态
    public bool isAiming = false;

    void Awake()
    {
        playerAnimator = GetComponent<Player_Animator>();
        thirdPersonCamera = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        composer = virtualCamera.GetCinemachineComponent<CinemachineComposer>();
    }

    void Update()
    {
        if (Input.GetMouseButton(1) && canShoot)
        {
            isAiming = true;
            playerAnimator.PlayAim(true);
        }
        else
        {
            isAiming = false;
            playerAnimator.PlayAim(false);
        }

        SmoothTransition();
    }

    void SmoothTransition()
    {
        if (isAiming)
        {
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 40f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.45f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.25f, smoothSpeed * Time.deltaTime);
        }
        else
        {
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 60f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2.75f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.3f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.4f, smoothSpeed * Time.deltaTime);
        }
    }
}