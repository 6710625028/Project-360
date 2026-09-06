using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;

public class StreamReceiver : MonoBehaviour
{
    [Header("Server Connection")]
    public string serverIp = "127.0.0.1"; // จะอัปเดตอัตโนมัติเมื่อเจอกล่องสัญญาณ
    public string currentMode = "360";
    public float currentAngle = 0f;

    [Header("Target Texture")]
    public RenderTexture targetRenderTexture;

    private bool isFetching = false;
    private UdpClient udpClient;
    private int currentModeIndex = 0;

    void Start()
    {
        StartCoroutine(StartUdpDiscovery());
        StartCoroutine(FetchLoop());
    }

    IEnumerator StartUdpDiscovery()
    {
        // คอยดักฟังสัญญาณค้นหาจาก Python Server
        udpClient = new UdpClient();
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 5005));
        udpClient.EnableBroadcast = true;

        while (true)
        {
            if (udpClient.Available > 0)
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref ep);
                string msg = Encoding.UTF8.GetString(data);

                if (msg == "PICO_SERVER_HERE")
                {
                    string discoveredIp = ep.Address.ToString();
                    if (serverIp != discoveredIp)
                    {
                        serverIp = discoveredIp;
                        Debug.Log("[StreamReceiver] ตรวจพบ Server อัตโนมัติที่ IP: " + serverIp);
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    void Update()
    {
        // 1. รองรับการกดปุ่ม 1-5 บนคีย์บอร์ด PC
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeStreamMode("360", 0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeStreamMode("180", 0);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeStreamMode("180", 90);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangeStreamMode("180", 180);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ChangeStreamMode("180", 270);

        // 2. รองรับปุ่มกดบนแว่น Pico 4 (ปุ่ม Trigger ด้านหน้า หรือปุ่มหลัก A/X)
        if (Input.GetKeyDown(KeyCode.Space) || CheckVRTriggerPressed())
        {
            ToggleNextMode();
        }
    }

    bool CheckVRTriggerPressed()
    {
        var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
            return true;
        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.TryGetFeatureValue(CommonUsages.triggerButton, out bool leftPressed) && leftPressed)
            return true;
        return false;
    }

    public void ToggleNextMode()
    {
        currentModeIndex = (currentModeIndex + 1) % 4;
        switch (currentModeIndex)
        {
            case 0: ChangeStreamMode("360", 0); break;
            case 1: ChangeStreamMode("180", 0); break;
            case 2: ChangeStreamMode("180", 90); break;
            case 3: ChangeStreamMode("180", 180); break;
        }
    }

    public void ChangeStreamMode(string mode, float angle)
    {
        currentMode = mode;
        currentAngle = angle;
        Debug.Log($"[StreamReceiver] ปรับโหมดเป็น: {mode} มุม {angle} องศา");
    }

    IEnumerator FetchLoop()
    {
        while (true)
        {
            if (!isFetching && targetRenderTexture != null)
            {
                StartCoroutine(DownloadFrame());
            }
            yield return new WaitForSeconds(0.015f); // เร่งความเร็วการดึงภาพ
        }
    }

    IEnumerator DownloadFrame()
    {
        isFetching = true;
        string url = $"http://{serverIp}:5000/frame?mode={currentMode}&angle={currentAngle}&t={Time.realtimeSinceStartup}";

        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                Graphics.Blit(tex, targetRenderTexture);
                Destroy(tex);
            }
        }
        isFetching = false;
    }

    void OnDestroy()
    {
        if (udpClient != null) udpClient.Close();
    }
}