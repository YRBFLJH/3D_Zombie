using System;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class NetClient : MonoBehaviour
{
    string ip = "127.0.0.1";
    int port = 8888;

    UdpClient client;
    IPEndPoint endPoint;

    void Start()
    {
        client = new UdpClient();
        endPoint = new IPEndPoint(IPAddress.Parse(ip), port);

        SendMsg("Hello");
        ReceiveMsg();
    }

    // Update is called once per frame
    void SendMsg(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        client.Send(data, data.Length, endPoint);
    }

    async void ReceiveMsg()
    {
        while (true)
        {
            try
            {
                var data = await client.ReceiveAsync();
                string msg = Encoding.UTF8.GetString(data.Buffer);
                Debug.Log("收到服务器信息" + msg);
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }
    }
}
