using Mirror;
using UnityEngine;

public class Player_ChangeHandItem : NetworkBehaviour
{
    Player_Getcomponent playerComponent;
    Player Player;

    public GameObject GunPrefab;

    GameObject currentGun;          // 本地客户端持有的枪引用
    GameObject serverGun;           // 服务器端持有的枪引用（用于命令）

    Transform GunSpawnPoint;

    void Awake()
    {
        playerComponent = GetComponent<Player_Getcomponent>();
    }

    void Start()
    {
        Player = playerComponent.playerCS;
        GunSpawnPoint = playerComponent.ItemSpawnPoint;
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Input.GetKeyDown(KeyCode.Tab))
        {   
            if(currentGun == null)
            {
                CmdSpawnGun();
            }
            else
            {
                CmdSetGunActive(!currentGun.activeSelf);
            }
        }
    }

    [Command]
    void CmdSpawnGun()
    {
        GameObject gun = Instantiate(GunPrefab);
        gun.transform.SetParent(GunSpawnPoint, false);
        NetworkServer.Spawn(gun, connectionToClient);
        gun.GetComponent<GunController>().ownerNetId = netId;
        gun.GetComponent<GunController>().ownPlayer = Player;

        serverGun = gun; // 服务器储存生成修改的枪

        // 通知所有客户端设置正确的父级
        RpcSetGunParent(gun, netId);
        // 通知客户端读取服务器修改好的枪
        TargetGetGun(connectionToClient, gun);
    }

    [Command]
    void CmdSetGunActive(bool active)
    {
        if (serverGun != null)
        {
            serverGun.SetActive(active);
            // 通知所有客户端同步激活状态
            RpcSetGunActive(serverGun, active);
        }
    }

    [ClientRpc]
    void RpcSetGunActive(GameObject gun, bool active)
    {
        if (gun != null)
        {
            gun.SetActive(active);
        }
    }

    [ClientRpc]
    void RpcSetGunParent(GameObject gun, uint ownerNetId)
    {
        // 查找拥有该枪的玩家对象
        GameObject owner = NetworkClient.spawned[ownerNetId]?.gameObject;

        Transform spawnPoint = owner.GetComponent<Player_Getcomponent>().ItemSpawnPoint;
        gun.transform.SetParent(spawnPoint, false);

        // 让枪的 GunController 识别到自己的持有者
        GunController gc = gun.GetComponent<GunController>();
        if (gc != null)
        {
            gc.ownPlayer = owner.GetComponent<Player>();
            gc.Setup(gc.ownPlayer);
        }
    }

    [TargetRpc]
    void TargetGetGun(NetworkConnection conn, GameObject gun)
    {
        currentGun = gun;
    }
}