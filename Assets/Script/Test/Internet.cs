using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class Internet : MonoBehaviour
{
    public Button HostButton;
    public Button JoinButton;

    void Start()
    {
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
}
