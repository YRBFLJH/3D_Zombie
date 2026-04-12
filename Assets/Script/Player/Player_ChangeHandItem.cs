using UnityEngine;

public class Player_ChangeHandItem : MonoBehaviour
{
    Player_Getcomponent playerComponent;
    Player Player;

    public GameObject GunPrefab;

    GameObject currentGun;          // 当前持有的枪引用

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
        if (Input.GetKeyDown(KeyCode.Tab))
        {   
            if (currentGun == null)
            {
                SpawnGun();
            }
            else
            {
                currentGun.SetActive(!currentGun.activeSelf);
            }
        }
    }

    void SpawnGun()
    {
        GameObject gun = Instantiate(GunPrefab);
        gun.transform.SetParent(GunSpawnPoint, false);
        gun.GetComponent<GunController>().ownPlayer = Player;

        currentGun = gun;
    }
}