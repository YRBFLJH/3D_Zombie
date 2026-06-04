using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Google.Protobuf;
using Game;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager instance;

    [Header("网络配置")]
    public string ip = "127.0.0.1";
    public int port = 8888;
    public int playerId = -1;
    public bool isHost = false;
    public int currentRoomId = -1;

    public bool IsConnected => playerId >= 0;
    public int CurrentRoomId => currentRoomId;

    [Header("预制体")]
    public GameObject localPlayerPrefab;
    public GameObject remotePlayerPrefab;
    public GameObject enemyPrefab;

    private UdpClient udpClient;
    private IPEndPoint endpoint;
    private CancellationTokenSource cts;

    public Dictionary<int, GameObject> players = new Dictionary<int, GameObject>();
    public Dictionary<int, GameObject> enemies = new Dictionary<int, GameObject>();
    private HashSet<int> deadEnemyIds = new HashSet<int>();
    public GameObject LocalPlayer { get; private set; }
    private bool _localPlayerSpawnSynced = false;

    private Queue<byte[]> _dataQueue = new Queue<byte[]>();
    private readonly object _queueLock = new object();

    private bool _hasLastLocalPos = false;
    private Vector3 _lastLocalPos;
    private float _lastLocalSyncTime = 0f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ConnectToServer();
    }

    public void ConnectToServer()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.Client.ReceiveBufferSize = 65535;
            udpClient.Client.SendBufferSize = 65535;

            endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
            cts = new CancellationTokenSource();
            _ = ReceiveAsync(cts.Token);
            Debug.Log("✅ 连接服务器成功");
        }
        catch (Exception e)
        {
            Debug.LogError("❌ 连接失败：" + e.Message);
        }
    }

    private void Update()
    {
        lock (_queueLock)
        {
            var tempQueue = new Queue<byte[]>(_dataQueue);
            _dataQueue.Clear();

            while (tempQueue.Count > 0)
            {
                byte[] data = tempQueue.Dequeue();
                try
                {
                    if (ChestWireCodec.TryReadChestStateSyncFromGameMessageBuffer(data, out var chestSync))
                    {
                        try
                        {
                            HandleChestStateSync(chestSync);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("箱子同步处理失败: " + ex);
                        }
                        continue;
                    }

                    if (SaveWireCodec.TryDetectSaveMessage(data, out var saveType, out var saveBytes))
                    {
                        try
                        {
                            HandleSaveMessage(saveType, saveBytes);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("存档消息处理失败: " + ex);
                        }
                        continue;
                    }

                    GameMessage msg = GameMessage.Parser.ParseFrom(data);
                    try
                    {
                        ProcessMessage(msg);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("处理消息业务逻辑失败: " + e.Message);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("解析消息失败: " + e.Message);
                }
            }
        }
    }

    private async Task ReceiveAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync();
                lock (_queueLock)
                {
                    _dataQueue.Enqueue(result.Buffer);
                }
            }
            catch
            {
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private void SendJoin()
    {
        GameMessage msg = new GameMessage();
        msg.JoinRequest = new JoinRequest();
        SendMessageToServer(msg.ToByteArray());
    }

    public void SendLoginRequest(string account, string password)
    {
        GameMessage msg = new GameMessage();
        msg.LoginRequest = new LoginRequest { Account = account, Password = password };
        SendMessageToServer(msg.ToByteArray());
    }

    public void SendRegisterRequest(string account, string password)
    {
        GameMessage msg = new GameMessage();
        msg.RegisterRequest = new RegisterRequest { Account = account, Password = password };
        SendMessageToServer(msg.ToByteArray());
    }

    public void SendLeave()
    {
        if (playerId == -1) return;
        GameMessage msg = new GameMessage();
        msg.LeaveRequest = new LeaveRequest();
        SendMessageToServer(msg.ToByteArray());
    }

    public void SendMessageToServer(byte[] data)
    {
        try { udpClient.Send(data, data.Length, endpoint); }
        catch (Exception e) { Debug.LogError("发送消息失败: " + e.Message); }
    }

    // Lobby events
    public System.Action<RoomListResponse> OnRoomList;
    public System.Action<CreateRoomResponse> OnCreateRoom;
    public System.Action<JoinRoomResponse> OnJoinRoom;
    public System.Action OnGameStart;
    public System.Action OnKickedFromRoom;

    // Auth events
    public System.Action<LoginResponse> OnLoginResponse;

    // Save events
    public System.Action<PlayerSaveAckMsg> OnPlayerSaveAck;
    public System.Action<WorldSaveAckMsg> OnWorldSaveAck;
    public System.Action<DeleteRoomResponseMsg> OnDeleteRoomResponse;
    public System.Action<SavePlayerData> OnPlayerLoadData;
    [HideInInspector] public SavePlayerData pendingLoadData;

    private void ProcessMessage(GameMessage msg)
    {
        switch (msg.PayloadCase)
        {
            case GameMessage.PayloadOneofCase.AssignId:
                playerId = msg.AssignId.Id;
                isHost = msg.AssignId.Ishost;
                Debug.Log($"Connected: id={playerId}");
                break;

            case GameMessage.PayloadOneofCase.HostNotify:
                isHost = (msg.HostNotify.Hostid == playerId);
                Debug.Log($"Host notify: new host={msg.HostNotify.Hostid}, isHost={isHost}");
                break;

            case GameMessage.PayloadOneofCase.WorldState:
                HandleWorldState(msg.WorldState);
                break;

            case GameMessage.PayloadOneofCase.ShootEvent:
                HandleShootEvent(msg.ShootEvent);
                break;

            case GameMessage.PayloadOneofCase.HitResult:
                HandleHitResult(msg.HitResult);
                break;

            case GameMessage.PayloadOneofCase.PlayerStatsSync:
                HandlePlayerStatsSync(msg.PlayerStatsSync);
                break;

            case GameMessage.PayloadOneofCase.PlayerDeath:
                HandlePlayerDeath(msg.PlayerDeath);
                break;

            case GameMessage.PayloadOneofCase.PlayerRespawn:
                HandlePlayerRespawn(msg.PlayerRespawn);
                break;

            case GameMessage.PayloadOneofCase.EnemySpawn:
                HandleEnemySpawn(msg.EnemySpawn);
                break;

            case GameMessage.PayloadOneofCase.EnemyDespawn:
                HandleEnemyDespawn(msg.EnemyDespawn);
                break;

            // Lobby messages
            case GameMessage.PayloadOneofCase.RoomListResponse:
                OnRoomList?.Invoke(msg.RoomListResponse);
                break;

            case GameMessage.PayloadOneofCase.CreateRoomResponse:
                if (msg.CreateRoomResponse.Success)
                    currentRoomId = msg.CreateRoomResponse.RoomId;
                OnCreateRoom?.Invoke(msg.CreateRoomResponse);
                break;

            case GameMessage.PayloadOneofCase.JoinRoomResponse:
                if (msg.JoinRoomResponse.Success)
                    currentRoomId = msg.JoinRoomResponse.RoomId;
                OnJoinRoom?.Invoke(msg.JoinRoomResponse);
                break;

            case GameMessage.PayloadOneofCase.GameStartNotify:
                OnGameStart?.Invoke();
                break;

            case GameMessage.PayloadOneofCase.LeaveRoomResponse:
                currentRoomId = -1;
                OnKickedFromRoom?.Invoke();
                break;

            case GameMessage.PayloadOneofCase.ChestStateSync:
                HandleChestStateSyncProtocol(msg.ChestStateSync);
                break;

            // Auth messages
            case GameMessage.PayloadOneofCase.LoginResponse:
                var loginResp = msg.LoginResponse;
                if (loginResp.Success)
                {
                    playerId = loginResp.PlayerId;
                    isHost = false;
                    Debug.Log($"Logged in: id={playerId}");
                }
                OnLoginResponse?.Invoke(loginResp);
                break;

        }
    }

    // Lobby actions
    public void RequestRoomList()
    {
        GameMessage msg = new GameMessage();
        msg.RoomListRequest = new RoomListRequest();
        SendMessageToServer(msg.ToByteArray());
    }

    public void RequestCreateRoom(string roomName)
    {
        GameMessage msg = new GameMessage();
        msg.CreateRoomRequest = new CreateRoomRequest { RoomName = roomName };
        SendMessageToServer(msg.ToByteArray());
    }

    public void RequestJoinRoom(int roomId)
    {
        GameMessage msg = new GameMessage();
        msg.JoinRoomRequest = new JoinRoomRequest { RoomId = roomId };
        SendMessageToServer(msg.ToByteArray());
    }

    public void RequestStartGame()
    {
        GameMessage msg = new GameMessage();
        msg.StartGameRequest = new StartGameRequest();
        SendMessageToServer(msg.ToByteArray());
    }

    public void RequestLeaveRoom()
    {
        GameMessage msg = new GameMessage();
        msg.LeaveRoomRequest = new LeaveRoomRequest();
        SendMessageToServer(msg.ToByteArray());
    }

    public void SendShootRequest(Vector3 firePos, Vector3 dir)
    {
        if (playerId == -1) return;

        ShootRequest req = new ShootRequest
        {
            ShooterId = playerId,
            FirePosX = firePos.x,
            FirePosY = firePos.y,
            FirePosZ = firePos.z,
            DirX = dir.x,
            DirY = dir.y,
            DirZ = dir.z
        };

        GameMessage msg = new GameMessage();
        msg.ShootRequest = req;
        SendMessageToServer(msg.ToByteArray());
    }

    private void SpawnLocalPlayer(Vector3 spawnPos, Quaternion spawnRot)
    {
        LocalPlayer = Instantiate(localPlayerPrefab, spawnPos, spawnRot);
        LocalPlayer.GetComponent<Player>().isLocalPlayer = true;
        players[playerId] = LocalPlayer;
        _localPlayerSpawnSynced = true;
        _lastLocalPos = spawnPos;
        _hasLastLocalPos = true;
        _lastLocalSyncTime = Time.time;
    }

    private int _syncFrameCounter = 0;
    private const int SYNC_INTERVAL = 2; // send position sync every 2 input frames (~40ms)

    public IEnumerator SendInputLoop()
    {
        yield return new WaitForSeconds(0.1f);
        while (true)
        {
            if (LocalPlayer == null)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            var move = LocalPlayer.GetComponent<Player_Move>();
            if (move == null)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            var shoot = LocalPlayer.GetComponent<Player_Shoot>();
            var player = LocalPlayer.GetComponent<Player>();
            Vector2 inputAxis = move.moveInput;

            Camera cam = Camera.main;
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();
            Vector3 worldMoveDir = camRight * inputAxis.x + camForward * inputAxis.y;

            float actorRotY = LocalPlayer.transform.eulerAngles.y;

            Game.Input input = new Game.Input
            {
                Id = playerId,
                MoveX = worldMoveDir.x,
                MoveZ = worldMoveDir.z,
                RotY = actorRotY,
                Running = move.running,
                Aiming = shoot != null && shoot.isAiming
            };

            GameMessage msg = new GameMessage();
            msg.Input = input;
            SendMessageToServer(msg.ToByteArray());

            // Periodically sync actual client position to correct server drift
            _syncFrameCounter++;
            if (_syncFrameCounter >= SYNC_INTERVAL)
            {
                _syncFrameCounter = 0;
                Vector3 pos = LocalPlayer.transform.position;
                Vector3 lookDir = Camera.main.transform.forward;

                Game.PlayerTransformSync sync = new Game.PlayerTransformSync
                {
                    Id = playerId,
                    PosX = pos.x,
                    PosY = pos.y,
                    PosZ = pos.z,
                    RotY = actorRotY,
                    Speed = move.running ? 8.5f : (move.moveInput.magnitude > 0.1f ? 3.0f : 0f),
                    Running = move.running,
                    Aiming = shoot != null && shoot.isAiming,
                    Armed = player != null && player.isArmed,
                    LookDirX = lookDir.x,
                    LookDirY = lookDir.y,
                    LookDirZ = lookDir.z
                };

                GameMessage syncMsg = new GameMessage();
                syncMsg.PlayerTransformSync = sync;
                SendMessageToServer(syncMsg.ToByteArray());
            }

            yield return new WaitForSeconds(0.02f);
        }
    }

    private void HandleWorldState(WorldState ws)
    {
        HashSet<int> alivePlayerIds = new HashSet<int>();
        foreach (var s in ws.Players)
        {
            int id = s.Id;
            alivePlayerIds.Add(id);
            Vector3 serverPos = new Vector3(s.PosX, s.PosY, s.PosZ);
            Quaternion rot = Quaternion.Euler(0, s.RotY, 0);

            if (id == playerId)
            {
                if (LocalPlayer == null)
                {
                    SpawnLocalPlayer(serverPos, rot);
                }
                else if (!_localPlayerSpawnSynced)
                {
                    LocalPlayer.transform.position = serverPos;
                    LocalPlayer.transform.rotation = rot;
                    _localPlayerSpawnSynced = true;
                }
            }
            else
            {
                Vector3 lookDir = new Vector3(s.LookDirX, s.LookDirY, s.LookDirZ);
                if (!players.ContainsKey(id))
                {
                    GameObject go = Instantiate(remotePlayerPrefab, serverPos, rot);
                    players[id] = go;
                    var rp = go.GetComponent<RemotePlayer>();
                    rp?.Initialize(id);
                    rp?.UpdateState(serverPos, rot, s.Speed, s.IsRunning, s.IsAiming, s.IsArmed, lookDir);
                }
                else
                {
                    players[id].GetComponent<RemotePlayer>()?.UpdateState(serverPos, rot, s.Speed, s.IsRunning, s.IsAiming, s.IsArmed, lookDir);
                }
            }
        }

        List<int> disconnectedPlayers = new List<int>();
        foreach (var pair in players)
        {
            int id = pair.Key;
            if (id == playerId) continue;
            if (!alivePlayerIds.Contains(id))
            {
                disconnectedPlayers.Add(id);
            }
        }

        foreach (int id in disconnectedPlayers)
        {
            if (players.TryGetValue(id, out GameObject go))
            {
                Destroy(go);
            }
            players.Remove(id);
        }

        foreach (var e in ws.Enemies)
        {
            int enemyId = e.EnemyId;
            if (deadEnemyIds.Contains(enemyId))
            {
                if (!enemies.ContainsKey(enemyId)) continue;
            }
            Vector3 enemyPos = new Vector3(e.PosX, e.PosY, e.PosZ);
            Quaternion enemyRot = Quaternion.Euler(0, e.RotY, 0);

            if (!enemies.ContainsKey(enemyId))
            {
                GameObject enemyObj = Instantiate(enemyPrefab, enemyPos, enemyRot);
                Enemy_Controller enemyCtrl = enemyObj.GetComponent<Enemy_Controller>();
                if (enemyCtrl != null)
                {
                    enemyCtrl.enemyId = enemyId;
                    enemyCtrl.isLocalAI = false;
                }
                enemies[enemyId] = enemyObj;

                StartCoroutine(DelayedUpdateEnemyState(enemyId, e));
            }
            else
            {
                Enemy_Controller ctrl = enemies[enemyId].GetComponent<Enemy_Controller>();
                if (ctrl != null)
                {
                    ctrl.UpdateRemoteState(enemyPos, enemyRot, e.Speed, e.State, e.IsDead, e.IsAttack);
                }
            }
        }

        List<int> deadEnemies = new List<int>();
        foreach (var pair in enemies)
        {
            int enemyId = pair.Key;
            bool exists = false;
            foreach (var e in ws.Enemies)
            {
                if (e.EnemyId == enemyId) { exists = true; break; }
            }
            if (!exists) deadEnemies.Add(enemyId);
        }

        foreach (int enemyId in deadEnemies)
        {
            Destroy(enemies[enemyId]);
            enemies.Remove(enemyId);
        }
    }

    private void HandleShootEvent(ShootEvent shoot)
    {
        int shooterId = shoot.ShooterId;
        if (shooterId == playerId) return;
        if (!players.ContainsKey(shooterId)) return;

        Vector3 firePos = new Vector3(shoot.FirePosX, shoot.FirePosY, shoot.FirePosZ);
        Vector3 dir = new Vector3(shoot.DirX, shoot.DirY, shoot.DirZ);

        Player_Shoot remoteShoot = players[shooterId].GetComponent<Player_Shoot>();
        if (remoteShoot != null && remoteShoot.currentGun != null)
        {
            remoteShoot.PlayRemoteShoot(firePos, dir);
            return;
        }

        Player_ChangeHandItem remoteHandItem = players[shooterId].GetComponent<Player_ChangeHandItem>();
        if (remoteHandItem != null)
        {
            remoteHandItem.SetArmedStateByNetwork(true);
            remoteHandItem.PlayRemoteShootEffect(firePos, dir);
        }
    }

    private void HandleHitResult(HitResult hit)
    {
        // Server-authoritative HP/death: WorldState + UpdateRemoteState drive all enemy state.
        // Here we only play hit feedback — no local HP changes.
        if (hit.TargetType == 0 && enemies.TryGetValue(hit.TargetId, out GameObject enemyObj))
        {
            // TODO: play hit VFX at enemyObj.transform.position
            Debug.Log($"Enemy {hit.TargetId} hit for {hit.Damage}, HP remaining: {hit.RemainingHP}");
        }
    }

    private void HandlePlayerStatsSync(PlayerStatsSync stats)
    {
        if (stats.Id == playerId)
        {
            if (LocalPlayer == null) return;
            Player_State ps = LocalPlayer.GetComponent<Player_State>();
            if (ps == null) return;
            ps.ApplyServerStats(stats.Hp, stats.MaxHp, stats.Food, stats.Water, stats.IsDead);
        }
        else if (players.TryGetValue(stats.Id, out GameObject remoteObj))
        {
            // Update remote player's displayed HP
            Player_State ps = remoteObj.GetComponent<Player_State>();
            if (ps != null)
                ps.ApplyServerStats(stats.Hp, stats.MaxHp, stats.Food, stats.Water, stats.IsDead);
        }
    }

    private void HandlePlayerDeath(PlayerDeath death)
    {
        if (death.PlayerId == playerId && LocalPlayer != null)
        {
            Player_Move move = LocalPlayer.GetComponent<Player_Move>();
            Player_Shoot shoot = LocalPlayer.GetComponent<Player_Shoot>();
            if (move != null) move.enabled = false;
            if (shoot != null) shoot.enabled = false;
        }
    }

    private void HandlePlayerRespawn(PlayerRespawn respawn)
    {
        Vector3 spawnPos = new Vector3(respawn.PosX, respawn.PosY, respawn.PosZ);
        if (respawn.PlayerId == playerId && LocalPlayer != null)
        {
            LocalPlayer.transform.position = spawnPos;
            Player_Move move = LocalPlayer.GetComponent<Player_Move>();
            Player_Shoot shoot = LocalPlayer.GetComponent<Player_Shoot>();
            Player_State ps = LocalPlayer.GetComponent<Player_State>();
            if (move != null) move.enabled = true;
            if (shoot != null) shoot.enabled = true;
            if (ps != null) ps.Respawn();
        }
    }

    private void HandleEnemySpawn(EnemySpawn spawn)
    {
        int enemyId = spawn.EnemyId;
        if (enemies.ContainsKey(enemyId) || deadEnemyIds.Contains(enemyId)) return;

        Vector3 pos = new Vector3(spawn.PosX, spawn.PosY, spawn.PosZ);
        Quaternion rot = Quaternion.Euler(0, spawn.RotY, 0);
        GameObject enemyObj = Instantiate(enemyPrefab, pos, rot);
        Enemy_Controller ctrl = enemyObj.GetComponent<Enemy_Controller>();
        if (ctrl != null)
        {
            ctrl.enemyId = enemyId;
            ctrl.isLocalAI = false;
        }
        enemies[enemyId] = enemyObj;
    }

    private void HandleEnemyDespawn(EnemyDespawn despawn)
    {
        int enemyId = despawn.EnemyId;
        deadEnemyIds.Add(enemyId);
        if (enemies.TryGetValue(enemyId, out GameObject enemyObj))
        {
            Enemy_Controller ctrl = enemyObj.GetComponent<Enemy_Controller>();
            if (ctrl != null) ctrl.isDead = true;
            Destroy(enemyObj);
            enemies.Remove(enemyId);
        }
    }

    private IEnumerator DelayedUpdateEnemyState(int enemyId, EnemyState e)
    {
        yield return null;
        if (enemies.ContainsKey(enemyId))
        {
            Enemy_Controller ctrl = enemies[enemyId].GetComponent<Enemy_Controller>();
            if (ctrl != null)
            {
                Vector3 enemyPos = new Vector3(e.PosX, e.PosY, e.PosZ);
                Quaternion enemyRot = Quaternion.Euler(0, e.RotY, 0);
                ctrl.UpdateRemoteState(enemyPos, enemyRot, e.Speed, e.State, e.IsDead, e.IsAttack);
            }
        }
    }

    public void UnregisterEnemyByObject(GameObject enemyRoot)
    {
        if (enemyRoot == null) return;
        List<int> removeIds = new List<int>();
        foreach (var pair in enemies)
        {
            if (pair.Value == enemyRoot)
            {
                removeIds.Add(pair.Key);
            }
        }
        foreach (int id in removeIds)
        {
            enemies.Remove(id);
            deadEnemyIds.Add(id);
        }
    }

    public void SendChestStateRequest(int chestId)
    {
        if (playerId < 0) return;
        byte[] raw = ChestWireCodec.BuildGameMessageWithChestStateRequest(playerId, chestId);
        SendMessageToServer(raw);
    }

    public void SendChestStateSubmit(int chestId, List<InventoryItem> items)
    {
        if (playerId < 0) return;
        byte[] raw = ChestWireCodec.BuildGameMessageWithChestStateSubmit(playerId, chestId, items);
        SendMessageToServer(raw);
    }

    // ===== 存档消息发送 =====

    public void SendPlayerSave(int roomId, SavePlayerData data)
    {
        if (playerId < 0) return;
        byte[] raw = SaveWireCodec.BuildPlayerSaveSubmit(playerId, roomId, data);
        SendMessageToServer(raw);
        Debug.Log($"[存档] 发送个人存档 roomId={roomId}");
    }

    public void SendWorldSave(int roomId, SaveWorldData data)
    {
        if (playerId < 0) { Debug.LogWarning("[存档] SendWorldSave 跳过: playerId<0"); return; }
        try
        {
            byte[] raw = SaveWireCodec.BuildWorldSaveSubmit(playerId, roomId, data);
            Debug.Log($"[存档] BuildWorldSaveSubmit 完成, {raw.Length} bytes");
            SendMessageToServer(raw);
            Debug.Log($"[存档] 发送世界存档 roomId={roomId}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[存档] SendWorldSave 异常: {ex}");
        }
    }

    public void SendClearAccounts()
    {
        // Raw tag 43 (ClearAccounts): (43<<3)|2 = 346 = 0xDA 0x02
        byte[] raw = { 0xDA, 0x02, 0x00 };
        SendMessageToServer(raw);
        Debug.Log("[Auth] 请求清除所有账号");
    }

    public void SendClearAllSaves()
    {
        if (playerId < 0) return;
        byte[] raw = SaveWireCodec.BuildClearAllSaves();
        SendMessageToServer(raw);
        Debug.Log("[存档] 请求清除所有房间存档");
    }

    public void SendDeleteRoom(int roomId)
    {
        if (playerId < 0) return;
        byte[] raw = SaveWireCodec.BuildDeleteRoomRequest(playerId, roomId);
        SendMessageToServer(raw);
        Debug.Log($"[存档] 请求删除房间 roomId={roomId}");
    }

    void HandleSaveMessage(SaveMessageType type, byte[] data)
    {
        switch (type)
        {
            case SaveMessageType.PlayerSaveAck:
                var pAck = SaveWireCodec.ParsePlayerSaveAck(data);
                Debug.Log($"[存档] PlayerSaveAck success={pAck.success}");
                OnPlayerSaveAck?.Invoke(pAck);
                break;
            case SaveMessageType.WorldSaveAck:
                var wAck = SaveWireCodec.ParseWorldSaveAck(data);
                Debug.Log($"[存档] WorldSaveAck success={wAck.success}");
                OnWorldSaveAck?.Invoke(wAck);
                break;
            case SaveMessageType.DeleteRoomResponse:
                var dResp = SaveWireCodec.ParseDeleteRoomResponse(data);
                Debug.Log($"[存档] DeleteRoomResponse success={dResp.success}");
                OnDeleteRoomResponse?.Invoke(dResp);
                break;
            case SaveMessageType.PlayerLoadData:
                var loadData = SaveWireCodec.ParsePlayerData(data);
                Debug.Log($"[存档] 收到玩家加载数据 playerId={loadData.playerId}");
                if (OnPlayerLoadData != null)
                    OnPlayerLoadData.Invoke(loadData);
                else
                    pendingLoadData = loadData; // buffer until player subscribes
                break;
            default:
                Debug.LogWarning($"[存档] 未知存档消息类型: {type}");
                break;
        }
    }

    void HandleChestStateSync(ChestStateSyncPayload p)
    {
        if (!ChestNetworkRegistry.TryGet(p.ChestId, out ChestInventory inv) || inv == null) return;
        var items = p.Items ?? new List<InventoryItem>();
        inv.ApplyNetworkSnapshot(items, p.FromPlayerId);
    }

    /// <summary> 处理服务端标准protobuf格式的ChestStateSync </summary>
    void HandleChestStateSyncProtocol(ChestStateSync chestSync)
    {
        if (!ChestNetworkRegistry.TryGet(chestSync.ChestId, out ChestInventory inv) || inv == null) return;

        List<InventoryItem> items = new List<InventoryItem>();
        foreach (var it in chestSync.Items)
        {
            items.Add(new InventoryItem(it.ItemId, it.Amount, it.X, it.Y, it.GridType, it.GridIndex, it.Rotated));
        }
        inv.ApplyNetworkSnapshot(items, chestSync.FromPlayerId);
    }

    private void OnDestroy()
    {
        SendLeave();
        cts?.Cancel();
        udpClient?.Close();
    }

    private void OnApplicationQuit()
    {
        // Trigger auto-save on all players BEFORE closing socket
        var handlers = FindObjectsOfType<AutoSaveHandler>();
        foreach (var h in handlers)
            h.SendAutoSave();

        // Small delay to let UDP packets flush
        System.Threading.Thread.Sleep(100);

        SendLeave();
        cts?.Cancel();
        udpClient?.Close();
    }
}