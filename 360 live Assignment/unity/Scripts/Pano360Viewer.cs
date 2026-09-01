using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// API demo: ขอภาพ crop ตามมุมที่ XR Camera หัน แล้วแสดงบน Quad
public class Pano360Viewer : MonoBehaviour
{
    [Header("Server")]
    [Tooltip("URL ของ mock server เช่น http://192.168.1.50:5000")]
    public string serverBaseUrl = "http://192.168.1.50:5000";

    [Tooltip("360 = มองได้รอบตัวเต็ม, 180 = จำกัดครึ่งซีก")]
    public string mode = "360";

    [Range(10, 150)]
    public float fov = 90f;

    [Header("Target")]
    [Tooltip("Renderer ของ Quad ที่แสดงภาพจาก server")]
    public Renderer targetRenderer;

    [Header("Timing")]
    [Tooltip("ระยะเวลาห่างขั้นต่ำระหว่าง request (วินาที)")]
    public float minRequestInterval = 0.2f;

    private float lastRequestTime = -999f;
    private bool requestInFlight;
    private Texture2D downloadedTexture;

    private void Update()
    {
        if (Time.time - lastRequestTime < minRequestInterval || requestInFlight)
        {
            return;
        }

        float yaw = transform.eulerAngles.y;
        float pitch = NormalizePitch(transform.eulerAngles.x);

        lastRequestTime = Time.time;
        StartCoroutine(RequestView(yaw, pitch));
    }

    private static float NormalizePitch(float rawX)
    {
        float pitch = rawX;
        if (pitch > 180f)
        {
            pitch -= 360f;
        }
        return Mathf.Clamp(pitch, -90f, 90f);
    }

    private IEnumerator RequestView(float yaw, float pitch)
    {
        requestInFlight = true;
        string url = $"{serverBaseUrl}/pano/view?yaw={yaw:F1}&pitch={pitch:F1}&fov={fov:F1}&mode={mode}";

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ApplyDownloadedTexture(DownloadHandlerTexture.GetContent(request));
            }
            else
            {
                Debug.LogWarning($"[Pano360Viewer] Request failed: {request.error} (url={url})");
            }
        }

        requestInFlight = false;
    }

    private void ApplyDownloadedTexture(Texture2D texture)
    {
        if (targetRenderer == null)
        {
            Destroy(texture);
            return;
        }

        if (downloadedTexture != null)
        {
            Destroy(downloadedTexture);
        }

        downloadedTexture = texture;
        targetRenderer.material.mainTexture = texture;
    }

    // เรียกจากปุ่ม UI เพื่อสลับโหมดระหว่าง demo
    public void SetMode(string newMode)
    {
        if (newMode == "360" || newMode == "180")
        {
            mode = newMode;
        }
    }

    private void OnDestroy()
    {
        if (downloadedTexture != null)
        {
            Destroy(downloadedTexture);
        }
    }
}
