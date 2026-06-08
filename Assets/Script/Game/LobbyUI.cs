using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Game;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject lobbyPanel;
    public GameObject roomListPanel;
    public GameObject roomPanel;
    public Transform roomListContent;
    public GameObject roomItemPrefab;
    public TMP_InputField roomNameInput;
    public Button createRoomBtn;
    public Button refreshBtn;
    public Button startGameBtn;
    public Button leaveRoomBtn;
    public Button clearAllBtn; // 清除所有房间存档（测试用）
    public TMP_Text roomTitleText;
    public TMP_Text playerListText;
    public TMP_Text tipText;  // "正在等待玩家：X/4"
    public TMP_Text errorText; // Error messages, auto-hides after 3s

    [Header("Settings")]
    public string gameSceneName = "Game";

    private List<RoomInfo> currentRooms = new List<RoomInfo>();
    private bool inRoom = false;
    private int myRoomId = -1;
    private bool isRoomOwner = false;
    private Coroutine heartbeatCoroutine;
    private Coroutine errorHideCoroutine;
    private bool gameStarting = false;

    void Start()
    {
        if (NetworkManager.instance == null)
        {
            Debug.LogError("NetworkManager not found");
            return;
        }

        NetworkManager.instance.OnRoomList += OnRoomList;
        NetworkManager.instance.OnCreateRoom += OnCreateRoom;
        NetworkManager.instance.OnJoinRoom += OnJoinRoom;
        NetworkManager.instance.OnGameStart += OnGameStart;
        NetworkManager.instance.OnKickedFromRoom += OnKicked;
        NetworkManager.instance.OnDeleteRoomResponse += OnDeleteRoom;

        createRoomBtn?.onClick.AddListener(CreateRoom);
        refreshBtn?.onClick.AddListener(RefreshRoomList);
        startGameBtn?.onClick.AddListener(StartGame);
        leaveRoomBtn?.onClick.AddListener(LeaveRoom);
        clearAllBtn?.onClick.AddListener(ClearAllSaves);

        ShowLobby();
        if (NetworkManager.instance.IsConnected)
            RefreshRoomList();

        // Error text starts hidden
        if (errorText != null) errorText.gameObject.SetActive(false);

        // Heartbeat: refresh room list every 4s to prevent server timeout
        heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
    }

    void OnDestroy()
    {
        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);

        if (NetworkManager.instance != null)
        {
            NetworkManager.instance.OnRoomList -= OnRoomList;
            NetworkManager.instance.OnCreateRoom -= OnCreateRoom;
            NetworkManager.instance.OnJoinRoom -= OnJoinRoom;
            NetworkManager.instance.OnGameStart -= OnGameStart;
            NetworkManager.instance.OnKickedFromRoom -= OnKicked;
            NetworkManager.instance.OnDeleteRoomResponse -= OnDeleteRoom;
        }
    }

    void OnKicked()
    {
        Debug.Log("Kicked from room (owner left)");
        inRoom = false;
        myRoomId = -1;
        isRoomOwner = false;
        gameStarting = false;
        ShowLobby();
    }

    IEnumerator HeartbeatLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(4f);
            if (!gameStarting && NetworkManager.instance != null
                && NetworkManager.instance.IsConnected)
                NetworkManager.instance.RequestRoomList();
        }
    }

    void ShowLobby()
    {
        lobbyPanel?.SetActive(true);
        roomListPanel?.SetActive(true);
        roomPanel?.SetActive(false);
        startGameBtn?.gameObject.SetActive(false);
        inRoom = false;
    }

    void ShowRoom()
    {
        lobbyPanel?.SetActive(false);
        roomListPanel?.SetActive(false);
        roomPanel?.SetActive(true);
        startGameBtn?.gameObject.SetActive(isRoomOwner);
    }

    public void RefreshRoomList()
    {
        if (NetworkManager.instance != null)
            NetworkManager.instance.RequestRoomList();
    }

    void CreateRoom()
    {
        string name = roomNameInput != null ? roomNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            ShowError("房间名不能为空");
            return;
        }
        NetworkManager.instance.RequestCreateRoom(name);
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

    void StartGame()
    {
        if (NetworkManager.instance != null)
            NetworkManager.instance.RequestStartGame();
    }

    void LeaveRoom()
    {
        if (NetworkManager.instance != null)
            NetworkManager.instance.RequestLeaveRoom();
        ShowLobby();
    }

    void OnRoomList(RoomListResponse list)
    {
        currentRooms.Clear();
        foreach (var room in list.Rooms)
            currentRooms.Add(room);

        // Update room list UI
        if (roomListContent != null && roomItemPrefab != null)
        {
            foreach (Transform child in roomListContent)
                Destroy(child.gameObject);

            foreach (var room in currentRooms)
            {
                GameObject item = Instantiate(roomItemPrefab, roomListContent);
                var texts = item.GetComponentsInChildren<TMP_Text>();
                if (texts.Length >= 2)
                {
                    texts[0].text = room.RoomName;
                    texts[1].text = $"{room.PlayerCount}/{room.MaxPlayers} {(room.InGame ? "[游戏中]" : "")}";
                }
                var btn = item.GetComponent<Button>();
                if (btn != null)
                {
                    // Allow joining in-game rooms (mid-game join) but not full rooms
                    bool canJoin = !room.InGame || (room.InGame && room.PlayerCount < room.MaxPlayers);
                    btn.interactable = canJoin;
                    if (canJoin)
                    {
                        int roomId = room.RoomId;
                        btn.onClick.AddListener(() => JoinRoom(roomId));
                    }
                }

                // 空房间 = 该玩家自己的存档房间，显示删除按钮
                var delBtn = item.transform.Find("DeleteBtn")?.GetComponent<Button>();
                if (delBtn != null)
                {
                    delBtn.gameObject.SetActive(room.PlayerCount == 0 && !room.InGame);
                    delBtn.onClick.RemoveAllListeners();
                    if (room.PlayerCount == 0 && !room.InGame)
                    {
                        int roomId = room.RoomId;
                        delBtn.onClick.AddListener(() => DeleteRoom(roomId));
                    }
                }
            }
        }

        // Update room panel if we have a room ID
        if (myRoomId > 0)
        {
            bool found = false;
            foreach (var room in currentRooms)
            {
                if (room.RoomId == myRoomId)
                {
                    if (roomTitleText) roomTitleText.text = room.RoomName;
                    if (tipText) tipText.text = $"正在等待玩家：{room.PlayerCount}/{room.MaxPlayers}";
                    found = true;
                    break;
                }
            }
            // Room no longer exists (kicked) → back to lobby
            if (!found && inRoom)
            {
                inRoom = false;
                myRoomId = -1;
                isRoomOwner = false;
                ShowLobby();
            }
        }
    }

    void JoinRoom(int roomId)
    {
        if (NetworkManager.instance != null)
            NetworkManager.instance.RequestJoinRoom(roomId);
    }

    void OnCreateRoom(CreateRoomResponse resp)
    {
        if (resp.Success)
        {
            myRoomId = resp.RoomId;
            inRoom = true;
            isRoomOwner = true;
            if (NetworkManager.instance != null)
                NetworkManager.instance.pendingLoadData = null;
            AutoSaveHandler.ResetSavedAmmo();
            ShowRoom();
            UpdateRoomInfoFromCache();
        }
        else if (!string.IsNullOrEmpty(resp.Error))
        {
            ShowError(resp.Error == "DUPLICATE" ? "房间名已重复" : resp.Error);
        }
    }

    void OnJoinRoom(JoinRoomResponse resp)
    {
        if (resp.Success)
        {
            myRoomId = resp.RoomId;
            inRoom = true;
            gameStarting = false; // Reset for potential mid-game join GameStartNotify
            // Check if server assigned us as host (ownership restored via account match)
            isRoomOwner = NetworkManager.instance != null && NetworkManager.instance.isHost;
            // Clear stale load data from previous sessions (safe: PlayerLoadData not sent yet)
            if (NetworkManager.instance != null)
                NetworkManager.instance.pendingLoadData = null;
            AutoSaveHandler.ResetSavedAmmo();
            ShowRoom();
            UpdateRoomInfoFromCache();
        }
    }

    void UpdateRoomInfoFromCache()
    {
        foreach (var room in currentRooms)
        {
            if (room.RoomId == myRoomId)
            {
                if (roomTitleText) roomTitleText.text = room.RoomName;
                if (tipText) tipText.text = $"正在等待玩家：{room.PlayerCount}/{room.MaxPlayers}";
                return;
            }
        }
    }

    void UpdateTipFromCache()
    {
        if (tipText == null) return;
        foreach (var room in currentRooms)
        {
            if (room.RoomId == myRoomId)
            {
                tipText.text = $"正在等待玩家：{room.PlayerCount}/{room.MaxPlayers}";
                return;
            }
        }
    }

    void OnGameStart()
    {
        if (gameStarting) return;
        gameStarting = true;

        lobbyPanel?.SetActive(false);

        if (NetworkManager.instance != null)
        {
            NetworkManager.instance.StartCoroutine(
                NetworkManager.instance.SendInputLoop());
        }

        AudioManager.Instance?.StopMusic();
        SceneManager.LoadScene(gameSceneName);
    }

    void ClearAllSaves()
    {
        if (NetworkManager.instance != null)
        {
            NetworkManager.instance.SendClearAllSaves();
            Debug.Log("[Lobby] 已发送清除所有存档请求");
            RefreshRoomList();
        }
    }

    void DeleteRoom(int roomId)
    {
        if (NetworkManager.instance != null)
        {
            NetworkManager.instance.SendDeleteRoom(roomId);
            Debug.Log($"[Lobby] 请求删除房间 {roomId}");
        }
    }

    void OnDeleteRoom(DeleteRoomResponseMsg resp)
    {
        if (resp.success)
        {
            Debug.Log("[Lobby] 房间已删除");
            RefreshRoomList();
        }
        else
        {
            ShowError("删除房间失败");
        }
    }
}
