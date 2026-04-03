using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using UnityEngine.PlayerLoop;

public class Internet : MonoBehaviour
{
    public Button SingleButton;
    public Button HostButton;
    public Button JoinButton;

    public GameObject PlayerPrefab;

    void Start()
    {
        SingleButton.onClick.AddListener(Single);
        HostButton.onClick.AddListener(Host);
        JoinButton.onClick.AddListener(Join);
    }

    public void Host()
    {
        NetworkManager.singleton.StartHost();
    }
    public void Join()
    {
        NetworkManager.singleton.StartClient();
    }
    public void Single()
    {
        NetworkManager.singleton.StartHost();
    }
}
