using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Mirror;

public class Player_Shoot : NetworkBehaviour
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

    private GunController currentGun;

    public void SetCurrentGun(GunController gun)
    {
        currentGun = gun;
    }

    public void FinishReload()
    {
        if(currentGun != null)
            currentGun.FinishReload();
    }

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
            playerAnimator.cameraRightOffset = 1.5f;
        }
        else
        {
            isAiming = false;
            playerAnimator.PlayAim(false);
            playerAnimator.cameraRightOffset = 0;
        }

        SmoothTransition();
    }

    void SmoothTransition()
    {
        if (isAiming)
        {
            crosshair.gameObject.SetActive(true);
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 40f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.45f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.25f, smoothSpeed * Time.deltaTime);
        }
        else
        {
            crosshair.gameObject.SetActive(false);
            virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(virtualCamera.m_Lens.FieldOfView, 60f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.CameraDistance = Mathf.Lerp(thirdPersonCamera.CameraDistance, 2.75f, smoothSpeed * Time.deltaTime);
            thirdPersonCamera.ShoulderOffset.x = Mathf.Lerp(thirdPersonCamera.ShoulderOffset.x, 0.3f, smoothSpeed * Time.deltaTime);
            composer.m_ScreenX = Mathf.Lerp(composer.m_ScreenX, 0.4f, smoothSpeed * Time.deltaTime);
        }
    }

    public void Reload()
    {
        playerAnimator.PlayReload();
    }

}