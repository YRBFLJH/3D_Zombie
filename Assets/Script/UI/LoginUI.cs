using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game;

public class LoginUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField accountInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public Button registerButton;
    public Button delAccountsButton;
    public TMP_Text errorText;

    [Header("Panel References")]
    public GameObject loginPanel;
    public GameObject lobbyPanel;

    private Coroutine errorHideCoroutine;

    void Start()
    {
        if (NetworkManager.instance == null)
        {
            Debug.LogError("NetworkManager not found");
            return;
        }

        NetworkManager.instance.OnLoginResponse += OnLoginResponse;

        loginButton?.onClick.AddListener(OnLoginClick);
        registerButton?.onClick.AddListener(OnRegisterClick);
        delAccountsButton?.onClick.AddListener(OnDelAccountsClick);

        loginPanel?.SetActive(true);
        lobbyPanel?.SetActive(false);

        if (errorText != null) errorText.gameObject.SetActive(false);

        AudioManager.Instance?.PlayThemeMusic();
    }

    void OnDestroy()
    {
        if (NetworkManager.instance != null)
            NetworkManager.instance.OnLoginResponse -= OnLoginResponse;
    }

    void OnDelAccountsClick()
    {
        NetworkManager.instance.SendClearAccounts();
        ShowError("已清除所有账号数据");
    }

    void OnLoginClick()
    {
        string account = accountInput?.text?.Trim() ?? "";
        string password = passwordInput?.text ?? "";

        if (string.IsNullOrEmpty(account))
        {
            ShowError("请输入账号");
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            ShowError("请输入密码");
            return;
        }

        NetworkManager.instance.SendLoginRequest(account, password);
    }

    void OnRegisterClick()
    {
        string account = accountInput?.text?.Trim() ?? "";
        string password = passwordInput?.text ?? "";

        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
        {
            ShowError("请输入账号和密码");
            return;
        }

        NetworkManager.instance.SendRegisterRequest(account, password);
    }

    void OnLoginResponse(LoginResponse resp)
    {
        if (resp.Success)
        {
            // Defer by one frame so LobbyUI.Start() runs before we request room list.
            // Otherwise the server's initial RoomListResponse (sent right after LoginResponse)
            // arrives before LobbyUI callbacks are set up and is silently dropped.
            StartCoroutine(ShowLobbyNextFrame());
        }
        else
        {
            if (!string.IsNullOrEmpty(resp.Error))
                ShowError(resp.Error);
        }
    }

    System.Collections.IEnumerator ShowLobbyNextFrame()
    {
        yield return null; // wait one frame

        loginPanel?.SetActive(false);
        lobbyPanel?.SetActive(true);

        var lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
            lobbyUI.RefreshRoomList();
    }

    void ShowError(string msg)
    {
        if (errorText == null) return;
        errorText.text = msg;
        errorText.gameObject.SetActive(true);
        if (errorHideCoroutine != null) StopCoroutine(errorHideCoroutine);
        errorHideCoroutine = StartCoroutine(HideErrorAfter(3f));
    }

    IEnumerator HideErrorAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (errorText != null)
            errorText.gameObject.SetActive(false);
    }
}
